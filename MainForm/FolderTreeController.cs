using System.Runtime.InteropServices;
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
    /// Each node's label is drawn in several colored segments (name, size, counts, status) using an
    /// owner-drawn TreeView, since a plain TreeNode can only have one color for its whole text.
    /// </summary>
    internal sealed class FolderTreeController : IDisposable
    {
        private static readonly Color PendingColor = Color.Gray;
        private static readonly Color ScanningColor = Color.DarkOrange;
        private static readonly Color SizeColor = Color.DarkGreen;
        private static readonly Color CountsColor = Color.SteelBlue;

        // Hot-path highlighting: how big a slice of the *whole disk* a node's bytes represent -
        // not the scan total, so it stays meaningful even when scanning a subfolder - shown only
        // once it crosses WarmFraction, colored deeper red past HotFraction. Deliberately a red
        // family, distinct from every other color already in use here, so it never reads as a
        // scan-state indicator. Since FileBytes already rolls up cumulatively to every ancestor,
        // this needs no extra bookkeeping: a hot folder buried three levels down naturally makes
        // its parent, grandparent, etc. show an elevated percentage too, guiding a look downward
        // without needing to single out "the one biggest path" - however many hot spots exist,
        // wherever they are, they light up as soon as their branch is expanded.
        private static readonly Color WarmColor = Color.IndianRed;
        private static readonly Color HotColor = Color.Firebrick;
        private const double WarmFraction = 0.01;
        private const double HotFraction = 0.05;

        // Control.DoubleBuffered (the usual WinForms flicker fix) does nothing for TreeView: it
        // only affects WinForms' own OnPaint pipeline, and TreeView delegates its actual rendering
        // to the native Win32 control underneath. The real fix is telling that native control to
        // double-buffer itself, via SendMessage(TVM_SETEXTENDEDSTYLE, TVS_EX_DOUBLEBUFFER).
        private const int TVM_SETEXTENDEDSTYLE = 0x1100 + 44;
        private const int TVS_EX_DOUBLEBUFFER = 0x0004;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private enum NodeState { Pending, Scanning, Completed }

        /// <summary>
        /// A label as a sequence of (text, color) runs drawn left to right - lets a node's name,
        /// size, counts and status each carry their own color instead of just one color for the
        /// whole line.
        /// </summary>
        private readonly record struct NodeDisplay(IReadOnlyList<(string Text, Color Color)> Segments)
        {
            public string FullText => string.Concat(Segments.Select(s => s.Text));
        }

        private readonly TreeView _treeView;
        private readonly FolderExplorer _explorer;
        private readonly System.Windows.Forms.Timer _timer;

        private IScanningNode? _lastSeenRoot;
        private long _diskSizeBytes;
        private bool _disposed;

        public FolderTreeController(TreeView treeView, FolderExplorer explorer)
        {
            _treeView = treeView;
            _explorer = explorer;

            _treeView.DrawMode = TreeViewDrawMode.OwnerDrawText;
            _treeView.DrawNode += OnDrawNode;
            _treeView.BeforeExpand += OnBeforeExpand;
            _treeView.AfterCollapse += OnAfterCollapse;
            EnableNativeDoubleBuffering();

            _timer = new System.Windows.Forms.Timer { Interval = 300 };
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        private void EnableNativeDoubleBuffering()
        {
            if (!_treeView.IsHandleCreated)
            {
                // FolderTreeController is constructed before the form is shown, so the native
                // control usually doesn't have a Win32 handle yet - defer until it does rather
                // than forcing early handle creation.
                _treeView.HandleCreated += (_, _) => EnableNativeDoubleBuffering();
                return;
            }

            SendMessage(_treeView.Handle, TVM_SETEXTENDEDSTYLE, (IntPtr)TVS_EX_DOUBLEBUFFER, (IntPtr)TVS_EX_DOUBLEBUFFER);
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            // Batch this tick's changes: without BeginUpdate/EndUpdate, every individual Text
            // change or node Add/Clear repaints immediately, which is what was causing the
            // flicker - several visible nodes (the whole active-scan branch) can change on the
            // same tick, so those repaints were visibly stacking up rather than landing as one.
            _treeView.BeginUpdate();
            try
            {
                var root = _explorer.RootNode;
                if (!ReferenceEquals(root, _lastSeenRoot))
                {
                    _lastSeenRoot = root;
                    SeedTree(root);
                }

                RefreshVisibleNodes(_treeView.Nodes);
            }
            finally
            {
                _treeView.EndUpdate();
            }
        }

        private void SeedTree(IScanningNode? root)
        {
            _treeView.Nodes.Clear();
            if (root is null)
            {
                _diskSizeBytes = 0;
                return;
            }

            _diskSizeBytes = GetDiskSizeBytes(root.FolderName);
            _treeView.Nodes.Add(CreateTreeNode(root));
        }

        /// <summary>
        /// 0 (rather than throwing) for anything DriveInfo can't make sense of, e.g. a UNC network
        /// path - hot-path percentages just won't be shown for that scan, which is the right call
        /// anyway since "disk size" isn't a well-defined idea for a network share.
        /// </summary>
        private static long GetDiskSizeBytes(string path)
        {
            try
            {
                var pathRoot = Path.GetPathRoot(path);
                return string.IsNullOrEmpty(pathRoot) ? 0 : new DriveInfo(pathRoot).TotalSize;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
            {
                return 0;
            }
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

            if (treeNode.Nodes.Count != 1 || treeNode.Nodes[0].Tag != null)
                return;

            _treeView.BeginUpdate();
            try
            {
                PopulateChildren(treeNode, node);
            }
            finally
            {
                _treeView.EndUpdate();
            }
        }

        private void OnAfterCollapse(object? sender, TreeViewEventArgs e)
        {
            var treeNode = e.Node!;
            if (treeNode.Tag is not IScanningNode node)
                return;

            _treeView.BeginUpdate();
            try
            {
                treeNode.Nodes.Clear();
                if (node.Children.Count > 0)
                    treeNode.Nodes.Add(CreateDummyNode());
            }
            finally
            {
                _treeView.EndUpdate();
            }
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
            var x = bounds.Left;

            // The control already painted the background before this runs, selection highlight
            // included - our per-segment colors (greens/blues/oranges tuned for a white background)
            // can be unreadable against that highlight (SteelBlue on the system blue highlight is
            // nearly invisible), so match what every native control does: selected text always
            // uses HighlightText, ignoring the segment's own color.
            bool isSelected = (e.State & TreeNodeStates.Selected) != 0;

            foreach (var (text, color) in display.Segments)
            {
                var width = TextRenderer.MeasureText(e.Graphics, text, font, bounds.Size, flags).Width;
                var rect = new Rectangle(x, bounds.Top, width, bounds.Height);
                TextRenderer.DrawText(e.Graphics, text, font, rect, isSelected ? SystemColors.HighlightText : color, flags);
                x += width;
            }
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

        /// <summary>
        /// Size and counts are different kinds of measurement (bytes vs. item counts), so they get
        /// their own colors and are visually grouped separately (size, then counts in parens) rather
        /// than run together - that grouping, not just color, is what actually makes them easy to
        /// tell apart at a glance.
        /// </summary>
        private NodeDisplay BuildNodeDisplay(IScanningNode node)
        {
            var name = Path.GetFileName(node.FolderName);
            if (string.IsNullOrEmpty(name))
                name = node.FolderName; // e.g. a drive root like "C:\"

            var state = GetNodeState(node);

            if (state == NodeState.Pending)
            {
                return new NodeDisplay([
                    (name, _treeView.ForeColor),
                    (" (Pending...)", PendingColor),
                ]);
            }

            var sizeColor = state == NodeState.Scanning ? ScanningColor : SizeColor;
            var size = MainForm.FormatBytes(node.FileBytes);
            var counts = $"{MainForm.FormatCount(node.FolderCount)} folders, {MainForm.FormatCount(node.FileCount)} files";

            var segments = new List<(string, Color)>
            {
                (name, _treeView.ForeColor),
                ($" \u2014 {size}", sizeColor),
            };

            if (TryBuildHotPathSegment(node.FileBytes, out var hotPathSegment))
                segments.Add(hotPathSegment);

            segments.Add(($"  ({counts})", CountsColor));

            if (state == NodeState.Scanning)
                segments.Add((" (Scanning...)", ScanningColor));

            return new NodeDisplay(segments);
        }

        private bool TryBuildHotPathSegment(long fileBytes, out (string Text, Color Color) segment)
        {
            segment = default;
            if (_diskSizeBytes <= 0)
                return false;

            // Round to the displayed precision *before* comparing against the thresholds, not
            // after - otherwise two values that round to the same displayed "5.0%" could land on
            // opposite sides of the Warm/Hot cutoff and show different colors for identical text
            // (the same rounding-vs-threshold mismatch fixed earlier in FormatBytes/FormatCount).
            var percent = Math.Round((double)fileBytes / _diskSizeBytes * 100, 1);
            if (percent < WarmFraction * 100)
                return false;

            var color = percent >= HotFraction * 100 ? HotColor : WarmColor;
            segment = ($" ({percent:F1}% of disk)", color);
            return true;
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
