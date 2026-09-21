using System.Security.AccessControl;
using System.Security.Principal;
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
            Assert.Equal(0, harness.Explorer.SkippedFolderCount);
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
        public void Scan_NestedTree_RollsUpFileAndFolderCountsToAncestors()
        {
            using var tree = new TempFolderTree();
            tree.CreateFile(Path.Combine("a", "top.txt"), 1);
            tree.CreateFile(Path.Combine("a", "b", "b1.txt"), 1);
            tree.CreateFile(Path.Combine("a", "b", "b2.txt"), 1);
            tree.CreateFile(Path.Combine("a", "b", "c", "leaf.txt"), 1);

            using var harness = new ScanHarness();
            harness.Explorer.Run(tree.RootPath);

            Assert.True(harness.WaitForCompleted(TimeSpan.FromSeconds(5)));

            var root = harness.GetNode(tree.RootPath);
            var a = harness.GetNode(Path.Combine(tree.RootPath, "a"));
            var b = harness.GetNode(Path.Combine(tree.RootPath, "a", "b"));
            var c = harness.GetNode(Path.Combine(tree.RootPath, "a", "b", "c"));

            // FolderCount excludes the node itself: "a" has "b" and "c" beneath it (2),
            // "b" has just "c" beneath it (1), "c" is a leaf (0).
            Assert.Equal(3, root.FolderCount); // a, b, c
            Assert.Equal(2, a.FolderCount);    // b, c
            Assert.Equal(1, b.FolderCount);    // c
            Assert.Equal(0, c.FolderCount);

            // FileCount includes the node's own files: 4 total (top.txt, b1.txt, b2.txt, leaf.txt).
            Assert.Equal(4, root.FileCount);
            Assert.Equal(4, a.FileCount);
            Assert.Equal(3, b.FileCount); // b1.txt, b2.txt, leaf.txt
            Assert.Equal(1, c.FileCount); // leaf.txt
        }

        [Fact]
        public void Scan_AccessDeniedFolder_IncrementsSkippedFolderCountAndExcludesItsBytes()
        {
            using var tree = new TempFolderTree();
            tree.CreateFile("visible.txt", 5);
            tree.DenyAccess("denied");

            using var harness = new ScanHarness();
            harness.Explorer.Run(tree.RootPath);

            Assert.True(harness.WaitForCompleted(TimeSpan.FromSeconds(10)));

            // "denied" itself is still discovered (its name is visible when listing the root),
            // but its own contents can't be read, so it never contributes bytes and gets counted
            // as skipped instead.
            Assert.True(harness.Explorer.SkippedFolderCount >= 1);
            Assert.Equal(5, harness.Explorer.TotalBytesFound);
        }

        [Fact]
        public void RootNode_IsNullBeforeRunAndMatchesScanAfter()
        {
            using var tree = new TempFolderTree();
            using var harness = new ScanHarness();

            Assert.Null(harness.Explorer.RootNode);

            harness.Explorer.Run(tree.RootPath);

            Assert.True(harness.WaitForCompleted(TimeSpan.FromSeconds(5)));
            Assert.NotNull(harness.Explorer.RootNode);
            Assert.Equal(tree.RootPath, harness.Explorer.RootNode!.FolderName);
        }

        [Fact]
        public void Children_ReflectsDiscoveredSubfolders()
        {
            using var tree = new TempFolderTree();
            tree.CreateFile(Path.Combine("a", "file.txt"), 1);
            tree.CreateFile(Path.Combine("b", "file.txt"), 1);

            using var harness = new ScanHarness();
            harness.Explorer.Run(tree.RootPath);

            Assert.True(harness.WaitForCompleted(TimeSpan.FromSeconds(5)));

            var childNames = harness.GetNode(tree.RootPath).Children
                .Select(c => Path.GetFileName(c.FolderName))
                .OrderBy(n => n)
                .ToArray();
            Assert.Equal(new[] { "a", "b" }, childNames);

            // The two leaf folders themselves have no children.
            Assert.Empty(harness.GetNode(Path.Combine(tree.RootPath, "a")).Children);
            Assert.Empty(harness.GetNode(Path.Combine(tree.RootPath, "b")).Children);
        }

        [Fact]
        public void Scan_GoesDepthFirst_SubtreesDoNotInterleave()
        {
            using var tree = new TempFolderTree();
            tree.CreateFile(Path.Combine("A", "A1", "file.txt"), 1);
            tree.CreateFile(Path.Combine("A", "A2", "file.txt"), 1);
            tree.CreateFile(Path.Combine("B", "B1", "file.txt"), 1);

            using var harness = new ScanHarness();
            harness.Explorer.Run(tree.RootPath);

            Assert.True(harness.WaitForCompleted(TimeSpan.FromSeconds(5)));

            var order = harness.UpdateOrder.Select(Path.GetFileName).ToList();
            var aIndexes = new[] { "A", "A1", "A2" }.Select(n => order.IndexOf(n)).ToArray();
            var bIndexes = new[] { "B", "B1" }.Select(n => order.IndexOf(n)).ToArray();

            Assert.All(aIndexes, i => Assert.True(i >= 0));
            Assert.All(bIndexes, i => Assert.True(i >= 0));

            // Depth-first means whichever of A/B's subtree starts first must finish entirely
            // before the other one starts - they must never interleave. Which one goes first
            // isn't guaranteed (Directory.GetDirectories' order isn't contractually defined),
            // so check both orderings rather than assuming A comes before B.
            bool aBeforeB = aIndexes.Max() < bIndexes.Min();
            bool bBeforeA = bIndexes.Max() < aIndexes.Min();
            Assert.True(aBeforeB || bBeforeA, $"Expected A's and B's subtrees not to interleave. Order: {string.Join(", ", order)}");
        }

        [Fact]
        public void Parent_IsNullForRootAndWalksUpForChildren()
        {
            using var tree = new TempFolderTree();
            tree.CreateFile(Path.Combine("a", "b", "file.txt"), 1);

            using var harness = new ScanHarness();
            harness.Explorer.Run(tree.RootPath);

            Assert.True(harness.WaitForCompleted(TimeSpan.FromSeconds(5)));

            var root = harness.GetNode(tree.RootPath);
            var a = harness.GetNode(Path.Combine(tree.RootPath, "a"));
            var b = harness.GetNode(Path.Combine(tree.RootPath, "a", "b"));

            Assert.Null(root.Parent);
            Assert.Same(root, a.Parent);
            Assert.Same(a, b.Parent);
        }

        [Fact]
        public void Children_CanBeReadConcurrentlyWhileScanning()
        {
            using var tree = new TempFolderTree();
            for (int i = 0; i < 50; i++)
                tree.CreateFile(Path.Combine($"folder{i}", "file.txt"), 1);

            using var harness = new ScanHarness();

            Exception? readerException = null;
            var stopReading = false;

            var reader = new Thread(() =>
            {
                try
                {
                    while (!Volatile.Read(ref stopReading))
                    {
                        var root = harness.Explorer.RootNode;
                        if (root != null)
                        {
                            foreach (var child in root.Children)
                                _ = child.FolderName; // touch the snapshot
                        }
                    }
                }
                catch (Exception ex)
                {
                    readerException = ex;
                }
            });
            reader.Start();

            harness.Explorer.Run(tree.RootPath);
            Assert.True(harness.WaitForCompleted(TimeSpan.FromSeconds(10)));

            Volatile.Write(ref stopReading, true);
            reader.Join(TimeSpan.FromSeconds(5));

            Assert.Null(readerException);
        }

        [Fact]
        public void CurrentlyScanningNode_IsNullBeforeRun()
        {
            using var harness = new ScanHarness();
            Assert.Null(harness.Explorer.CurrentlyScanningNode);
        }

        [Fact]
        public void CurrentlyScanningNode_MatchesTheNodeBeingProcessed()
        {
            using var tree = new TempFolderTree();
            tree.CreateFile(Path.Combine("a", "file.txt"), 1);
            tree.CreateFile(Path.Combine("b", "file.txt"), 1);

            using var harness = new ScanHarness();
            harness.Explorer.Run(tree.RootPath);

            Assert.True(harness.WaitForCompleted(TimeSpan.FromSeconds(5)));

            // At the moment each node's own onNodeUpdated fires, it must still be the one
            // CurrentlyScanningNode points at - the loop only moves on to the next dequeue
            // afterwards.
            Assert.True(harness.WasCurrentlyScanningAtUpdateTime[tree.RootPath]);
            Assert.True(harness.WasCurrentlyScanningAtUpdateTime[Path.Combine(tree.RootPath, "a")]);
            Assert.True(harness.WasCurrentlyScanningAtUpdateTime[Path.Combine(tree.RootPath, "b")]);
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
            public Dictionary<string, bool> WasCurrentlyScanningAtUpdateTime { get; } = new();
            public List<string> UpdateOrder { get; } = new();

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
                        // Explorer is always assigned by the time this callback can actually fire -
                        // it only runs after Run() is called, long after this constructor returns.
                        var wasCurrentlyScanning = ReferenceEquals(node, Explorer!.CurrentlyScanningNode);

                        lock (_lock)
                        {
                            NodesByFolder[node.FolderName] = node;
                            InProgressAtUpdateTime[node.FolderName] = node.InProgress;
                            WasCurrentlyScanningAtUpdateTime[node.FolderName] = wasCurrentlyScanning;
                            UpdateOrder.Add(node.FolderName);
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

            private readonly List<string> _accessDeniedPaths = new();

            public void CreateFile(string relativePath, int sizeBytes)
            {
                var fullPath = Path.Combine(RootPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllBytes(fullPath, new byte[sizeBytes]);
            }

            /// <summary>
            /// Creates a subfolder and denies the current user's own access to it via a real ACL
            /// deny rule - no admin rights needed, since an owner can always change permissions on
            /// their own objects. Reversed in Dispose() before cleanup deletes the tree.
            /// </summary>
            // ACL manipulation is Windows-only, same as the rest of this app - suppressed rather
            // than annotated with [SupportedOSPlatform], since annotating would make every caller
            // (including every other test's plain `using var tree = new TempFolderTree()`) need
            // the same annotation too.
#pragma warning disable CA1416
            public string DenyAccess(string relativePath)
            {
                var fullPath = Path.Combine(RootPath, relativePath);
                Directory.CreateDirectory(fullPath);

                var dirInfo = new DirectoryInfo(fullPath);
                var security = dirInfo.GetAccessControl();
                var currentUser = WindowsIdentity.GetCurrent().User!;
                security.AddAccessRule(new FileSystemAccessRule(
                    currentUser,
                    FileSystemRights.ListDirectory | FileSystemRights.Read | FileSystemRights.ReadAndExecute,
                    AccessControlType.Deny));
                dirInfo.SetAccessControl(security);

                _accessDeniedPaths.Add(fullPath);
                return fullPath;
            }
#pragma warning restore CA1416

            public void Dispose()
            {
#pragma warning disable CA1416
                foreach (var path in _accessDeniedPaths)
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(path);
                        var security = dirInfo.GetAccessControl();
                        security.PurgeAccessRules(WindowsIdentity.GetCurrent().User!);
                        dirInfo.SetAccessControl(security);
                    }
                    catch { /* best effort - if this fails, the delete below likely will too */ }
                }
#pragma warning restore CA1416

                try { Directory.Delete(RootPath, recursive: true); }
                catch { /* best-effort cleanup */ }
            }
        }
    }
}
