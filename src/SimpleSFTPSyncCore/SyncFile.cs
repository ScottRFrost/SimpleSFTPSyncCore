namespace SimpleSFTPSyncCore;

public partial class SyncFile
{
	public long SyncFileId { get; set; }
	public string RemotePath { get; set; } = string.Empty;
	public long Length { get; set; }
	public string RemoteDateModified { get; set; } = string.Empty;
	public string DateDiscovered { get; set; } = string.Empty;
	public string? DateDownloaded { get; set; }
}
