using DiskMasterLib;

namespace MainForm
{
    /// <summary>
    /// Drives a virtual, live-updating view of the scan tree on a TreeView: only ever materializes
    /// currently-visible TreeNodes (a dummy placeholder child gives a node an expand arrow without
    /// materializing its real children), and tears them back down on collapse.
    ///
    /// Deliberately doesn't hook FolderExplorer's background-thread callbacks - everything here runs
    /// off a UI-thread-only Timer instead, avoiding any cross-thread Invoke marshaling. That's cheap
    /// because the number of visible nodes is exactly what virtualization keeps small.
    ///
    /// Each node's label is drawn in two colored segments (folder name, then status/size) using an
    /// owner-drawn TreeView, since a plain TreeNode can only have one color for its whole text.
    /// </summary>
    internal sealed class FolderTreeController : IDisposable
    {
        private static readonly Color PendingColor = Color.Gray;
        private static readonly Color ScanningColor = Color.DarkOrange;
        private static readonly Color SizeColor = Color.DarkGreen;

        private enum NodeState { Pending, Scanning, Completed }

        private readonly record struct NodeDisplay(string NamePart, string StatusPart, Color NameColor, Color StatusColor)
        {
            public string FullText => NamePart + StatusPart;
        }

        private readonly TreeView _treeView;
        private readonly FolderExplorer _explorer;
        private readonly System.Windows.Forms.Timer _timer;

        private IScanningNode? _lastSeenRoot;
        private bool _disposed;

        public FolderTreeController(TreeView treeView, FolderExplorer explorer)
        {
            _treeView = treeView;
            _explorer = explorer;

            _treeView.DrawMode = TreeViewDrawMode.OwnerDrawText;
            _treeView.DrawNode += OnDrawNode;
            _treeView.BeforeExpand += OnBeforeExpand;
            _treeView.AfterCollapse += OnAfterCollapse;

            _timer = new System.Windows.Forms.Timer { Interval = 300 };
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            var root = _explorer.RootNode;
            if (!ReferenceEquals(root, _lastSeenRoot))
            {
                _lastSeenRoot = root;
                SeedTree(root);
            }

            RefreshVisibleNodes(_treeView.Nodes);
        }

        private void SeedTree(IScanningNode? root)
        {
            _treeView.Nodes.Clear();
            if (root is null)
                return;

            _treeView.Nodes.Add(CreateTreeNode(root));
        }

        private void RefreshVisibleNodes(TreeNodeCollection nodes)
        {
            foreach (TreeNode treeNode in nodes)
            {
                if (treeNode.Tag is not IScanningNode node)
                    continue; // dummy placeholder

                var label = BuildNodeDisplay(node).FullText;
                if (treeNode.Text != label)
                    treeNode.Text = label;

                if (treeNode.Nodes.Count == 0 && node.Children.Count > 0)
                {
                    if (treeNode.Parent == null)
                    {
                        // Root level: auto-populate and expand the first level on its own.
                        PopulateChildren(treeNode, node);
                        treeNode.Expand();
                    }
                    else
                    {
                        treeNode.Nodes.Add(CreateDummyNode());
                    }
                }

                RefreshVisibleNodes(treeNode.Nodes);
            }
        }

        private void OnBeforeExpand(object? sender, TreeViewCancelEventArgs e)
        {
            var treeNode = e.Node!;
            if (treeNode.Tag is not IScanningNode node)
                return;

            if (treeNode.Nodes.Count == 1 && treeNode.Nodes[0].Tag == null)
                PopulateChildren(treeNode, node);
        }

        private void OnAfterCollapse(object? sender, TreeViewEventArgs e)
        {
            var treeNode = e.Node!;
            if (treeNode.Tag is not IScanningNode node)
                return;

            treeNode.Nodes.Clear();
            if (node.Children.Count > 0)
                treeNode.Nodes.Add(CreateDummyNode());
        }

        private void OnDrawNode(object? sender, DrawTreeNodeEventArgs e)
        {
            if (e.Node?.Tag is not IScanningNode node)
            {
                e.DrawDefault = true;
                return;
            }

            var display = BuildNodeDisplay(node);
            var font = e.Node.NodeFont ?? _treeView.Font;
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.Left | TextFormatFlags.VerticalCenter;
            var bounds = e.Bounds;

            var nameWidth = TextRenderer.MeasureText(e.Graphics, display.NamePart, font, bounds.Size, flags).Width;
            var nameRect = new Rectangle(bounds.Left, bounds.Top, nameWidth, bounds.Height);
            TextRenderer.DrawText(e.Graphics, display.NamePart, font, nameRect, display.NameColor, flags);

            var statusRect = new Rectangle(bounds.Left + nameWidth, bounds.Top, Math.Max(bounds.Width - nameWidth, 0), bounds.Height);
            TextRenderer.DrawText(e.Graphics, display.StatusPart, font, statusRect, display.StatusColor, flags);
        }

        private void PopulateChildren(TreeNode treeNode, IScanningNode node)
        {
            treeNode.Nodes.Clear(); // removes the dummy placeholder
            foreach (var child in node.Children)
                treeNode.Nodes.Add(CreateTreeNode(child));
        }

        private TreeNode CreateTreeNode(IScanningNode node)
        {
            var treeNode = new TreeNode(BuildNodeDisplay(node).FullText) { Tag = node };
            if (node.Children.Count > 0)
                treeNode.Nodes.Add(CreateDummyNode());

            return treeNode;
        }

        private static TreeNode CreateDummyNode() => new();

        private NodeDisplay BuildNodeDisplay(IScanningNode node)
        {
            var name = Path.GetFileName(node.FolderName);
            if (string.IsNullOrEmpty(name))
                name = node.FolderName; // e.g. a drive root like "C:\"

            var counts = $"{node.FolderCount} folders, {node.FileCount} files";

            return GetNodeState(node) switch
            {
                NodeState.Pending => new NodeDisplay(name, " (Pending...)", _treeView.ForeColor, PendingColor),
                NodeState.Scanning => new NodeDisplay(name, $" \u2014 {MainForm.FormatBytes(node.FileBytes)} \u00b7 {counts} (Scanning...)", _treeView.ForeColor, ScanningColor),
                _ => new NodeDisplay(name, $" \u2014 {MainForm.FormatBytes(node.FileBytes)} \u00b7 {counts}", _treeView.ForeColor, SizeColor),
            };
        }

        private NodeState GetNodeState(IScanningNode node)
        {
            if (!node.InProgress)
                return NodeState.Completed;

            return IsOnActiveScanPath(node) ? NodeState.Scanning : NodeState.Pending;
        }

        /// <summary>
        /// True for the node currently being scanned and every one of its ancestors, so the whole
        /// active branch - all the way up to the root - shows as "Scanning...", not just the one
        /// leaf actually being worked on right now.
        /// </summary>
        private bool IsOnActiveScanPath(IScanningNode node)
        {
            var current = _explorer.CurrentlyScanningNode;
            while (current != null)
            {
                if (ReferenceEquals(current, node))
                    return true;

                current = current.Parent;
            }

            return false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _timer.Stop();
            _timer.Dispose();
            _treeView.DrawNode -= OnDrawNode;
            _treeView.BeforeExpand -= OnBeforeExpand;
            _treeView.AfterCollapse -= OnAfterCollapse;
        }
    }
}
