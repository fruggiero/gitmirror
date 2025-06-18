# GitMirror PowerShell Helper Script
# This script provides convenient commands for running GitMirror operations

param(
    [Parameter(Position=0)]
    [ValidateSet("mirror", "sync", "daemon", "build", "help")]
    [string]$Command = "help",
    
    [Parameter()]
    [string]$SourceUrl,
    
    [Parameter()]
    [string]$TargetUrl,
    
    [Parameter()]
    [string]$SourceToken,
    
    [Parameter()]
    [string]$TargetToken,
    
    [Parameter()]
    [string]$Branch = "main"
)

function Show-Help {
    Write-Host "GitMirror PowerShell Helper" -ForegroundColor Green
    Write-Host ""
    Write-Host "Usage: .\GitMirror.ps1 [Command] [Options]" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Commands:" -ForegroundColor Cyan
    Write-Host "  build      - Build the GitMirror application"
    Write-Host "  mirror     - Perform a one-time mirror operation"
    Write-Host "  sync       - Perform a one-time sync operation"
    Write-Host "  daemon     - Run in daemon mode (continuous sync)"
    Write-Host "  help       - Show this help message"
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Cyan
    Write-Host "  -SourceUrl    Source repository URL"
    Write-Host "  -TargetUrl    Target repository URL"
    Write-Host "  -SourceToken  Source repository access token"
    Write-Host "  -TargetToken  Target repository access token"
    Write-Host "  -Branch       Branch to sync (default: main)"
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Magenta
    Write-Host "  .\GitMirror.ps1 build"
    Write-Host "  .\GitMirror.ps1 sync"
    Write-Host "  .\GitMirror.ps1 mirror -SourceUrl 'https://github.com/source/repo.git' -TargetUrl 'https://github.com/target/repo.git'"
    Write-Host "  .\GitMirror.ps1 daemon"
    Write-Host ""
}

function Set-EnvironmentVariables {
    if ($SourceUrl) {
        $env:GitMirror__SourceRepository__Url = $SourceUrl
        Write-Host "Set source URL: $SourceUrl" -ForegroundColor Green
    }
    
    if ($TargetUrl) {
        $env:GitMirror__TargetRepository__Url = $TargetUrl
        Write-Host "Set target URL: $TargetUrl" -ForegroundColor Green
    }
    
    if ($SourceToken) {
        $env:GitMirror__SourceRepository__Token = $SourceToken
        Write-Host "Set source token (hidden)" -ForegroundColor Green
    }
    
    if ($TargetToken) {
        $env:GitMirror__TargetRepository__Token = $TargetToken
        Write-Host "Set target token (hidden)" -ForegroundColor Green
    }
    
    if ($Branch -ne "main") {
        $env:GitMirror__SourceRepository__Branch = $Branch
        $env:GitMirror__TargetRepository__Branch = $Branch
        Write-Host "Set branch: $Branch" -ForegroundColor Green
    }
}

function Invoke-GitMirror {
    param([string]$Operation)
    
    Write-Host "Executing GitMirror: $Operation" -ForegroundColor Yellow
    
    if ($Operation -eq "build") {
        dotnet build
    } else {
        dotnet run $Operation
    }
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "GitMirror operation completed successfully!" -ForegroundColor Green
    } else {
        Write-Host "GitMirror operation failed with exit code: $LASTEXITCODE" -ForegroundColor Red
    }
}

# Main script execution
switch ($Command.ToLower()) {
    "help" {
        Show-Help
    }
    "build" {
        Write-Host "Building GitMirror..." -ForegroundColor Yellow
        Invoke-GitMirror "build"
    }
    "mirror" {
        Set-EnvironmentVariables
        Invoke-GitMirror "mirror"
    }
    "sync" {
        Set-EnvironmentVariables
        Invoke-GitMirror "sync"
    }
    "daemon" {
        Set-EnvironmentVariables
        Write-Host "Starting GitMirror daemon... Press Ctrl+C to stop." -ForegroundColor Yellow
        Invoke-GitMirror "daemon"
    }
    default {
        Write-Host "Unknown command: $Command" -ForegroundColor Red
        Show-Help
    }
}
