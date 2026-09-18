using DiskMasterLib;

namespace MainForm
{
    public partial class MainForm : Form
    {
        private readonly FolderExplorer _folderExplorer;
        private bool _scannerAborted;

        public MainForm(FolderExplorerFactory folderExplorerFactory)
        {
            InitializeComponent();

            _folderExplorer = folderExplorerFactory.Create(OnScannerRunStateChanged, OnScannerNodeUpdated);
            UpdateActionButtonStates(_folderExplorer.RunState);
            UpdateScannerStateLabel(_folderExplorer.RunState);
        }

        private void OnScannerRunStateChanged(RunState runState)
        {
            Invoke(() =>
            {
                if (runState == RunState.Aborted)
                {
                    _scannerAborted = true;
                    Close();
                    return;
                }

                UpdateScannerStateLabel(runState);
                UpdateActionButtonStates(runState);
            });
        }

        private void UpdateScannerStateLabel(RunState runState)
        {
            LabScannerState.Text = $"Scanner state: {runState}";
        }

        int updateModulus;
        private void OnScannerNodeUpdated(IScanningNode node)
        {
            if (updateModulus++ % 100 != 0)
                return;

            Invoke(() => {
                LabFoldersInQueue.Text = $"Folders in queue: {_folderExplorer.FoldersInQueue}";
                LabCurrentFolder.Text = $"Current folder: {node.FolderName}";
                LabFoldersProcessed.Text = $"Folders processed: {_folderExplorer.TotalFoldersFound}";
            });
        }

        private void UpdateActionButtonStates(RunState runState)
        {
            if (runState == RunState.StopPending || runState == RunState.PausePending)
            {
                BtnRun.Enabled = false;
                BtnPause.Enabled = false;
                BtnStop.Enabled = false;
                return;
            }

            BtnRun.Enabled =
                runState == RunState.WaitingForRun ||
                runState == RunState.Paused ||
                runState == RunState.Stopped ||
                runState == RunState.Completed;

            BtnPause.Enabled = runState == RunState.Running;

            BtnStop.Enabled = runState == RunState.Running ||
                runState == RunState.Paused;
        }

        private async void BtnRun_Click(object sender, EventArgs e)
        {
            //_folderExplorer.Run(@"C:\mnt");
            _folderExplorer.Run(@"C:\");
        }

        private void BtnPause_Click(object sender, EventArgs e)
        {
            _folderExplorer.Pause();
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            _folderExplorer.Stop();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_scannerAborted)
                return;

            _folderExplorer.Dispose();
            e.Cancel = true;
        }
    }
}
