namespace DiskMasterLib
{
    public class FolderExplorerFactory
    {
        public FolderExplorer Create(
            Action<RunState> onRunStateChanged,
            Action<IScanningNode> onNodeUpdated,
            Action onScannerCompleted)
        => new (onRunStateChanged, onNodeUpdated, onScannerCompleted);
    }
}
