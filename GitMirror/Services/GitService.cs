using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.Extensions.Logging;
using GitMirror.Configuration;

namespace GitMirror.Services;

public class GitService(ILogger<GitService> logger, GitMirrorConfig config)
{
    private readonly ILogger<GitService> _logger = logger;
    private readonly GitMirrorConfig _config = config;
    private const string OriginRemoteName = "origin";
    private const string SourceRemoteName = "source";

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
        if (!Directory.Exists(_config.LocalPath))
            return;

        try
        {
            _logger.LogDebug("Cleaning up local repository: {LocalPath}", _config.LocalPath);
            
            // Force delete with read-only file handling
            DeleteDirectoryWithReadOnlyFiles(_config.LocalPath);
            
            _logger.LogDebug("Successfully cleaned up local repository");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean up local repository: {LocalPath}", _config.LocalPath);
            throw new InvalidOperationException($"Unable to clean up local repository at '{_config.LocalPath}'. This may prevent future sync operations.", ex);
        }
    }

    private static void DeleteDirectoryWithReadOnlyFiles(string path)
    {
        if (!Directory.Exists(path))
            return;

        // Remove read-only attributes from all files recursively
        ClearReadOnlyAttributes(path);
        
        // Now delete the directory
        Directory.Delete(path, recursive: true);
    }

    private static void ClearReadOnlyAttributes(string path)
    {
        try
        {
            // Clear read-only attribute from the directory itself
            var dirInfo = new DirectoryInfo(path);
            if (dirInfo.Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                dirInfo.Attributes &= ~FileAttributes.ReadOnly;
            }

            // Clear read-only attributes from all files
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.Attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    fileInfo.Attributes &= ~FileAttributes.ReadOnly;
                }
            }

            // Clear read-only attributes from all subdirectories
            foreach (var dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
            {
                var subDirInfo = new DirectoryInfo(dir);
                if (subDirInfo.Attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    subDirInfo.Attributes &= ~FileAttributes.ReadOnly;
                }
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail - the delete might still work
            Console.WriteLine($"Warning: Could not clear read-only attributes: {ex.Message}");
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
                LogCredentialGuidance(url);

                var credentials = GetSystemCredentials(url);
                if (credentials != null)
                {
                    _logger.LogInformation("Successfully authenticated with {Url}", url);
                    return credentials;
                }

                _logger.LogWarning("Git credential helper did not provide valid credentials for {Url}", url);
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
            using var process = CreateGitCredentialProcess();
            
            _logger.LogDebug("Starting git credential process for {Url}", url);
            process.Start();

            SendUrlInfoToProcess(process, uri);
            
            _logger.LogInformation("Waiting for Git credential authentication (this may show a dialog)...");
            process.WaitForExit(30000); // 30 second timeout for interactive dialogs

            return HandleProcessResult(process);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Exception while trying to get system credentials");
            return null;
        }
    }

    private static System.Diagnostics.Process CreateGitCredentialProcess()
    {
        var process = new System.Diagnostics.Process();
        process.StartInfo.FileName = "git";
        process.StartInfo.Arguments = "credential fill";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = false; // Allow UI dialogs to show

        // Set environment variables to allow interactive authentication
        process.StartInfo.Environment["GIT_TERMINAL_PROMPT"] = "1";
        process.StartInfo.Environment["GCM_INTERACTIVE"] = "auto";

        return process;
    }

    private static void SendUrlInfoToProcess(System.Diagnostics.Process process, Uri uri)
    {
        using var writer = process.StandardInput;
        writer.WriteLine($"protocol={uri.Scheme}");
        writer.WriteLine($"host={uri.Host}");
        if (uri.Port != -1 && !uri.IsDefaultPort)
        {
            writer.WriteLine($"port={uri.Port}");
        }
        writer.WriteLine($"path={uri.AbsolutePath.TrimStart('/')}");
        writer.WriteLine(); // Empty line to signal end
    }

    private UsernamePasswordCredentials? HandleProcessResult(System.Diagnostics.Process process)
    {
        if (process.HasExited && process.ExitCode == 0)
        {
            return ParseCredentialsFromOutput(process.StandardOutput.ReadToEnd());
        }
        
        if (!process.HasExited)
        {
            _logger.LogWarning("Git credential process timed out - killing process");
            process.Kill();
            process.WaitForExit(1000);
        }
        else
        {
            LogProcessError(process);
        }

        return null;
    }

    private UsernamePasswordCredentials? ParseCredentialsFromOutput(string output)
    {
        _logger.LogDebug("Git credential output received");
        
        string? username = null;
        string? password = null;

        foreach (string line in output.Split('\n'))
        {
            if (line.StartsWith("username="))
                username = line.Substring(9).Trim();
            else if (line.StartsWith("password="))
                password = line.Substring(9).Trim();
        }

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            _logger.LogDebug("Successfully obtained credentials from Git credential helper");
            return new UsernamePasswordCredentials
            {
                Username = username,
                Password = password
            };
        }

        _logger.LogWarning("Git credential helper returned success but no valid username/password");
        return null;
    }

    private void LogProcessError(System.Diagnostics.Process process)
    {
        _logger.LogWarning("Git credential process exited with code: {ExitCode}", process.ExitCode);
        string errorOutput = process.StandardError.ReadToEnd();
        if (!string.IsNullOrEmpty(errorOutput))
        {
            _logger.LogDebug("Git credential error output: {ErrorOutput}", errorOutput);
        }
    }

    /// <summary>
    /// Checks if the application is running in an interactive environment
    /// </summary>
    private static bool IsInteractiveEnvironment()
    {
        return Environment.UserInteractive && !Console.IsInputRedirected && !Console.IsOutputRedirected;
    }

    /// <summary>
    /// Provides user-friendly guidance for credential configuration
    /// </summary>
    private void LogCredentialGuidance(string url)
    {
        _logger.LogInformation("=== Git Authentication Required ===");
        _logger.LogInformation("Repository: {Url}", url);
        
        if (IsInteractiveEnvironment())
        {
            _logger.LogInformation("If a dialog appears, please select your GitHub account and complete authentication.");
            _logger.LogInformation("This may take up to 30 seconds...");
        }
        else
        {
            _logger.LogInformation("Running in non-interactive mode. Consider setting explicit credentials:");
            _logger.LogInformation("Option 1: Set environment variables:");
            _logger.LogInformation("  GitMirror__TargetRepository__Token=your-github-token");
            _logger.LogInformation("Option 2: Update appsettings.json with your credentials");
        }
    }
}
