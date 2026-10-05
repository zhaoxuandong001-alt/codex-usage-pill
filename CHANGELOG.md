# Changelog

All notable changes to this project are documented here.

## 1.1.0 - 2026-10-05

- Move the default indicator into the new Codex left icon rail, above the account avatar.
- Render CODEX and WEEK vertically with a 3-pixel gap between letter shapes for a taller, less compressed appearance; use uppercase 5H for the short-window label.
- Automatically show weekly-only or stacked five-hour and weekly layouts based on the actual account windows, including after plan changes.
- Identify windows by duration and prefer the named Codex rate-limit bucket over the legacy response.
- Color each window's text, background, and border independently.
- Preserve the old horizontal position and remember vertical placement separately.
- Add automated layout and plan-transition checks to builds and releases, plus installation guidance for every account type.

## 1.0.4 - 2026-08-07

- Restore live usage refreshes with current Codex versions by writing BOM-free UTF-8 to the app server.
- Match the pill border to the green, amber, red, and unavailable status colors.

## 1.0.3 - 2026-07-29

- Add an installer that starts the pill from the native Codex application launch event instead of Windows startup.
- Exit the pill five seconds after a previously detected Codex window closes.
- Keep the pill alive but hidden while Codex is minimized.

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
