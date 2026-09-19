using DiskMasterLib;
using System.Diagnostics;
using System.Xml.Linq;

namespace MainForm
{
    public partial class MainForm : Form
    {
        private readonly FolderExplorer _folderExplorer;
        private bool _scannerAborted;

        public MainForm(FolderExplorerFactory folderExplorerFactory)
        {
            InitializeComponent();

            _folderExplorer = folderExplorerFactory.Create(OnScannerRunStateChanged, OnScannerNodeUpdated, OnScannerCompleted);
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


        private void OnScannerCompleted()
        {
            UpdateScannerStats();
        }

        private void UpdateScannerStats()
        {
            Invoke(() => {
                LabFoldersInQueue.Text = $"Folders in queue: {_folderExplorer.FoldersInQueue}";
                LabCurrentFolder.Text = $"Current folder: -";
                LabFoldersFound.Text = $"Folders found: {_folderExplorer.TotalFoldersFound}";
                LabFilesFound.Text = $"Files found: {_folderExplorer.TotalFilesFound}";
                LabSize.Text = $"Size: {FormatBytes(_folderExplorer.TotalBytesFound)}";
            });
        }

        int updateModulus;
        private void OnScannerNodeUpdated(IScanningNode node)
        {
            if (updateModulus++ % 1000 != 0)
                return;

            UpdateScannerStats();
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
            //_folderExplorer.Run(@"C:\Users\peter\source\repos\PythonChat\venv");
            _folderExplorer.Run(@"C:\everquestlegends");
        }

        private static string FormatBytes(long bytes)
        {
            const double Kb = 1024;
            const double Mb = Kb * 1024;
            const double Gb = Mb * 1024;
            const double Tb = Gb * 1024;

            return bytes switch
            {
                < (long)Kb => $"{bytes} bytes",
                < (long)Mb => $"{bytes / Kb:F2} KB",
                < (long)Gb => $"{bytes / Mb:F2} MB",
                < (long)Tb => $"{bytes / Gb:F2} GB",
                _ => $"{bytes / Tb:F2} TB"
            };
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
