using System.ComponentModel;

namespace DiskMasterLib
{
    public interface IScanningNode
    {
        string FolderName { get; }
        long FolderSizeBytes { get; }
    }

    internal class ScanningNode : IScanningNode
    {
        public ScanningNode Parent { get; set; }
        public List<ScanningNode> Children { get; set; }
        public string FolderName { get; set; }
        public long FolderSizeBytes { get; set; }
    }
}
