using System.Runtime.InteropServices;
using DiskMasterLib;

namespace MainForm
{
    public partial class MainForm : Form
    {
        // Windows 10 (2004+)/11 native dark title bar - the standard, documented way to get the
        // non-client frame (title bar, min/max/close buttons) to follow a dark theme, since WinForms
        // has no managed API for it. 20 is the modern attribute ID; 19 is the same attribute on
        // older Windows 10 builds before it was renumbered, tried as a fallback.
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        private const uint SWP_FRAMECHANGED_ONLY = 0x0002 | 0x0001 | 0x0004 | 0x0020; // NOMOVE|NOSIZE|NOZORDER|FRAMECHANGED

        private readonly FolderExplorer _folderExplorer;
        private readonly FolderTreeController _folderTreeController;
        private bool _scannerAborted;
        private int _updateModulus;
        private bool _isDarkMode = true;
        private AppTheme _currentTheme = AppTheme.Light;

        public MainForm(FolderExplorerFactory folderExplorerFactory)
        {
            InitializeComponent();

            foreach (var button in new[] { BtnRun, BtnPause, BtnStop, BtnBrowse, BtnTheme })
                button.Paint += Button_PaintOverDisabledText;

            _folderExplorer = folderExplorerFactory.Create(OnScannerRunStateChanged, OnScannerNodeUpdated, OnScannerCompleted);
            _folderTreeController = new FolderTreeController(TvFolderView, _folderExplorer);
            PopulateRootFolderChoices();
            UpdateActionButtonStates(_folderExplorer.RunState);
            UpdateScannerStateLabel(_folderExplorer.RunState);
            ApplyTheme();
        }

        // ---- Button handlers ----

        private void BtnTheme_Click(object sender, EventArgs e)
        {
            _isDarkMode = !_isDarkMode;
            ApplyTheme();
        }

        /// <summary>
        /// Applies the current theme to every control. Buttons default to OS-visual-style rendering
        /// (FlatStyle.Standard + UseVisualStyleBackColor), which ignores BackColor/ForeColor
        /// overrides entirely - switching to Flat with UseVisualStyleBackColor = false is what
        /// actually makes our colors take effect, done unconditionally (not just for dark mode) so
        /// toggling back to light doesn't need to separately restore the original rendering mode.
        /// </summary>
        private void ApplyTheme()
        {
            var theme = _isDarkMode ? AppTheme.Dark : AppTheme.Light;
            _currentTheme = theme;
            BtnTheme.Text = _isDarkMode ? "Light Mode" : "Dark Mode";

            BackColor = theme.FormBackColor;
            ForeColor = theme.FormForeColor;

            PanelButtons.BackColor = theme.FormBackColor;
            PanelButtons.ForeColor = theme.FormForeColor;
            LabFolderPrompt.ForeColor = theme.FormForeColor;

            foreach (var button in new[] { BtnRun, BtnPause, BtnStop, BtnBrowse, BtnTheme })
            {
                button.BackColor = theme.ControlBackColor;
                button.ForeColor = theme.ControlForeColor;
                button.FlatStyle = FlatStyle.Flat;
                button.UseVisualStyleBackColor = false;
                button.FlatAppearance.BorderColor = theme.ControlForeColor;
                button.Invalidate(); // repaint now so a currently-disabled button's text recolors immediately
            }

            CmbRootFolder.BackColor = theme.ControlBackColor;
            CmbRootFolder.ForeColor = theme.ControlForeColor;

            TvFolderView.BackColor = theme.TreeBackColor;
            TvFolderView.ForeColor = theme.TreeForeColor;

            StatusStripMain.BackColor = theme.FormBackColor;
            StatusStripMain.ForeColor = theme.FormForeColor;
            foreach (ToolStripItem item in StatusStripMain.Items)
                item.ForeColor = theme.FormForeColor;

            _folderTreeController.SetTheme(theme);
            ApplyTitleBarTheme(_isDarkMode);
        }

        /// <summary>
        /// A disabled Button's built-in text rendering derives its embossed shadow/highlight colors
        /// from BackColor via ControlPaint.Dark/Light, completely ignoring ForeColor - on our dark
        /// theme's near-black BackColor, Dark(BackColor) comes out nearly indistinguishable from
        /// BackColor itself (verified: Dark(45,45,48) = (15,15,17)), so disabled text is effectively
        /// invisible no matter what ForeColor is set to. There's no built-in way to override just
        /// that, so repaint over it ourselves with DisabledForeColor once the base rendering is done.
        /// </summary>
        private void Button_PaintOverDisabledText(object? sender, PaintEventArgs e)
        {
            if (sender is not Button button || button.Enabled)
                return;

            using var backBrush = new SolidBrush(button.BackColor);
            e.Graphics.FillRectangle(backBrush, Rectangle.Inflate(button.ClientRectangle, -2, -2));
            TextRenderer.DrawText(e.Graphics, button.Text, button.Font, button.ClientRectangle, _currentTheme.DisabledForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void ApplyTitleBarTheme(bool dark)
        {
            if (!IsHandleCreated)
            {
                HandleCreated += (_, _) => ApplyTitleBarTheme(dark);
                return;
            }

            var useDark = dark ? 1 : 0;
            if (DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int)) != 0)
                DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref useDark, sizeof(int));

            // DWM doesn't always repaint the frame on its own after the attribute changes while the
            // window is already visible - force it to.
            SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_FRAMECHANGED_ONLY);
        }

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
                SslFoldersInQueue.Text = $"Folders in queue: {FormatCount(_folderExplorer.FoldersInQueue)}";
                SslCurrentFolder.Text = $"Current folder: {TruncatePathForStatusBar(_folderExplorer.CurrentlyScanningNode?.FolderName ?? "-")}";
                SslFoldersFound.Text = $"Folders found: {FormatCount(_folderExplorer.TotalFoldersFound)}";
                SslFilesFound.Text = $"Files found: {FormatCount(_folderExplorer.TotalFilesFound)}";
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

            // The Math.Round guards (rather than plain "< Mb" etc.) matter: e.g. 1048575 bytes is
            // technically < Mb, but 1048575/Kb rounds to 1024.00 at 2 decimals, which would
            // otherwise display as the nonsensical "1024.00 KB" instead of rolling over to "1.00 MB".
            return bytes switch
            {
                _ when bytes < (long)Kb => $"{bytes} bytes",
                _ when Math.Round(bytes / Kb, 2) < 1024 => $"{bytes / Kb:F2} KB",
                _ when Math.Round(bytes / Mb, 2) < 1024 => $"{bytes / Mb:F2} MB",
                _ when Math.Round(bytes / Gb, 2) < 1024 => $"{bytes / Gb:F2} GB",
                _ => $"{bytes / Tb:F2} TB"
            };
        }

        /// <summary>
        /// Same idea as FormatBytes - a bare count is hard to read once a scan reaches into the
        /// hundreds of thousands, so scale it the same way, just with decimal (1000-based) steps
        /// instead of binary ones, since these are counts, not byte multiples.
        /// </summary>
        internal static string FormatCount(long count)
        {
            const double K = 1000;
            const double M = K * 1000;
            const double B = M * 1000;

            // See the Math.Round guards in FormatBytes - same rollover-at-the-boundary fix applies
            // here, e.g. 999999 is < M but rounds to "1000.00 K" at 2 decimals without this.
            return count switch
            {
                _ when count < (long)K => $"{count}",
                _ when Math.Round(count / K, 2) < 1000 => $"{count / K:F2} K",
                _ when Math.Round(count / M, 2) < 1000 => $"{count / M:F2} M",
                _ => $"{count / B:F2} B"
            };
        }
    }
}
