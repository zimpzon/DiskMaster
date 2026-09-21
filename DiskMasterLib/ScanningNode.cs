namespace DiskMasterLib
{
    public interface IScanningNode
    {
        string FolderName { get; }
        long FileBytes { get; }
        bool InProgress { get; }
        IReadOnlyList<IScanningNode> Children { get; }
        IScanningNode? Parent { get; }

        /// <summary>Files found in this folder and everywhere beneath it, itself included.</summary>
        long FileCount { get; }

        /// <summary>Subfolders beneath this one, at any depth - not counting this folder itself.</summary>
        long FolderCount { get; }
    }

    internal class ScanningNode : IScanningNode
    {
        public static readonly ScanningNode Empty = new();

        static ScanningNode()
        {
            Empty.Parent = Empty;
        }

        private readonly object _childrenLock = new();
        private readonly List<ScanningNode> _children = [];

        public ScanningNode Parent { get; set; } = Empty;
        public string FolderName { get; set; } = string.Empty;
        public long FileBytes { get; set; }
        public long FileCount { get; set; }
        public long FolderCount { get; set; }
        public Exception? ScanError { get; set; }
        public bool InProgress { get; set; }

        /// <summary>
        /// Unlocked view for the scanning thread's own same-thread reads (it's the only thread
        /// that ever mutates or reads this). Cross-thread callers must go through the locked,
        /// snapshotting IScanningNode.Children instead.
        /// </summary>
        internal IReadOnlyList<ScanningNode> Children => _children;

        internal void AddChild(ScanningNode child)
        {
            lock (_childrenLock)
                _children.Add(child);
        }

        IReadOnlyList<IScanningNode> IScanningNode.Children
        {
            get
            {
                lock (_childrenLock)
                    return _children.ToArray();
            }
        }

        /// <summary>
        /// Null for the root (mirrors FolderExplorer.RootNode rather than exposing the internal
        /// Empty sentinel). Safe to read cross-thread without a lock: it's set once at construction
        /// and never reassigned afterward, and every path that hands a child node to another thread
        /// already goes through the Children lock, whose acquire/release gives the necessary memory
        /// barrier for this field too (not just _children).
        /// </summary>
        IScanningNode? IScanningNode.Parent => ReferenceEquals(Parent, Empty) ? null : Parent;
    }
}
