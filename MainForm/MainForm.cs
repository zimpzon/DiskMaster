using DiskMasterLib;

namespace MainForm
{
    public partial class MainForm : Form
    {
        private readonly FolderExplorer _folderExplorer;

        public MainForm(FolderExplorer folderExplorer)
        {
            ArgumentNullException.ThrowIfNull(folderExplorer, nameof(folderExplorer));
            _folderExplorer = folderExplorer;

            InitializeComponent();

            SetActionButtonStates();
        }

        private void SetActionButtonStates()
        {
            BtnRun.Enabled = _folderExplorer.RunState == FolderExplorerRunState.Stopped || _folderExplorer.RunState == FolderExplorerRunState.Paused;
            BtnPause.Enabled = _folderExplorer.RunState == FolderExplorerRunState.Running;
            BtnStop.Enabled = _folderExplorer.RunState == FolderExplorerRunState.Running || _folderExplorer.RunState == FolderExplorerRunState.Paused;
        }
    }
}
