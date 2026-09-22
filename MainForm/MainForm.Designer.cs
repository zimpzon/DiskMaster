namespace MainForm
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            TvFolderView = new TreeView();
            PanelButtons = new FlowLayoutPanel();
            LabFolderPrompt = new Label();
            CmbRootFolder = new ComboBox();
            BtnBrowse = new Button();
            BtnRun = new Button();
            BtnPause = new Button();
            BtnStop = new Button();
            BtnTheme = new Button();
            StatusStripMain = new StatusStrip();
            SslScannerState = new ToolStripStatusLabel();
            SslCurrentFolder = new ToolStripStatusLabel();
            SslFoldersInQueue = new ToolStripStatusLabel();
            SslFoldersFound = new ToolStripStatusLabel();
            SslFilesFound = new ToolStripStatusLabel();
            SslSize = new ToolStripStatusLabel();
            PanelButtons.SuspendLayout();
            StatusStripMain.SuspendLayout();
            SuspendLayout();
            //
            // TvFolderView
            //
            TvFolderView.Dock = DockStyle.Fill;
            TvFolderView.Location = new Point(0, 58);
            TvFolderView.Name = "TvFolderView";
            TvFolderView.Size = new Size(1468, 605);
            TvFolderView.TabIndex = 6;
            //
            // PanelButtons
            //
            PanelButtons.AutoSize = true;
            PanelButtons.Controls.Add(LabFolderPrompt);
            PanelButtons.Controls.Add(CmbRootFolder);
            PanelButtons.Controls.Add(BtnBrowse);
            PanelButtons.Controls.Add(BtnRun);
            PanelButtons.Controls.Add(BtnPause);
            PanelButtons.Controls.Add(BtnStop);
            PanelButtons.Controls.Add(BtnTheme);
            PanelButtons.Dock = DockStyle.Top;
            PanelButtons.Location = new Point(0, 0);
            PanelButtons.Name = "PanelButtons";
            PanelButtons.Padding = new Padding(12);
            PanelButtons.Size = new Size(1468, 58);
            PanelButtons.TabIndex = 0;
            //
            // LabFolderPrompt
            //
            LabFolderPrompt.AutoSize = true;
            LabFolderPrompt.Location = new Point(15, 22);
            LabFolderPrompt.Margin = new Padding(3, 10, 3, 3);
            LabFolderPrompt.Name = "LabFolderPrompt";
            LabFolderPrompt.Size = new Size(58, 25);
            LabFolderPrompt.TabIndex = 0;
            LabFolderPrompt.Text = "Folder:";
            //
            // CmbRootFolder
            //
            CmbRootFolder.Location = new Point(79, 15);
            CmbRootFolder.Margin = new Padding(3, 3, 10, 3);
            CmbRootFolder.Name = "CmbRootFolder";
            CmbRootFolder.Size = new Size(380, 33);
            CmbRootFolder.TabIndex = 1;
            //
            // BtnBrowse
            //
            BtnBrowse.Location = new Point(472, 15);
            BtnBrowse.Margin = new Padding(3, 3, 20, 3);
            BtnBrowse.Name = "BtnBrowse";
            BtnBrowse.Size = new Size(90, 34);
            BtnBrowse.TabIndex = 2;
            BtnBrowse.Text = "Browse...";
            BtnBrowse.UseVisualStyleBackColor = true;
            BtnBrowse.Click += BtnBrowse_Click;
            //
            // BtnRun
            //
            BtnRun.Location = new Point(605, 15);
            BtnRun.Margin = new Padding(3, 3, 10, 3);
            BtnRun.Name = "BtnRun";
            BtnRun.Size = new Size(112, 34);
            BtnRun.TabIndex = 3;
            BtnRun.Text = "Run";
            BtnRun.UseVisualStyleBackColor = true;
            BtnRun.Click += BtnRun_Click;
            //
            // BtnPause
            //
            BtnPause.Location = new Point(727, 15);
            BtnPause.Margin = new Padding(3, 3, 10, 3);
            BtnPause.Name = "BtnPause";
            BtnPause.Size = new Size(112, 34);
            BtnPause.TabIndex = 4;
            BtnPause.Text = "Pause";
            BtnPause.UseVisualStyleBackColor = true;
            BtnPause.Click += BtnPause_Click;
            //
            // BtnStop
            //
            BtnStop.Location = new Point(849, 15);
            BtnStop.Margin = new Padding(3, 3, 10, 3);
            BtnStop.Name = "BtnStop";
            BtnStop.Size = new Size(112, 34);
            BtnStop.TabIndex = 5;
            BtnStop.Text = "Stop";
            BtnStop.UseVisualStyleBackColor = true;
            BtnStop.Click += BtnStop_Click;
            //
            // BtnTheme
            //
            BtnTheme.Location = new Point(971, 15);
            BtnTheme.Margin = new Padding(20, 3, 3, 3);
            BtnTheme.Name = "BtnTheme";
            BtnTheme.Size = new Size(112, 34);
            BtnTheme.TabIndex = 6;
            BtnTheme.Text = "Dark Mode";
            BtnTheme.UseVisualStyleBackColor = true;
            BtnTheme.Click += BtnTheme_Click;
            //
            // StatusStripMain
            //
            StatusStripMain.Items.AddRange(new ToolStripItem[] { SslScannerState, SslFoldersInQueue, SslFoldersFound, SslFilesFound, SslSize, SslCurrentFolder });
            StatusStripMain.Location = new Point(0, 663);
            StatusStripMain.Name = "StatusStripMain";
            StatusStripMain.Size = new Size(1468, 32);
            StatusStripMain.TabIndex = 7;
            //
            // SslScannerState
            //
            SslScannerState.BorderSides = ToolStripStatusLabelBorderSides.Right;
            SslScannerState.BorderStyle = Border3DStyle.Etched;
            SslScannerState.Overflow = ToolStripItemOverflow.Never;
            SslScannerState.Name = "SslScannerState";
            SslScannerState.Padding = new Padding(4, 0, 10, 0);
            SslScannerState.Size = new Size(115, 27);
            SslScannerState.Text = "Scanner state: -";
            //
            // SslCurrentFolder
            //
            SslCurrentFolder.Overflow = ToolStripItemOverflow.Never;
            SslCurrentFolder.Spring = true;
            SslCurrentFolder.TextAlign = ContentAlignment.MiddleLeft;
            SslCurrentFolder.Name = "SslCurrentFolder";
            SslCurrentFolder.Padding = new Padding(4, 0, 0, 0);
            SslCurrentFolder.Size = new Size(115, 27);
            SslCurrentFolder.Text = "Current folder: -";
            //
            // SslFoldersInQueue
            //
            SslFoldersInQueue.BorderSides = ToolStripStatusLabelBorderSides.Right;
            SslFoldersInQueue.BorderStyle = Border3DStyle.Etched;
            SslFoldersInQueue.Overflow = ToolStripItemOverflow.Never;
            SslFoldersInQueue.Name = "SslFoldersInQueue";
            SslFoldersInQueue.Padding = new Padding(4, 0, 10, 0);
            SslFoldersInQueue.Size = new Size(115, 27);
            SslFoldersInQueue.Text = "Folders in queue: 0";
            //
            // SslFoldersFound
            //
            SslFoldersFound.BorderSides = ToolStripStatusLabelBorderSides.Right;
            SslFoldersFound.BorderStyle = Border3DStyle.Etched;
            SslFoldersFound.Overflow = ToolStripItemOverflow.Never;
            SslFoldersFound.Name = "SslFoldersFound";
            SslFoldersFound.Padding = new Padding(4, 0, 10, 0);
            SslFoldersFound.Size = new Size(115, 27);
            SslFoldersFound.Text = "Folders found: 0";
            //
            // SslFilesFound
            //
            SslFilesFound.BorderSides = ToolStripStatusLabelBorderSides.Right;
            SslFilesFound.BorderStyle = Border3DStyle.Etched;
            SslFilesFound.Overflow = ToolStripItemOverflow.Never;
            SslFilesFound.Name = "SslFilesFound";
            SslFilesFound.Padding = new Padding(4, 0, 10, 0);
            SslFilesFound.Size = new Size(115, 27);
            SslFilesFound.Text = "Files found: 0";
            //
            // SslSize
            //
            SslSize.BorderSides = ToolStripStatusLabelBorderSides.Right;
            SslSize.BorderStyle = Border3DStyle.Etched;
            SslSize.Overflow = ToolStripItemOverflow.Never;
            SslSize.Name = "SslSize";
            SslSize.Padding = new Padding(4, 0, 10, 0);
            SslSize.Size = new Size(115, 27);
            SslSize.Text = "Size: 0 bytes";
            //
            // MainForm
            //
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1468, 695);
            Controls.Add(TvFolderView);
            Controls.Add(PanelButtons);
            Controls.Add(StatusStripMain);
            MinimumSize = new Size(650, 420);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "DiskMaster";
            FormClosing += MainForm_FormClosing;
            PanelButtons.ResumeLayout(false);
            StatusStripMain.ResumeLayout(false);
            StatusStripMain.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TreeView TvFolderView;
        private FlowLayoutPanel PanelButtons;
        private Label LabFolderPrompt;
        private ComboBox CmbRootFolder;
        private Button BtnBrowse;
        private Button BtnRun;
        private Button BtnPause;
        private Button BtnStop;
        private Button BtnTheme;
        private StatusStrip StatusStripMain;
        private ToolStripStatusLabel SslScannerState;
        private ToolStripStatusLabel SslFoldersInQueue;
        private ToolStripStatusLabel SslFoldersFound;
        private ToolStripStatusLabel SslFilesFound;
        private ToolStripStatusLabel SslSize;
        private ToolStripStatusLabel SslCurrentFolder;
    }
}
