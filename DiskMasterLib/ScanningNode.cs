namespace DiskMasterLib
{
    public interface IScanningNode
    {
        string FolderName { get; }
        long FileBytes { get; }
    }

    internal class ScanningNode : IScanningNode
    {
        public static readonly ScanningNode Empty = new();

        static ScanningNode()
        {
            Empty.Parent = Empty;
        }

        public ScanningNode Parent { get; set; } = Empty;
        public List<ScanningNode> Children { get; set; } = [];
        public string FolderName { get; set; } = string.Empty;
        public long FileBytes { get; set; }
        public Exception? ScanError { get; set; }
    }
}
