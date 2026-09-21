using DiskMasterLib;

namespace MainForm
{
    public partial class MainForm : Form
    {
        private readonly FolderExplorer _folderExplorer;
        private readonly FolderTreeController _folderTreeController;
        private bool _scannerAborted;
        private int _updateModulus;

        public MainForm(FolderExplorerFactory folderExplorerFactory)
        {
            InitializeComponent();

            _folderExplorer = folderExplorerFactory.Create(OnScannerRunStateChanged, OnScannerNodeUpdated, OnScannerCompleted);
            _folderTreeController = new FolderTreeController(TvFolderView, _folderExplorer);
            PopulateRootFolderChoices();
            UpdateActionButtonStates(_folderExplorer.RunState);
            UpdateScannerStateLabel(_folderExplorer.RunState);
        }

        // ---- Button handlers ----

        private void PopulateRootFolderChoices()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                    CmbRootFolder.Items.Add(drive.RootDirectory.FullName);
            }

            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
            CmbRootFolder.Text = systemDrive is not null && CmbRootFolder.Items.Contains(systemDrive)
                ? systemDrive
                : CmbRootFolder.Items.Cast<string>().FirstOrDefault() ?? "";
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select a folder to scan",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false,
                SelectedPath = Directory.Exists(CmbRootFolder.Text) ? CmbRootFolder.Text : "",
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
                CmbRootFolder.Text = dialog.SelectedPath;
        }

        private void BtnRun_Click(object sender, EventArgs e)
        {
            var path = CmbRootFolder.Text.Trim();
            if (!Directory.Exists(path))
            {
                MessageBox.Show(this, $"\"{path}\" is not a valid, accessible folder.", "DiskMaster", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _folderExplorer.Run(path);
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

            _folderTreeController.Dispose();
            _folderExplorer.Dispose();
            e.Cancel = true;
        }

        // ---- FolderExplorer callbacks (may arrive on the scanner's background thread) ----

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

        private void OnScannerCompleted()
        {
            UpdateScannerStats();
        }

        private void OnScannerNodeUpdated(IScanningNode node)
        {
            // Refreshing the UI on every folder is too slow, so only do it every 1000th update.
            if (_updateModulus++ % 1000 != 0)
                return;

            UpdateScannerStats();
        }

        // ---- UI updates ----

        private void UpdateScannerStateLabel(RunState runState)
        {
            SslScannerState.Text = $"Scanner state: {runState}";
        }

        private void UpdateScannerStats()
        {
            Invoke(() => {
                SslFoldersInQueue.Text = $"Folders in queue: {_folderExplorer.FoldersInQueue}";
                SslCurrentFolder.Text = $"Current folder: {TruncatePathForStatusBar(_folderExplorer.CurrentlyScanningNode?.FolderName ?? "-")}";
                SslFoldersFound.Text = $"Folders found: {_folderExplorer.TotalFoldersFound}";
                SslFilesFound.Text = $"Files found: {_folderExplorer.TotalFilesFound}";
                SslSize.Text = $"Size: {FormatBytes(_folderExplorer.TotalBytesFound)}";
            });
        }

        private void UpdateActionButtonStates(RunState runState)
        {
            if (runState == RunState.StopPending || runState == RunState.PausePending)
            {
                BtnRun.Enabled = false;
                BtnPause.Enabled = false;
                BtnStop.Enabled = false;
                CmbRootFolder.Enabled = false;
                BtnBrowse.Enabled = false;
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

            // Excludes Paused: clicking Run while paused resumes the existing scan and ignores
            // the folder selection entirely, so changing it then would be misleading.
            CmbRootFolder.Enabled =
                runState == RunState.WaitingForRun ||
                runState == RunState.Stopped ||
                runState == RunState.Completed;
            BtnBrowse.Enabled = CmbRootFolder.Enabled;
        }

        /// <summary>
        /// StatusStrip doesn't wrap or shrink items to fit - an item whose natural width exceeds
        /// the space available just renders past the strip's edge instead of truncating, so a deep
        /// path would otherwise push itself (and push nothing else, but render itself invisibly)
        /// off-screen. Keeping the tail of the path (the most specific, most useful part) rather
        /// than the start.
        /// </summary>
        private static string TruncatePathForStatusBar(string path)
        {
            const int MaxLength = 60;
            return path.Length <= MaxLength ? path : "..." + path[^(MaxLength - 3)..];
        }

        internal static string FormatBytes(long bytes)
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
    }
}
