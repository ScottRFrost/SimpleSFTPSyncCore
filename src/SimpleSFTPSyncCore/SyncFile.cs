using System;
using System.Collections.Generic;

namespace SimpleSFTPSyncCore
{
    public partial class SyncFile
    {
        public long SyncFileId { get; set; }
        public string RemotePath { get; set; }
        public long Length { get; set; }
        public string RemoteDateModified { get; set; }
        public string DateDiscovered { get; set; }
        public string DateDownloaded { get; set; }
    }
}
