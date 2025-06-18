# GitMirror Project Structure

```
GitMirror/
├── Configuration/
│   └── GitMirrorConfig.cs         # Configuration models
├── Services/
│   └── GitService.cs              # Core Git operations service
├── .env.example                   # Environment variables template
├── .gitignore                     # Git ignore file
├── appsettings.json              # Default configuration
├── GitMirror.csproj              # Project file
├── GitMirror.ps1                 # PowerShell helper script
├── Program.cs                    # Main application entry point
└── README.md                     # Comprehensive documentation
```

## Quick Start

1. **Configure your repositories** in `appsettings.json` or use environment variables
2. **Build the project**: `dotnet build`
3. **Run a sync operation**: `dotnet run sync`
4. **Run in daemon mode**: `dotnet run daemon`

## Key Features

- ✅ Mirror operations (full repository copy)
- ✅ Sync operations (incremental updates)
- ✅ Daemon mode (continuous sync)
- ✅ Authentication support (tokens, credentials)
- ✅ Comprehensive logging
- ✅ PowerShell helper script
- ✅ Environment variable configuration
- ✅ Error handling and cleanup

## Files Overview

### Core Application Files

- **Program.cs**: Main entry point with command-line argument parsing and hosting setup
- **GitService.cs**: Core service containing all Git operations (clone, fetch, push)
- **GitMirrorConfig.cs**: Configuration models for repository settings

### Configuration Files

- **appsettings.json**: Default JSON configuration
- **.env.example**: Template for environment variables
- **GitMirror.ps1**: PowerShell helper script for common operations

### Documentation

- **README.md**: Complete usage documentation with examples
- **PROJECT_STRUCTURE.md**: This file

## Usage Examples

### Basic Sync
```bash
dotnet run sync
```

### Mirror Operation
```bash
dotnet run mirror
```

### Daemon Mode
```bash
dotnet run daemon
```

### Using PowerShell Helper
```powershell
.\GitMirror.ps1 sync -SourceUrl "https://github.com/source/repo.git" -TargetUrl "https://github.com/target/repo.git"
```

The application is production-ready and includes proper error handling, logging, and authentication support for secure Git operations.
