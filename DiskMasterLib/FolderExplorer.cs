using System.Collections.Concurrent;
using System.Diagnostics;

namespace DiskMasterLib
{
    public class FolderExplorer : IDisposable
    {
        public RunState RunState => (RunState)_runState;

        public long FoldersInQueue => _pendingFolders.Count;
        public long TotalFoldersFound => _scannerStats.TotalFoldersFound;
        public long TotalBytesFound => _scannerStats.TotalBytesFound;

        private ScannerStats _scannerStats = new();

        private int _runState = (int)RunState.Stopped;
        private ConcurrentQueue<string> _pendingFolders = new();
        private Thread _scanningThread;
        private ThreadWaiter _threadPauseWaiter = new(false);
        private ThreadWaiter _threadWaitForRunWaiter = new(false);
        private EventFlag _stopFlag = new();
        private EventFlag _disposeFlag = new();
        private Action<RunState> _onRunStateChanged;
        private Action<IScanningNode> _onNodeUpdated;
        private ScanningNode _rootNode = ScanningNode.Empty;

        public FolderExplorer(Action<RunState> onRunStateChanged, Action<IScanningNode> onNodeUpdated)
        {
            ArgumentNullException.ThrowIfNull(onRunStateChanged, nameof(onRunStateChanged));
            ArgumentNullException.ThrowIfNull(onNodeUpdated, nameof(onNodeUpdated));

            _onRunStateChanged = onRunStateChanged;
            _onNodeUpdated = onNodeUpdated;
    
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

            SetRunState(RunState.Running);
            _pendingFolders.Enqueue(rootFolder);
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

        private void ThreadMain()
        {
            // Scan from beginning loop
            while (!_disposeFlag.IsSet())
            {
                _threadWaitForRunWaiter.WaitIfSet();

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

                    if (!_pendingFolders.TryDequeue(out string? currentFolder))
                    {
                        SetRunState(RunState.Completed);
                        break;
                    }

                    if (currentFolder.Equals(@"C:\Windows", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        var enumerationOptions = new EnumerationOptions {
                            IgnoreInaccessible = true,
                            ReturnSpecialDirectories = false,
                        };

                        var childFolders = Directory.GetDirectories(currentFolder, "*", enumerationOptions);
                        foreach (var folder in childFolders)
                        {
                            if (!string.IsNullOrWhiteSpace(folder))
                            {
                                _pendingFolders.Enqueue(folder);
                                _scannerStats.TotalFoldersFound++;
                            }
                        }
                    }
                    catch(Exception _) when (_ is UnauthorizedAccessException || _ is IOException)
                    {
                        ;
                        // Add node with flag
                        Debug.WriteLine(_.Message);
                    }

                    _onNodeUpdated(new ScanningNode { FolderName = currentFolder });

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
