namespace DiskMasterLib
{
    public class FolderExplorerFactory
    {
        public FolderExplorer Create(Action<RunState> onRunStateChanged, Action<IScanningNode> onNodeUpdated, Action onScannerCompleted)
            => new FolderExplorer(onRunStateChanged, onNodeUpdated, onScannerCompleted);
    }
}
