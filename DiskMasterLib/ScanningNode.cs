namespace DiskMasterLib
{
    public interface IScanningNode
    {
        string FolderName { get; }
        long FolderSizeBytes { get; }
    }

    internal class ScanningNode : IScanningNode
    {
        public static readonly ScanningNode Empty = new();

        public ScanningNode? Parent { get; set; }
        public List<ScanningNode> Children { get; set; } = [];
        public string FolderName { get; set; } = string.Empty;
        public long FolderSizeBytes { get; set; }
        public bool CannotAccess { get; set; }
    }
}
