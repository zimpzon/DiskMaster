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
            BtnRun = new Button();
            BtnPause = new Button();
            BtnStop = new Button();
            LabScannerState = new Label();
            LabFoldersInQueue = new Label();
            LabCurrentFolder = new Label();
            LabFoldersFound = new Label();
            RichTextLog = new RichTextBox();
            LabFilesFound = new Label();
            LabSize = new Label();
            SuspendLayout();
            // 
            // TvFolderView
            // 
            TvFolderView.Location = new Point(23, 25);
            TvFolderView.Name = "TvFolderView";
            TvFolderView.Size = new Size(337, 430);
            TvFolderView.TabIndex = 0;
            // 
            // BtnRun
            // 
            BtnRun.Location = new Point(12, 486);
            BtnRun.Name = "BtnRun";
            BtnRun.Size = new Size(112, 34);
            BtnRun.TabIndex = 1;
            BtnRun.Text = "Run";
            BtnRun.UseVisualStyleBackColor = true;
            BtnRun.Click += BtnRun_Click;
            // 
            // BtnPause
            // 
            BtnPause.Location = new Point(130, 486);
            BtnPause.Name = "BtnPause";
            BtnPause.Size = new Size(112, 34);
            BtnPause.TabIndex = 2;
            BtnPause.Text = "Pause";
            BtnPause.UseVisualStyleBackColor = true;
            BtnPause.Click += BtnPause_Click;
            // 
            // BtnStop
            // 
            BtnStop.Location = new Point(248, 486);
            BtnStop.Name = "BtnStop";
            BtnStop.Size = new Size(112, 34);
            BtnStop.TabIndex = 3;
            BtnStop.Text = "Stop";
            BtnStop.UseVisualStyleBackColor = true;
            BtnStop.Click += BtnStop_Click;
            // 
            // LabScannerState
            // 
            LabScannerState.AutoSize = true;
            LabScannerState.Location = new Point(476, 438);
            LabScannerState.Name = "LabScannerState";
            LabScannerState.Size = new Size(176, 25);
            LabScannerState.TabIndex = 4;
            LabScannerState.Text = "Scanner state: (none)";
            // 
            // LabFoldersInQueue
            // 
            LabFoldersInQueue.AutoSize = true;
            LabFoldersInQueue.Location = new Point(476, 394);
            LabFoldersInQueue.Name = "LabFoldersInQueue";
            LabFoldersInQueue.Size = new Size(162, 25);
            LabFoldersInQueue.TabIndex = 5;
            LabFoldersInQueue.Text = "Folders in queue: 0";
            // 
            // LabCurrentFolder
            // 
            LabCurrentFolder.AutoSize = true;
            LabCurrentFolder.Location = new Point(477, 355);
            LabCurrentFolder.Name = "LabCurrentFolder";
            LabCurrentFolder.Size = new Size(181, 25);
            LabCurrentFolder.TabIndex = 6;
            LabCurrentFolder.Text = "Current folder: (none)";
            // 
            // LabFoldersFound
            // 
            LabFoldersFound.AutoSize = true;
            LabFoldersFound.Location = new Point(476, 315);
            LabFoldersFound.Name = "LabFoldersFound";
            LabFoldersFound.Size = new Size(142, 25);
            LabFoldersFound.TabIndex = 7;
            LabFoldersFound.Text = "Folders found: 0";
            // 
            // RichTextLog
            // 
            RichTextLog.Location = new Point(965, 25);
            RichTextLog.Name = "RichTextLog";
            RichTextLog.Size = new Size(109, 66);
            RichTextLog.TabIndex = 8;
            RichTextLog.Text = "";
            // 
            // LabFilesFound
            // 
            LabFilesFound.AutoSize = true;
            LabFilesFound.Location = new Point(476, 280);
            LabFilesFound.Name = "LabFilesFound";
            LabFilesFound.Size = new Size(118, 25);
            LabFilesFound.TabIndex = 9;
            LabFilesFound.Text = "Files found: 0";
            // 
            // LabSize
            // 
            LabSize.AutoSize = true;
            LabSize.Location = new Point(476, 242);
            LabSize.Name = "LabSize";
            LabSize.Size = new Size(73, 25);
            LabSize.TabIndex = 10;
            LabSize.Text = "Size: 0b";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1468, 570);
            Controls.Add(LabSize);
            Controls.Add(LabFilesFound);
            Controls.Add(RichTextLog);
            Controls.Add(LabFoldersFound);
            Controls.Add(LabCurrentFolder);
            Controls.Add(LabFoldersInQueue);
            Controls.Add(LabScannerState);
            Controls.Add(BtnStop);
            Controls.Add(BtnPause);
            Controls.Add(BtnRun);
            Controls.Add(TvFolderView);
            Name = "MainForm";
            Text = "Form1";
            FormClosing += MainForm_FormClosing;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TreeView TvFolderView;
        private Button BtnRun;
        private Button BtnPause;
        private Button BtnStop;
        private Label LabScannerState;
        private Label LabFoldersInQueue;
        private Label LabCurrentFolder;
        private Label LabFoldersFound;
        private RichTextBox RichTextLog;
        private Label LabFilesFound;
        private Label LabSize;
    }
}
