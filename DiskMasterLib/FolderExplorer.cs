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

        // ---- Fields ----

        private readonly ConcurrentQueue<ScanningNode> _pendingFolders = new();
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
            _rootNode = new() { FolderName = rootFolder };
            SetRunState(RunState.Running);
            _pendingFolders.Enqueue(_rootNode);
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

                if (!_pendingFolders.TryDequeue(out var currentNode))
                {
                    SetRunState(RunState.Completed);
                    _onScannerCompleted();
                    _threadWaitForRunWaiter.SetWait();
                    return;
                }

                if (currentNode.FolderName.Equals(ExcludedFolder, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!TryScanFiles(currentNode))
                    continue;

                EnqueueChildFolders(currentNode);

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
                AddFileBytesToTree(node, fileBytes);
                return true;
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is IOException)
            {
                // Folder is not accesible
                node.ScanError = e;
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
                return;
            }

            foreach (string folder in childFolders)
            {
                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                var childNode = new ScanningNode { FolderName = folder, Parent = node };
                node.Children.Add(childNode);

                _pendingFolders.Enqueue(childNode);
                _scannerStats.TotalFoldersFound++;
            }
        }

        private static void AddFileBytesToTree(ScanningNode node, long fileBytes)
        {
            node.FileBytes += fileBytes;
            var parentNode = node.Parent;
            while (parentNode != ScanningNode.Empty)
            {
                parentNode.FileBytes += fileBytes;
                parentNode = parentNode.Parent;
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
