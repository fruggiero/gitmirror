namespace GitMirror.Configuration;

public class GitMirrorConfig
{
    public RepositoryConfig SourceRepository { get; set; } = new();
    public RepositoryConfig TargetRepository { get; set; } = new();
    public string LocalPath { get; set; } = "./temp_repo";
    public int SyncInterval { get; set; } = 300; // seconds
}

public class RepositoryConfig
{
    public string Url { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
    public string Username { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}
