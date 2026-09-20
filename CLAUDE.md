# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

DiskMaster is a Windows Forms disk-usage scanner. It walks a folder tree on a background thread, accumulating file counts/sizes per folder, and reports progress back to the UI. Two projects, referenced via `DiskMaster.slnx`:

- **DiskMasterLib** (`net10.0`) — the scan engine. Platform-agnostic, no UI dependencies.
- **MainForm** (`net10.0-windows`, WinForms) — the UI, references `DiskMasterLib`.

## Commands

- Build: `dotnet build` (from repo root; builds both projects via `DiskMaster.slnx`)
- Run: `dotnet run --project MainForm`

There is no test project in this repo yet.

## Architecture

### `FolderExplorer` (DiskMasterLib) — the scan engine

`FolderExplorer` owns a dedicated background thread (`_scanningThread`, started in the constructor) that walks folders pulled from a `ConcurrentQueue<ScanningNode>`. It's a small state machine over `RunState` (`WaitingForRun → Running ⇄ Paused (via PausePending) → Stopped/Completed/Aborted`, controlled via `Run()` / `Pause()` / `Stop()` / `Dispose()`).

Thread signaling uses two small wrapper types instead of raw `ManualResetEvent`/flags:
- `ThreadWaiter` — wraps `ManualResetEvent` with `SetWait()`/`SetNoWait()` naming, since `ManualResetEvent`'s own "signaled = don't wait" polarity reads backwards.
- `EventFlag` — a `Volatile`-backed boolean flag (`_stopFlag`, `_disposeFlag`).

The scan pipeline itself is `ThreadMain()` → `RunScanLoop()` → `TryScanFiles()` / `EnqueueChildFolders()` per folder, with `AddFileBytesToTree()` propagating found bytes up through `ScanningNode.Parent` to every ancestor. Each loop iteration in `RunScanLoop` checks `_disposeFlag` / `_stopFlag` / queue-empty before dequeuing the next folder — when touching this method, keep each exit path (`Stopped`, `Completed`, disposed) symmetric: every path that stops the loop must also re-arm `_threadWaitForRunWaiter` via `SetWait()`, or the thread will spin instead of blocking on the next `Run()`.

`FolderExplorer`'s three callbacks (`onRunStateChanged`, `onNodeUpdated`, `onScannerCompleted`) are invoked directly from the scanning thread, not marshaled anywhere — any UI consumer must dispatch to its own thread itself (see `MainForm.Invoke(...)` usage).

`C:\Windows` is hardcoded as an excluded folder (`ExcludedFolder` const in `FolderExplorer`) — it's skipped entirely, not descended into.

### `ScanningNode` (DiskMasterLib, internal) — the folder tree

Each scanned folder is a `ScanningNode`, forming a tree via `Parent`/`Children`. `ScanningNode.Empty` is a shared sentinel used to terminate parent-walks (`while (parentNode != ScanningNode.Empty)`) — it's self-referencing (`Empty.Parent == Empty`), which is set up explicitly in a static constructor rather than via field initializer, because a field initializer runs before the static `Empty` field itself is assigned and would otherwise capture `null`.

The public-facing `IScanningNode` interface exposes a read-only subset (`FolderName`, `FileBytes`) for consumers outside the library; `ScanningNode` itself is `internal`.

### `MainForm` (WinForms) — the UI

`Program.cs` wires everything through `Microsoft.Extensions.Hosting`'s generic host: `FolderExplorerFactory` is a singleton, `MainForm` is transient, resolved once and passed to `Application.Run`.

`MainForm` creates its `FolderExplorer` in its constructor via the injected `FolderExplorerFactory`, wiring its three callbacks. Since those callbacks fire on the scanner's background thread, every UI-touching handler wraps its work in `Invoke(...)`.

`MainForm_FormClosing` cancels the first close attempt and calls `_folderExplorer.Dispose()` instead; the actual `Close()` happens once the scanner thread reports `RunState.Aborted` back through `OnScannerRunStateChanged`, so the form only closes after the background thread has actually exited.

`BtnRun_Click` currently scans a hardcoded path (`C:\everquestlegends`) — there's no folder picker wired up yet, and `TvFolderView` in the designer is not currently populated by any code.
