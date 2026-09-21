namespace DiskMasterLib
{
    internal class ScannerStats
    {
        public long TotalFoldersFound { get; set; }
        public long TotalFilesFound { get; set; }
        public long TotalBytesFound { get; set; }
        public long SkippedFolderCount { get; set; }
    }
}
