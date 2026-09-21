using System.Collections.Concurrent;
using System.Diagnostics;

namespace DiskMasterLib
{
    public class FolderExplorer : IDisposable
    {
        private const string ExcludedFolder = @"C:\Windows";

        // ---- Public state ----

        public RunState RunState => (RunState)Volatile.Read(ref _runState);
        public long FoldersInQueue => _pendingFolders.Count;
        public long TotalFoldersFound => _scannerStats.TotalFoldersFound;
        public long TotalFilesFound => _scannerStats.TotalFilesFound;
        public long TotalBytesFound => _scannerStats.TotalBytesFound;

        /// <summary>
        /// How many folders were left out of the totals above - either hardcoded-excluded
        /// (ExcludedFolder) or inaccessible (UnauthorizedAccessException/IOException while
        /// reading its files or listing its subfolders). The totals are never wrong so much as
        /// incomplete when this is non-zero.
        /// </summary>
        public long SkippedFolderCount => _scannerStats.SkippedFolderCount;

        /// <summary>
        /// The root of the current (or most recent) scan, or null before Run() has ever been called.
        /// _rootNode is only ever touched from the caller's thread (assigned in Run()), so this needs
        /// no extra synchronization beyond that same-thread assumption.
        /// </summary>
        public IScanningNode? RootNode => ReferenceEquals(_rootNode, ScanningNode.Empty) ? null : _rootNode;

        /// <summary>
        /// The one node the scanning thread is actively working on right now, or null when nothing
        /// is running. Every other node with InProgress == true is merely waiting - either still
        /// queued, or an ancestor waiting on descendants. Reference assignment is atomic and this is
        /// only ever written by the scanning thread, so Volatile is enough - no lock needed.
        /// </summary>
        public IScanningNode? CurrentlyScanningNode => Volatile.Read(ref _currentlyScanningNode);

        // ---- Fields ----

        // A stack, not a queue: scanning goes depth-first (fully finish one branch before
        // starting the next sibling) rather than breadth-first, so a live view doesn't jump
        // between unrelated branches while working through a level.
        private readonly ConcurrentStack<ScanningNode> _pendingFolders = new();
        private readonly Thread _scanningThread;
        private readonly ThreadWaiter _threadPauseWaiter = new(false);
        private readonly ThreadWaiter _threadWaitForRunWaiter = new(false);
        private readonly EventFlag _stopFlag = new();
        private readonly EventFlag _disposeFlag = new();
        private readonly Action<RunState> _onRunStateChanged;
        private readonly Action<IScanningNode> _onNodeUpdated;
        private readonly Action _onScannerCompleted;

        private ScannerStats _scannerStats = new();
        private int _runState = (int)RunState.Stopped;
        private ScanningNode _rootNode = ScanningNode.Empty;
        private ScanningNode? _currentlyScanningNode;

        public FolderExplorer(Action<RunState> onRunStateChanged, Action<IScanningNode> onNodeUpdated, Action onScannerCompleted)
        {
            ArgumentNullException.ThrowIfNull(onRunStateChanged, nameof(onRunStateChanged));
            ArgumentNullException.ThrowIfNull(onNodeUpdated, nameof(onNodeUpdated));
            ArgumentNullException.ThrowIfNull(onScannerCompleted, nameof(onScannerCompleted));

            _onRunStateChanged = onRunStateChanged;
            _onNodeUpdated = onNodeUpdated;
            _onScannerCompleted = onScannerCompleted;

            _scanningThread = new Thread(ThreadMain)
            {
                Priority = ThreadPriority.BelowNormal
            };

            _threadWaitForRunWaiter.SetWait();
            _scanningThread.Start();
        }

        // ---- Public control API ----

        public void Run(string rootFolder)
        {
            ThrowIfDisposed();

            var state = GetRunState();
            if (state == RunState.Running)
                throw new InvalidOperationException($"Already running");

            if (state == RunState.PausePending || state == RunState.StopPending)
                throw new InvalidOperationException($"Cannot start Run in state {state}");

            bool isResume = state == RunState.Paused;
            if (isResume)
            {
                SetRunState(RunState.Running);
                _threadPauseWaiter.SetNoWait();
                return;
            }

            PrepareForNewRun();
            _rootNode = new() { FolderName = rootFolder, InProgress = true };
            SetRunState(RunState.Running);
            _pendingFolders.Push(_rootNode);
            _threadWaitForRunWaiter.SetNoWait();
        }

        public void Pause()
        {
            ThrowIfDisposed();

            var state = GetRunState();
            if (state != RunState.Running)
                throw new InvalidOperationException($"Must be running to pause");

            SetRunState(RunState.PausePending);
            _threadPauseWaiter.SetWait();
        }

        public void Stop()
        {
            ThrowIfDisposed();

            var state = GetRunState();
            if (state != RunState.Running && state != RunState.Paused)
                throw new InvalidOperationException($"Must be running to stop");

            SetRunState(RunState.StopPending);
            _threadPauseWaiter.SetNoWait();
            _stopFlag.Set();
        }

        /// <summary>
        /// Dispose waits for the worker thread to exit. Callbacks could happen before is has exited so
        /// if this is called from UI thread and callbacks also requires UI thread a deadlock could occur.
        /// </summary>
        public void Dispose()
        {
            _disposeFlag.Set();
            _threadPauseWaiter.SetNoWait();
            _threadWaitForRunWaiter.SetNoWait();
        }

        // ---- Run-state bookkeeping ----

        private RunState GetRunState()
            => (RunState)Volatile.Read(ref _runState);

        private void SetRunState(RunState runState)
        {
            if (Volatile.Read(in _runState) == (int)runState)
                return;

            Volatile.Write(ref _runState, (int)runState);
            _onRunStateChanged((RunState)_runState);
        }

        private void PrepareForNewRun()
        {
            SetRunState(RunState.WaitingForRun);
            _threadWaitForRunWaiter.SetWait();
            _threadPauseWaiter.SetNoWait();
            _stopFlag.Clear();
            _pendingFolders.Clear();
            _scannerStats = new();
            Volatile.Write(ref _currentlyScanningNode, null);
        }

        private void ThrowIfDisposed()
            => ObjectDisposedException.ThrowIf(_disposeFlag.IsSet(), this);

        // ---- Worker thread: scans folders from _pendingFolders until stopped, disposed or drained ----

        private void ThreadMain()
        {
            while (!_disposeFlag.IsSet())
            {
                _threadWaitForRunWaiter.WaitIfSet();
                RunScanLoop();
            }

            SetRunState(RunState.Aborted);
        }

        private void RunScanLoop()
        {
            while (true)
            {
                if (_disposeFlag.IsSet())
                    return;

                if (_stopFlag.IsSet())
                {
                    SetRunState(RunState.Stopped);
                    _threadWaitForRunWaiter.SetWait();
                    return;
                }

                if (!_pendingFolders.TryPop(out var currentNode))
                {
                    SetRunState(RunState.Completed);
                    _onScannerCompleted();
                    _threadWaitForRunWaiter.SetWait();
                    return;
                }

                Volatile.Write(ref _currentlyScanningNode, currentNode);

                if (currentNode.FolderName.Equals(ExcludedFolder, StringComparison.OrdinalIgnoreCase))
                {
                    _scannerStats.SkippedFolderCount++;
                    MarkScanComplete(currentNode);
                    continue;
                }

                if (!TryScanFiles(currentNode))
                {
                    MarkScanComplete(currentNode);
                    continue;
                }

                EnqueueChildFolders(currentNode);
                MarkScanComplete(currentNode);

                _onNodeUpdated(currentNode);

                PauseIfRequested();
            }
        }

        private bool TryScanFiles(ScanningNode node)
        {
            try
            {
                var files = Directory.GetFiles(node.FolderName).Select(f => new FileInfo(f)).ToList();
                long fileBytes = files.Sum(f => f.Length);

                _scannerStats.TotalBytesFound += fileBytes;
                _scannerStats.TotalFilesFound += files.Count;
                AddFileStatsToTree(node, fileBytes, files.Count);
                return true;
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is IOException)
            {
                // Folder is not accesible
                node.ScanError = e;
                _scannerStats.SkippedFolderCount++;
                Debug.WriteLine($"Cannot access files in folder {node.FolderName}: {e.Message}");
                return false;
            }
        }

        private void EnqueueChildFolders(ScanningNode node)
        {
            var options = new EnumerationOptions()
            {
                AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
            };

            List<string> childFolders;
            try
            {
                childFolders = Directory.GetDirectories(node.FolderName, "*", options).ToList();
            }
            catch (Exception e)
            {
                // Unknown error enumerating directories, skip it.
                // It could have been deleted or similar.
                Debug.WriteLine($"Cannot get child directories for {node.FolderName} : {e.Message}");
                node.ScanError = e;
                _scannerStats.SkippedFolderCount++;
                return;
            }

            var childNodes = new List<ScanningNode>();
            foreach (string folder in childFolders)
            {
                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                var childNode = new ScanningNode { FolderName = folder, Parent = node, InProgress = true };
                node.AddChild(childNode);
                childNodes.Add(childNode);

                _scannerStats.TotalFoldersFound++;
            }

            if (childNodes.Count > 0)
                AddFolderCountToTree(node, childNodes.Count);

            // Push in reverse so the first-discovered child (also the first shown in the tree)
            // ends up on top of the stack and is scanned first - without this, the stack's LIFO
            // order would dive into the last child first instead.
            for (int i = childNodes.Count - 1; i >= 0; i--)
                _pendingFolders.Push(childNodes[i]);
        }

        private static void AddFileStatsToTree(ScanningNode node, long fileBytes, long fileCount)
        {
            node.FileBytes += fileBytes;
            node.FileCount += fileCount;
            var parentNode = node.Parent;
            while (parentNode != ScanningNode.Empty)
            {
                parentNode.FileBytes += fileBytes;
                parentNode.FileCount += fileCount;
                parentNode = parentNode.Parent;
            }
        }

        /// <summary>
        /// Adds newly-discovered subfolders to node's count and every ancestor's - node itself is
        /// included since they're direct children of it, but the walk stops before Empty so the
        /// sentinel itself is never touched.
        /// </summary>
        private static void AddFolderCountToTree(ScanningNode node, long folderCount)
        {
            var current = node;
            while (true)
            {
                current.FolderCount += folderCount;

                if (current.Parent == ScanningNode.Empty)
                    return;

                current = current.Parent;
            }
        }

        /// <summary>
        /// Clears InProgress on a node once none of its children are still in progress, then does the
        /// same check on its parent, and so on up the tree. A child's InProgress already reflects whether
        /// everything beneath it is done, so checking only direct children at each level is enough.
        /// </summary>
        private static void MarkScanComplete(ScanningNode node)
        {
            var current = node;
            while (true)
            {
                if (current.Children.Any(c => c.InProgress))
                    return;

                current.InProgress = false;

                if (current.Parent == ScanningNode.Empty)
                    return;

                current = current.Parent;
            }
        }

        private void PauseIfRequested()
        {
            if (!_threadPauseWaiter.WouldWait())
                return;

            SetRunState(RunState.Paused);
            _threadPauseWaiter.WaitIfSet();
        }
    }
}
