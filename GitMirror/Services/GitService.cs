using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.Extensions.Logging;
using GitMirror.Configuration;

namespace GitMirror.Services;

public class GitService
{
    private readonly ILogger<GitService> _logger;
    private readonly GitMirrorConfig _config;
    private const string OriginRemoteName = "origin";
    private const string SourceRemoteName = "source";

    public GitService(ILogger<GitService> logger, GitMirrorConfig config)
    {
        _logger = logger;
        _config = config;
    }

    public Task<bool> MirrorRepositoryAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                _logger.LogInformation("Starting repository mirror operation");
                
                CleanupLocalRepository();
                CloneSourceRepository();
                
                using var repo = new Repository(_config.LocalPath);
                SetupTargetRemote(repo);
                PushToTarget(repo);
                CleanupLocalRepository();
                
                _logger.LogInformation("Repository mirror operation completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during repository mirror operation");
                CleanupLocalRepository();
                return false;
            }
        });
    }

    public async Task<bool> SyncRepositoryAsync()
    {
        try
        {
            _logger.LogInformation("Starting repository sync operation");

            // Check if local repository exists and is valid
            if (!Directory.Exists(_config.LocalPath) || !Repository.IsValid(_config.LocalPath))
            {
                _logger.LogInformation("Local repository not found or invalid, performing initial mirror");
                return await MirrorRepositoryAsync();
            }

            using var repo = new Repository(_config.LocalPath);

            if (!await FetchFromSource(repo))
                return false;

            if (!HasNewCommits(repo))
            {
                _logger.LogInformation("Repository is up to date");
                return true;
            }

            UpdateLocalRepository(repo);
            PushToTarget(repo);

            _logger.LogInformation("Repository sync completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during repository sync operation");
            return false;
        }
    }

    private void CleanupLocalRepository()
    {
        if (Directory.Exists(_config.LocalPath))
        {
            try
            {
                Directory.Delete(_config.LocalPath, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up local repository");
            }
        }
    }

    private void CloneSourceRepository()
    {
        var cloneOptions = new CloneOptions
        {
            IsBare = false,
            BranchName = _config.SourceRepository.Branch
        };
        
        cloneOptions.FetchOptions.CredentialsProvider = GetCredentialsHandler(_config.SourceRepository);
        Repository.Clone(_config.SourceRepository.Url, _config.LocalPath, cloneOptions);
    }

    private void SetupTargetRemote(Repository repo)
    {
        var originRemote = repo.Network.Remotes[OriginRemoteName];
        if (originRemote != null)
        {
            repo.Network.Remotes.Remove(OriginRemoteName);
        }
        repo.Network.Remotes.Add(OriginRemoteName, _config.TargetRepository.Url);
    }

    private void PushToTarget(Repository repo)
    {
        try
        {
            _logger.LogInformation("Starting push to target repository: {TargetUrl}", _config.TargetRepository.Url);

            var remote = repo.Network.Remotes[OriginRemoteName];
            if (remote == null)
            {
                throw new InvalidOperationException($"Remote '{OriginRemoteName}' not found in repository");
            }

            var pushOptions = new PushOptions
            {
                CredentialsProvider = GetCredentialsHandler(_config.TargetRepository),
                OnPushStatusError = (pushStatusError) =>
                {
                    _logger.LogError("Push status error for {Reference}: {Message}",
                        pushStatusError.Reference, pushStatusError.Message);
                }
            };

            var pushRefSpecs = remote.PushRefSpecs.Select(x => x.Specification).ToList();
            if (!pushRefSpecs.Any())
            {
                // If no push refspecs, use the current branch
                var currentBranch = repo.Head;
                if (currentBranch?.FriendlyName != null)
                {
                    pushRefSpecs.Add($"refs/heads/{currentBranch.FriendlyName}:refs/heads/{currentBranch.FriendlyName}");
                    _logger.LogDebug("Using current branch for push: {BranchName}", currentBranch.FriendlyName);
                }
                else
                {
                    throw new InvalidOperationException("No push refspecs found and no current branch detected");
                }
            }

            _logger.LogDebug("Pushing {RefSpecCount} ref specs to target", pushRefSpecs.Count);
            repo.Network.Push(remote, pushRefSpecs, pushOptions);
            _logger.LogInformation("Successfully pushed to target repository");
        }
        catch (LibGit2SharpException ex)
        {
            _logger.LogError(ex, "LibGit2Sharp error during push to target repository: {TargetUrl}. " +
                "This usually means the target repository doesn't exist, you don't have permission, or there are network issues.",
                _config.TargetRepository.Url);
            throw new InvalidOperationException($"Failed to push to target repository '{_config.TargetRepository.Url}': {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during push to target repository: {TargetUrl}", _config.TargetRepository.Url);
            throw new InvalidOperationException($"Unexpected error during push: {ex.Message}", ex);
        }
    }

    private async Task<bool> FetchFromSource(Repository repo)
    {
        return await Task.Run(() =>
        {
            try
            {
                var sourceRemote = repo.Network.Remotes.FirstOrDefault(r => r.Name == SourceRemoteName);
                if (sourceRemote == null)
                {
                    repo.Network.Remotes.Add(SourceRemoteName, _config.SourceRepository.Url);
                    sourceRemote = repo.Network.Remotes[SourceRemoteName];
                }

                var fetchOptions = new FetchOptions
                {
                    CredentialsProvider = GetCredentialsHandler(_config.SourceRepository)
                };

                var refSpecs = sourceRemote.FetchRefSpecs.Select(x => x.Specification);
                Commands.Fetch(repo, sourceRemote.Name, refSpecs, fetchOptions, "Fetching from source");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch from source repository");
                return false;
            }
        });
    }

    private bool HasNewCommits(Repository repo)
    {
        var sourceBranch = repo.Branches[$"{SourceRemoteName}/{_config.SourceRepository.Branch}"];
        var localBranch = repo.Branches[_config.SourceRepository.Branch];
        return sourceBranch?.Tip?.Sha != localBranch?.Tip?.Sha;
    }

    private void UpdateLocalRepository(Repository repo)
    {
        var sourceBranch = repo.Branches[$"{SourceRemoteName}/{_config.SourceRepository.Branch}"];
        Commands.Checkout(repo, sourceBranch);
    }

    private CredentialsHandler GetCredentialsHandler(RepositoryConfig repoConfig)
    {
        return (url, usernameFromUrl, types) =>
        {
            _logger.LogDebug("Credential request for {Url}, types: {Types}", url, types);

            // If explicit credentials are provided, use them
            if (!string.IsNullOrEmpty(repoConfig.Token))
            {
                _logger.LogDebug("Using provided credentials for {Url}", url);
                return new UsernamePasswordCredentials
                {
                    Username = string.IsNullOrEmpty(repoConfig.Username) ? repoConfig.Token : repoConfig.Username,
                    Password = repoConfig.Token
                };
            }

            // Try to use system Git credential helper by invoking git credential fill
            try
            {
                _logger.LogDebug("Attempting to use system Git credential helper for {Url}", url);
                var credentials = GetSystemCredentials(url);
                if (credentials != null)
                {
                    _logger.LogDebug("Successfully obtained credentials from system for {Url}", url);
                    return credentials;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get credentials from system credential helper for {Url}", url);
            }

            // If all else fails, try default credentials (for SSH, etc.)
            _logger.LogDebug("Using default credentials for {Url}", url);
            return new DefaultCredentials();
        };
    }

    private UsernamePasswordCredentials? GetSystemCredentials(string url)
    {
        try
        {
            var uri = new Uri(url);
            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "git";
            process.StartInfo.Arguments = "credential fill";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();

            // Send the URL info to git credential
            using (var writer = process.StandardInput)
            {
                writer.WriteLine($"protocol={uri.Scheme}");
                writer.WriteLine($"host={uri.Host}");
                if (uri.Port != -1 && !uri.IsDefaultPort)
                {
                    writer.WriteLine($"port={uri.Port}");
                }
                writer.WriteLine($"path={uri.AbsolutePath.TrimStart('/')}");
                writer.WriteLine(); // Empty line to signal end
            }

            process.WaitForExit(5000); // 5 second timeout

            if (process.ExitCode == 0)
            {
                string? username = null;
                string? password = null;
                
                string output = process.StandardOutput.ReadToEnd();
                foreach (string line in output.Split('\n'))
                {
                    if (line.StartsWith("username="))
                        username = line.Substring(9).Trim();
                    else if (line.StartsWith("password="))
                        password = line.Substring(9).Trim();
                }

                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    return new UsernamePasswordCredentials
                    {
                        Username = username,
                        Password = password
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Exception while trying to get system credentials");
        }

        return null;
    }
}
