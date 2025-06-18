# GitMirror

A .NET console application that synchronizes Git repositories by pulling from a source repository and pushing to a target repository.

## Features

- **Mirror Operation**: Clone from source repository and push to target repository
- **Sync Operation**: Fetch updates from source and push to target (maintains local copy)
- **Daemon Mode**: Continuously sync at specified intervals
- **Authentication Support**: Support for personal access tokens and credentials
- **Configurable**: JSON configuration file for easy setup
- **Logging**: Comprehensive logging with configurable levels

## Configuration

Create or modify the `appsettings.json` file to configure your repositories:

```json
{
  "GitMirror": {
    "SourceRepository": {
      "Url": "https://github.com/source/repository.git",
      "Branch": "main",
      "Username": "your-username",
      "Token": "your-personal-access-token"
    },
    "TargetRepository": {
      "Url": "https://github.com/target/repository.git", 
      "Branch": "main",
      "Username": "your-username",
      "Token": "your-personal-access-token"
    },
    "LocalPath": "./temp_repo",
    "SyncInterval": 300
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "GitMirror": "Debug"
    }
  }
}
```

### Configuration Options

- **SourceRepository.Url**: The URL of the repository to pull from
- **SourceRepository.Branch**: The branch to sync (default: "main")
- **SourceRepository.Username**: Username for authentication (optional)
- **SourceRepository.Token**: Personal access token or password for authentication
- **TargetRepository.Url**: The URL of the repository to push to
- **TargetRepository.Branch**: The target branch (default: "main")
- **TargetRepository.Username**: Username for authentication (optional)
- **TargetRepository.Token**: Personal access token or password for authentication
- **LocalPath**: Local directory for temporary repository clone
- **SyncInterval**: Interval in seconds for daemon mode

## Usage

### Build the Application

```bash
dotnet build
```

### Run Commands

#### One-time Mirror Operation
Clones the source repository and pushes it to the target repository:

```bash
dotnet run mirror
```

#### One-time Sync Operation
Fetches updates from source and pushes to target (default if no command specified):

```bash
dotnet run sync
# or simply
dotnet run
```

#### Daemon Mode
Runs continuously, syncing at the specified interval:

```bash
dotnet run daemon
```

### Command Line Arguments

You can also override configuration using command line arguments or environment variables.

## Authentication

### Personal Access Tokens (Recommended)

For GitHub, GitLab, and other Git hosting services, use personal access tokens:

1. Generate a personal access token with appropriate permissions
2. Set the `Token` field in the configuration
3. Leave `Username` empty or set it to your username

### Username/Password

For basic authentication:

1. Set both `Username` and `Token` (where `Token` is your password)

### Environment Variables

You can set credentials using environment variables:

```bash
# Set environment variables
$env:GitMirror__SourceRepository__Token = "your-source-token"
$env:GitMirror__TargetRepository__Token = "your-target-token"

# Run the application
dotnet run
```

## Examples

### Mirror a Public Repository to Your Private Repository

```json
{
  "GitMirror": {
    "SourceRepository": {
      "Url": "https://github.com/microsoft/dotnet.git",
      "Branch": "main"
    },
    "TargetRepository": {
      "Url": "https://github.com/yourusername/dotnet-mirror.git",
      "Branch": "main",
      "Token": "your-github-token"
    }
  }
}
```

### Sync Between Two Private Repositories

```json
{
  "GitMirror": {
    "SourceRepository": {
      "Url": "https://github.com/company/internal-repo.git",
      "Branch": "develop",
      "Token": "source-repo-token"
    },
    "TargetRepository": {
      "Url": "https://github.com/backup/internal-repo-backup.git",
      "Branch": "develop",
      "Token": "target-repo-token"
    },
    "SyncInterval": 900
  }
}
```

## Deployment

### As a Windows Service

You can deploy this as a Windows service using the `daemon` mode. Consider using tools like `sc.exe` or third-party service wrappers.

### As a Docker Container

Create a Dockerfile:

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY bin/Release/net8.0/ .
ENTRYPOINT ["dotnet", "GitMirror.dll", "daemon"]
```

### As a Scheduled Task

Use Windows Task Scheduler or cron to run sync operations at regular intervals:

```bash
# Run sync every 5 minutes
dotnet run sync
```

## Logging

The application provides comprehensive logging. Check the console output for:

- Sync operations progress
- Authentication status
- Error messages and troubleshooting information

## Troubleshooting

### Authentication Issues

- Ensure your personal access token has the required permissions
- Check that the token is not expired
- Verify the repository URLs are correct

### Network Issues

- Check firewall settings
- Verify internet connectivity
- Ensure the Git hosting service is accessible

### Repository Issues

- Verify the source repository exists and is accessible
- Check that the target repository exists and you have push permissions
- Ensure the specified branches exist

## Security Notes

- Store personal access tokens securely
- Use environment variables or secure configuration management in production
- Consider using service accounts for automated deployments
- Regularly rotate access tokens

## Dependencies

- .NET 8.0
- LibGit2Sharp for Git operations
- Microsoft.Extensions.* packages for configuration and logging
