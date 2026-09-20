using System.Threading;
using DiskMasterLib;

namespace DiskMasterLib.Tests
{
    public class FolderExplorerTests
    {
        [Fact]
        public void Scan_EmptyFolder_CompletesWithZeroCounts()
        {
            using var tree = new TempFolderTree();
            using var harness = new ScanHarness();

            harness.Explorer.Run(tree.RootPath);

            Assert.True(harness.WaitForCompleted(TimeSpan.FromSeconds(5)));
            Assert.Equal(RunState.Completed, harness.LastState);
            Assert.Equal(0, harness.Explorer.TotalFoldersFound);
            Assert.Equal(0, harness.Explorer.TotalFilesFound);
            Assert.Equal(0, harness.Explorer.TotalBytesFound);
        }

        [Fact]
        public void Scan_FolderWithFiles_CountsFilesAndBytes()
        {
            using var tree = new TempFolderTree();
            tree.CreateFile("a.txt", 100);
            tree.CreateFile("b.txt", 250);

            using var harness = new ScanHarness();
            harness.Explorer.Run(tree.RootPath);

            Assert.True(harness.WaitForCompleted(TimeSpan.FromSeconds(5)));
            Assert.Equal(2, harness.Explorer.TotalFilesFound);
            Assert.Equal(350, harness.Explorer.TotalBytesFound);
        }

        [Fact]
        public void Scan_NestedTree_CountsSubfoldersAndRollsUpBytesToAncestors()
        {
            using var tree = new TempFolderTree();
            tree.CreateFile(Path.Combine("a", "b", "leaf.txt"), 42);

            using var harness = new ScanHarness();
            harness.Explorer.Run(tree.RootPath);

            Assert.True(harness.WaitForCompleted(TimeSpan.FromSeconds(5)));
            Assert.Equal(2, harness.Explorer.TotalFoldersFound); // "a" and "a/b"

            Assert.Equal(42, harness.GetNode(tree.RootPath).FileBytes);
            Assert.Equal(42, harness.GetNode(Path.Combine(tree.RootPath, "a")).FileBytes);
            Assert.Equal(42, harness.GetNode(Path.Combine(tree.RootPath, "a", "b")).FileBytes);
        }

        [Fact]
        public void InProgress_StaysTrueUntilAllSiblingsComplete()
        {
            using var tree = new TempFolderTree();
            tree.CreateFile(Path.Combine("a", "b", "c", "file.txt"), 1);
            tree.CreateFile(Path.Combine("a", "x", "file.txt"), 1);

            using var harness = new ScanHarness();
            harness.Explorer.Run(tree.RootPath);

            Assert.True(harness.WaitForCompleted(TimeSpan.FromSeconds(5)));

            // Each node's own onNodeUpdated fires exactly once, right after MarkScanComplete
            // runs for that node's own step - a non-leaf folder still has just-created,
            // in-progress children at that point, a leaf folder does not.
            Assert.True(harness.InProgressAtUpdateTime[tree.RootPath]);
            Assert.True(harness.InProgressAtUpdateTime[Path.Combine(tree.RootPath, "a")]);
            Assert.True(harness.InProgressAtUpdateTime[Path.Combine(tree.RootPath, "a", "b")]);
            Assert.False(harness.InProgressAtUpdateTime[Path.Combine(tree.RootPath, "a", "b", "c")]);
            Assert.False(harness.InProgressAtUpdateTime[Path.Combine(tree.RootPath, "a", "x")]);

            // Once the whole tree is done, completion has propagated all the way up.
            Assert.False(harness.GetNode(tree.RootPath).InProgress);
            Assert.False(harness.GetNode(Path.Combine(tree.RootPath, "a")).InProgress);
            Assert.False(harness.GetNode(Path.Combine(tree.RootPath, "a", "b")).InProgress);
        }

        [Fact]
        public void Pause_ThenResume_CompletesScan()
        {
            using var tree = new TempFolderTree();
            tree.CreateFile("a.txt", 10);

            using var harness = new ScanHarness();

            // Pause() the instant RunState flips to Running: SetRunState invokes this
            // callback synchronously, on this same thread, before the scanning thread is
            // even unblocked. That's the only deterministic way to catch the Running state
            // for a tree this small - otherwise the scan could finish before the test
            // thread gets a chance to call Pause() itself.
            harness.OnStateChanged = state =>
            {
                if (state == RunState.Running)
                    harness.Explorer.Pause();
            };

            harness.Explorer.Run(tree.RootPath);

            Assert.True(harness.WaitForState(RunState.Paused, TimeSpan.FromSeconds(5)));

            harness.OnStateChanged = null;
            harness.Explorer.Run(tree.RootPath); // resume - the folder argument is ignored

            Assert.True(harness.WaitForCompleted(TimeSpan.FromSeconds(5)));
            Assert.Equal(RunState.Completed, harness.LastState);
            Assert.Equal(1, harness.Explorer.TotalFilesFound);
        }

        [Fact]
        public void Stop_LeavesUnprocessedFoldersInProgress()
        {
            using var tree = new TempFolderTree();
            tree.CreateFile(Path.Combine("child", "file.txt"), 5);

            using var harness = new ScanHarness();

            // Stop() the instant the root folder's own scan step finishes - its
            // onNodeUpdated callback runs synchronously on the scanning thread, right
            // before it would move on to dequeue "child" - so "child" is guaranteed to
            // never be processed.
            harness.OnNodeUpdated = node =>
            {
                if (node.FolderName == tree.RootPath)
                    harness.Explorer.Stop();
            };

            harness.Explorer.Run(tree.RootPath);

            Assert.True(harness.WaitForState(RunState.Stopped, TimeSpan.FromSeconds(5)));
            Assert.True(harness.GetNode(tree.RootPath).InProgress);
        }

        [Fact]
        public void Run_WhileRunning_Throws()
        {
            using var tree = new TempFolderTree();
            using var harness = new ScanHarness();

            // Same reentrant trick as the pause test: catch the Running state the moment
            // it's reported, before the scan can race ahead and finish on its own.
            harness.OnStateChanged = state =>
            {
                if (state == RunState.Running)
                    Assert.Throws<InvalidOperationException>(() => harness.Explorer.Run(tree.RootPath));
            };

            harness.Explorer.Run(tree.RootPath);

            Assert.True(harness.WaitForCompleted(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void Pause_WhenNotRunning_Throws()
        {
            using var harness = new ScanHarness();
            Assert.Throws<InvalidOperationException>(() => harness.Explorer.Pause());
        }

        [Fact]
        public void Stop_WhenNotRunningOrPaused_Throws()
        {
            using var harness = new ScanHarness();
            Assert.Throws<InvalidOperationException>(() => harness.Explorer.Stop());
        }

        [Fact]
        public void Dispose_DrivesStateToAborted_AndSubsequentCallsThrow()
        {
            var harness = new ScanHarness();
            harness.Explorer.Dispose();

            Assert.True(harness.WaitForState(RunState.Aborted, TimeSpan.FromSeconds(5)));
            Assert.Throws<ObjectDisposedException>(() => harness.Explorer.Run(@"C:\"));
            Assert.Throws<ObjectDisposedException>(() => harness.Explorer.Pause());
            Assert.Throws<ObjectDisposedException>(() => harness.Explorer.Stop());
        }

        /// <summary>
        /// Wraps a FolderExplorer with thread-safe access to what its callbacks have reported,
        /// plus small polling waits, since FolderExplorer never blocks and calls back on its own
        /// scanning thread.
        /// </summary>
        private sealed class ScanHarness : IDisposable
        {
            private readonly object _lock = new();
            private bool _completed;

            public FolderExplorer Explorer { get; }
            public RunState LastState { get; private set; }
            public Dictionary<string, IScanningNode> NodesByFolder { get; } = new();
            public Dictionary<string, bool> InProgressAtUpdateTime { get; } = new();

            public Action<RunState>? OnStateChanged { get; set; }
            public Action<IScanningNode>? OnNodeUpdated { get; set; }

            public ScanHarness()
            {
                Explorer = new FolderExplorer(
                    onRunStateChanged: state =>
                    {
                        lock (_lock)
                            LastState = state;

                        OnStateChanged?.Invoke(state);
                    },
                    onNodeUpdated: node =>
                    {
                        lock (_lock)
                        {
                            NodesByFolder[node.FolderName] = node;
                            InProgressAtUpdateTime[node.FolderName] = node.InProgress;
                        }

                        OnNodeUpdated?.Invoke(node);
                    },
                    onScannerCompleted: () =>
                    {
                        lock (_lock)
                            _completed = true;
                    });
            }

            public IScanningNode GetNode(string folderPath)
            {
                lock (_lock)
                    return NodesByFolder[folderPath];
            }

            public bool WaitUntil(Func<bool> condition, TimeSpan timeout)
            {
                var deadline = DateTime.UtcNow + timeout;
                while (DateTime.UtcNow < deadline)
                {
                    if (condition())
                        return true;

                    Thread.Sleep(5);
                }

                return condition();
            }

            public bool WaitForState(RunState state, TimeSpan timeout)
                => WaitUntil(() => { lock (_lock) return LastState == state; }, timeout);

            public bool WaitForCompleted(TimeSpan timeout)
                => WaitUntil(() => { lock (_lock) return _completed; }, timeout);

            public void Dispose() => Explorer.Dispose();
        }

        /// <summary>A throwaway real folder tree under the OS temp directory, deleted on Dispose.</summary>
        private sealed class TempFolderTree : IDisposable
        {
            public string RootPath { get; }

            public TempFolderTree()
            {
                RootPath = Path.Combine(Path.GetTempPath(), "DiskMasterLibTests_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(RootPath);
            }

            public void CreateFile(string relativePath, int sizeBytes)
            {
                var fullPath = Path.Combine(RootPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllBytes(fullPath, new byte[sizeBytes]);
            }

            public void Dispose()
            {
                try { Directory.Delete(RootPath, recursive: true); }
                catch { /* best-effort cleanup */ }
            }
        }
    }
}
