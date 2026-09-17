using System.Collections.Concurrent;

namespace DiskMasterLib
{
    public class FolderExplorer : IDisposable
    {
        public RunState RunState => (RunState)_runState;

        private int _runState = (int)RunState.Stopped;
        private ConcurrentQueue<string> _pendingFolders = new();
        private Thread _scanningThread ;
        private ThreadWaiter _threadPauseWaiter = new(false);
        private ThreadWaiter _threadWaitForRunWaiter = new(false);
        private EventFlag _stopFlag = new();
        private EventFlag _disposeFlag = new();
        private Action<RunState> _onRunStateChanged;
        private Action<IScanningNode> _onNodeUpdated;

        public FolderExplorer(Action<RunState> onRunStateChanged, Action<IScanningNode> onNodeUpdated)
        {
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

        private void PrepareForCleanRun()
        {
            SetRunState(RunState.WaitingForRun);
            _threadWaitForRunWaiter.SetWait();
            _threadPauseWaiter.SetNoWait();
            _stopFlag.Clear();
            _pendingFolders.Clear();
        }

        public void Run(string rootFolder)
        {
            ThrowIfDisposed();
            if (Volatile.Read(in _runState) == (int)RunState.Running)
                throw new InvalidOperationException($"Already running");

            PrepareForCleanRun();

            SetRunState(RunState.Running);
            _pendingFolders.Enqueue(rootFolder);
            _threadWaitForRunWaiter.SetNoWait();
        }

        private void ThrowIfDisposed()
            => ObjectDisposedException.ThrowIf(_disposeFlag.IsSet(), this);

        public void Pause()
        {
            ThrowIfDisposed();
            var state = (RunState)Volatile.Read(in _runState);
            if (state != RunState.Running)
                throw new InvalidOperationException($"Must be running to pause");

            SetRunState(RunState.PausePending);
            _threadPauseWaiter.SetWait();
        }

        public void Stop()
        {
            ThrowIfDisposed();
            var state = (RunState)Volatile.Read(in _runState);
            if (state != RunState.Running && state != RunState.Paused)
                throw new InvalidOperationException($"Must be running to stop");

            SetRunState(RunState.StopPending);
            _threadPauseWaiter.SetNoWait();
            _stopFlag.Set();
        }

        private void ThreadMain()
        {
            // Full scan loop
            while (!_disposeFlag.IsSet())
            {
                _threadWaitForRunWaiter.WaitIfSet();

                // In process scan loop
                while (true)
                {
                    if (_threadPauseWaiter.WouldWait())
                    {
                        SetRunState(RunState.Paused);
                       _threadPauseWaiter.WaitIfSet();
                    }

                    if (_stopFlag.IsSet() || _disposeFlag.IsSet())
                    {
                        SetRunState(RunState.Stopped);
                        break;
                    }

                    Thread.Sleep(500);
                }
            }
        }

        public void Dispose()
        {
            _disposeFlag.Set();
            _threadPauseWaiter.SetNoWait();
            _threadWaitForRunWaiter.SetNoWait();

            _scanningThread.Join();
        }
    }
}
