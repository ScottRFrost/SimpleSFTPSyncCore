namespace SimpleSFTPSyncCore;

#pragma warning disable IDE1006 // Naming Styles
public class ConfigFile
{
    public string hostname { get; set; } = string.Empty;
    public int port { get; set; }
    public string username { get; set; } = string.Empty;
    public string password { get; set; } = string.Empty;
    public string fingerprint { get; set; } = string.Empty;
    public string[] remoteDir { get; set; } = [];
    public string downloadDir { get; set; } = string.Empty;
    public string movieDir { get; set; } = string.Empty;
    public string tvDir { get; set; } = string.Empty;
    public string unrar { get; set; } = string.Empty;
    public string tmdbKey { get; set; } = string.Empty;
    public int lftp { get; set; }
}
#pragma warning restore IDE1006 // Naming Styles
