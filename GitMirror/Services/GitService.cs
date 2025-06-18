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
            
            if (!Directory.Exists(_config.LocalPath))
            {
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
        var remote = repo.Network.Remotes[OriginRemoteName];
        var pushOptions = new PushOptions
        {
            CredentialsProvider = GetCredentialsHandler(_config.TargetRepository)
        };

        var pushRefSpecs = remote.PushRefSpecs.Select(x => x.Specification);
        repo.Network.Push(remote, pushRefSpecs, pushOptions);
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
            if (!string.IsNullOrEmpty(repoConfig.Token))
            {
                return new UsernamePasswordCredentials
                {
                    Username = string.IsNullOrEmpty(repoConfig.Username) ? repoConfig.Token : repoConfig.Username,
                    Password = repoConfig.Token
                };
            }
            
            return new DefaultCredentials();
        };
    }
}
