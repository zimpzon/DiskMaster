# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

DiskMaster is a Windows Forms disk-usage scanner. It walks a folder tree on a background thread, accumulating file counts/sizes per folder, and reports progress back to the UI. Three projects, referenced via `DiskMaster.slnx`:

- **DiskMasterLib** (`net10.0`) — the scan engine. Platform-agnostic, no UI dependencies.
- **MainForm** (`net10.0-windows`, WinForms) — the UI, references `DiskMasterLib`.
- **DiskMasterLib.Tests** (`net10.0`, xUnit) — tests `DiskMasterLib` only, via its public API.

## Commands

- Build: `dotnet build` (from repo root; builds all three projects via `DiskMaster.slnx`)
- Run: `dotnet run --project MainForm`
- Test: `dotnet test` (or `dotnet test --filter FullyQualifiedName~TestName` for a single test)

## Architecture

### `FolderExplorer` (DiskMasterLib) — the scan engine

`FolderExplorer` owns a dedicated background thread (`_scanningThread`, started in the constructor) that walks folders pulled from a `ConcurrentQueue<ScanningNode>`. It's a small state machine over `RunState` (`WaitingForRun → Running ⇄ Paused (via PausePending) → Stopped/Completed/Aborted`, controlled via `Run()` / `Pause()` / `Stop()` / `Dispose()`).

Thread signaling uses two small wrapper types instead of raw `ManualResetEvent`/flags:
- `ThreadWaiter` — wraps `ManualResetEvent` with `SetWait()`/`SetNoWait()` naming, since `ManualResetEvent`'s own "signaled = don't wait" polarity reads backwards.
- `EventFlag` — a `Volatile`-backed boolean flag (`_stopFlag`, `_disposeFlag`).

The scan pipeline itself is `ThreadMain()` → `RunScanLoop()` → `TryScanFiles()` / `EnqueueChildFolders()` per folder, with `AddFileBytesToTree()` propagating found bytes up through `ScanningNode.Parent` to every ancestor. Each loop iteration in `RunScanLoop` checks `_disposeFlag` / `_stopFlag` / queue-empty before dequeuing the next folder — when touching this method, keep each exit path (`Stopped`, `Completed`, disposed) symmetric: every path that stops the loop must also re-arm `_threadWaitForRunWaiter` via `SetWait()`, or the thread will spin instead of blocking on the next `Run()`.

`ScanningNode.InProgress` tracks whether a folder or anything beneath it is still being scanned — set `true` at node creation, cleared by `MarkScanComplete()` once none of a node's direct children are still in progress (a child's own `InProgress` already recursively encodes its whole subtree, so checking only direct children is enough). `MarkScanComplete(currentNode)` must be called at every point in `RunScanLoop` where a node finishes being handled — currently three: the excluded-folder skip, a failed `TryScanFiles`, and after `EnqueueChildFolders` on the success path. Miss one of those and its ancestors never clear `InProgress`. Folders still queued when `Stop()`/`Dispose()` cuts a scan short are intentionally left `InProgress = true` — never processed, never completed.

`FolderExplorer`'s three callbacks (`onRunStateChanged`, `onNodeUpdated`, `onScannerCompleted`) are invoked directly from the scanning thread, not marshaled anywhere — any UI consumer must dispatch to its own thread itself (see `MainForm.Invoke(...)` usage).

`C:\Windows` is hardcoded as an excluded folder (`ExcludedFolder` const in `FolderExplorer`) — it's skipped entirely, not descended into.

### `ScanningNode` (DiskMasterLib, internal) — the folder tree

Each scanned folder is a `ScanningNode`, forming a tree via `Parent`/`Children`. `ScanningNode.Empty` is a shared sentinel used to terminate parent-walks (`while (parentNode != ScanningNode.Empty)`) — it's self-referencing (`Empty.Parent == Empty`), which is set up explicitly in a static constructor rather than via field initializer, because a field initializer runs before the static `Empty` field itself is assigned and would otherwise capture `null`.

The public-facing `IScanningNode` interface exposes a read-only subset (`FolderName`, `FileBytes`, `InProgress`) for consumers outside the library; `ScanningNode` itself is `internal`.

### `MainForm` (WinForms) — the UI

`Program.cs` wires everything through `Microsoft.Extensions.Hosting`'s generic host: `FolderExplorerFactory` is a singleton, `MainForm` is transient, resolved once and passed to `Application.Run`.

`MainForm` creates its `FolderExplorer` in its constructor via the injected `FolderExplorerFactory`, wiring its three callbacks. Since those callbacks fire on the scanner's background thread, every UI-touching handler wraps its work in `Invoke(...)`.

`MainForm_FormClosing` cancels the first close attempt and calls `_folderExplorer.Dispose()` instead; the actual `Close()` happens once the scanner thread reports `RunState.Aborted` back through `OnScannerRunStateChanged`, so the form only closes after the background thread has actually exited.

`BtnRun_Click` currently scans a hardcoded path (`C:\everquestlegends`) — there's no folder picker wired up yet, and `TvFolderView` in the designer is not currently populated by any code.

### `DiskMasterLib.Tests` — testing approach

There's no file-system abstraction in `FolderExplorer` (it calls `Directory.GetFiles`/`GetDirectories` directly), so tests run against small real folder trees created under the OS temp directory rather than mocks — see the `TempFolderTree` helper in `FolderExplorerTests.cs`.

`FolderExplorer` never blocks and reports everything through its three constructor callbacks on its own scanning thread, so tests can't assert immediately after calling `Run()`. The `ScanHarness` helper in the same file wraps a `FolderExplorer`, records what its callbacks report under a lock, and exposes polling waits (`WaitForState`, `WaitForCompleted`) plus optional `OnStateChanged`/`OnNodeUpdated` hooks.

Those hooks exist to deterministically hit otherwise-racy states on tiny/fast trees: since `SetRunState` invokes `onRunStateChanged` *synchronously* before returning, a hook that calls e.g. `Pause()` the instant it sees `RunState.Running` is guaranteed to catch that state — see `Pause_ThenResume_CompletesScan`, `Run_WhileRunning_Throws`, and `Stop_LeavesUnprocessedFoldersInProgress` for the pattern (the last one hooks `onNodeUpdated` instead, which fires synchronously on the *scanning* thread). Don't reach for `Thread.Sleep`-based timing to hit a specific state — it's flaky for trees this small; use the reentrant-callback trick instead.
