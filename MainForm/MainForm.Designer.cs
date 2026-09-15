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
            // 
            // BtnPause
            // 
            BtnPause.Location = new Point(130, 486);
            BtnPause.Name = "BtnPause";
            BtnPause.Size = new Size(112, 34);
            BtnPause.TabIndex = 2;
            BtnPause.Text = "Pause";
            BtnPause.UseVisualStyleBackColor = true;
            // 
            // BtnStop
            // 
            BtnStop.Location = new Point(248, 486);
            BtnStop.Name = "BtnStop";
            BtnStop.Size = new Size(112, 34);
            BtnStop.TabIndex = 3;
            BtnStop.Text = "Stop";
            BtnStop.UseVisualStyleBackColor = true;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(912, 544);
            Controls.Add(BtnStop);
            Controls.Add(BtnPause);
            Controls.Add(BtnRun);
            Controls.Add(TvFolderView);
            Name = "MainForm";
            Text = "Form1";
            ResumeLayout(false);
        }

        #endregion

        private TreeView TvFolderView;
        private Button BtnRun;
        private Button BtnPause;
        private Button BtnStop;
    }
}
