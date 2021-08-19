namespace SimpleSFTPSyncCore
{
#pragma warning disable IDE1006 // Naming Styles
    public class ConfigFile
    {
        public string hostname { get; set; }
        public int port { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public string fingerprint { get; set; }
        public string[] remoteDir { get; set; }
        public string downloadDir { get; set; }
        public string movieDir { get; set; }
        public string tvDir { get; set; }
        public string unrar { get; set; }
        public string tmdbKey { get; set; }
        public int lftp { get; set; }
    }
#pragma warning restore IDE1006 // Naming Styles
}
