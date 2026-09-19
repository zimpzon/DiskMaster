using System.Collections.Concurrent;
using System.Diagnostics;

namespace DiskMasterLib
{
    public class FolderExplorer : IDisposable
    {
        public RunState RunState => (RunState)_runState;

        public long FoldersInQueue => _pendingFolders.Count;
        public long TotalFoldersFound => _scannerStats.TotalFoldersFound;
        public long TotalFilesFound => _scannerStats.TotalFilesFound;
        public long TotalBytesFound => _scannerStats.TotalBytesFound;

        private ScannerStats _scannerStats = new();

        private int _runState = (int)RunState.Stopped;
        private ConcurrentQueue<ScanningNode> _pendingFolders = new();
        private Thread _scanningThread;
        private ThreadWaiter _threadPauseWaiter = new(false);
        private ThreadWaiter _threadWaitForRunWaiter = new(false);
        private EventFlag _stopFlag = new();
        private EventFlag _disposeFlag = new();
        private Action<RunState> _onRunStateChanged;
        private Action<IScanningNode> _onNodeUpdated;
        private Action _onScannerCompleted;
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

        private RunState GetRunState()
            => (RunState)Volatile.Read(ref _runState);

        public void Run(string rootFolder)
        {
            ThrowIfDisposed();

            var state = GetRunState();
            if (state == RunState.Running)
                throw new InvalidOperationException($"Already running");

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

        private void ThrowIfDisposed()
            => ObjectDisposedException.ThrowIf(_disposeFlag.IsSet(), this);

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

        private void UpdateTreeWithFolderFileBytes(ScanningNode node, long fileBytes)
        {
            node.FileBytes += fileBytes;
            var parentNode = node.Parent;
            while (parentNode != ScanningNode.Empty)
            {
                parentNode.FileBytes += fileBytes;
                parentNode = parentNode.Parent;
            }
        }

        private void ThreadMain()
        {
            // Scan from beginning loop
            while (!_disposeFlag.IsSet())
            {
                _threadWaitForRunWaiter.WaitIfSet();
                ScanningNode currentNode = ScanningNode.Empty;

                // Folder scanning loop
                while (true)
                {
                    if (_disposeFlag.IsSet())
                    {
                        break;
                    }

                    if (_stopFlag.IsSet())
                    {
                        SetRunState(RunState.Stopped);
                        _threadWaitForRunWaiter.SetWait();
                        break;
                    }

                    if (!_pendingFolders.TryDequeue(out currentNode!))
                    {
                        SetRunState(RunState.Completed);
                        _onScannerCompleted();
                        break;
                    }

                    if (currentNode.FolderName.Equals(@"C:\Windows", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        var files = Directory.GetFiles(currentNode.FolderName).Select(f => new FileInfo(f)).ToList();
                        long fileBytes = files.Sum(f => f.Length);
                        _scannerStats.TotalBytesFound += fileBytes;
                        _scannerStats.TotalFilesFound += files.Count;
                        UpdateTreeWithFolderFileBytes(currentNode, fileBytes);
                    }
                    catch (Exception _) when (_ is UnauthorizedAccessException || _ is IOException)
                    {
                        // Folder is not accesible
                        Debug.WriteLine($"CANNOT ACCESS FOLDER: {_.Message}");
                        continue;
                    }

                    var options = new EnumerationOptions()
                    {
                        AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
                    };

                    var childFolders = Directory.GetDirectories(currentNode.FolderName, "*", options).ToList();
                    //childFolders = childFolders.Where(f => !f.EndsWith(@"\.") && !f.EndsWith(@"\..")).ToArray();

                    foreach (string folder in childFolders)
                    {
                        if (!string.IsNullOrWhiteSpace(folder))
                        {
                                var newNode = new ScanningNode { FolderName = folder };
                                newNode.Parent = currentNode;
                                currentNode.Children.Add(newNode);

                                _pendingFolders.Enqueue(newNode);
                                _scannerStats.TotalFoldersFound++;
                        }
                    }

                    _onNodeUpdated(currentNode);

                    if (_threadPauseWaiter.WouldWait())
                    {
                        SetRunState(RunState.Paused);
                       _threadPauseWaiter.WaitIfSet();
                    }
                }
            }

            SetRunState(RunState.Aborted);
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
    }
}
