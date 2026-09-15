namespace DiskMasterLib
{
    public class FolderExplorer
    {
        public FolderExplorerRunState RunState => _runState;

        private FolderExplorerRunState _runState = FolderExplorerRunState.Stopped;

        public async Task Run()
        {
            if (_runState == FolderExplorerRunState.Running)
                throw new InvalidOperationException($"Already running");
        }

        public async Task Pause()
        {
            if (_runState != FolderExplorerRunState.Running)
                throw new InvalidOperationException($"Must be running to pause");
        }

        public async Task Stop()
        {
            if (_runState != FolderExplorerRunState.Running)
                throw new InvalidOperationException($"Must be running to stop");
        }
    }
}
