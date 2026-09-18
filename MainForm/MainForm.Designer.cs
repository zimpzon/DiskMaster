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
            LabFoldersProcessed = new Label();
            RichTextLog = new RichTextBox();
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
            // LabFoldersProcessed
            // 
            LabFoldersProcessed.AutoSize = true;
            LabFoldersProcessed.Location = new Point(476, 315);
            LabFoldersProcessed.Name = "LabFoldersProcessed";
            LabFoldersProcessed.Size = new Size(175, 25);
            LabFoldersProcessed.TabIndex = 7;
            LabFoldersProcessed.Text = "Folders processed: 0";
            // 
            // RichTextLog
            // 
            RichTextLog.Location = new Point(947, 117);
            RichTextLog.Name = "RichTextLog";
            RichTextLog.Size = new Size(370, 338);
            RichTextLog.TabIndex = 8;
            RichTextLog.Text = "";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1468, 570);
            Controls.Add(RichTextLog);
            Controls.Add(LabFoldersProcessed);
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
        private Label LabFoldersProcessed;
        private RichTextBox RichTextLog;
    }
}
