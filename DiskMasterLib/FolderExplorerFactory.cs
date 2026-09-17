namespace DiskMasterLib
{
    public class FolderExplorerFactory
    {
        public FolderExplorer Create(Action<RunState> onRunStateChanged, Action<IScanningNode> onNodeUpdated)
            => new FolderExplorer(onRunStateChanged, onNodeUpdated);
    }
}
