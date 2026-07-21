# Changelog

All notable changes to this project are documented here.

## 1.0.2 - 2026-07-21

- Find the real Codex main window when newer desktop versions expose additional small top-level windows.
- Restore drag-to-position behavior after the Codex desktop update.

## 1.0.1 - 2026-07-21

- Keep the pill visible when focus moves from Codex to another application.
- Keep the pill directly above Codex in the window stack instead of globally topmost, so overlapping windows cover it.
- Continue hiding the pill when Codex is closed or minimized.

## 1.0.0 - 2026-07-21

- Added a compact usage pill that follows the Codex desktop window.
- Added weekly and short-window remaining percentages with reset times.
- Added drag-to-position and persistent placement.
- Added color thresholds, tray controls, sign-in, and manual refresh.
- Added per-monitor DPI handling and a true `102 × 29` pixel layout.
- Added single-instance behavior and five-minute refreshes.
- Added dependency-free Windows build and GitHub release automation.
