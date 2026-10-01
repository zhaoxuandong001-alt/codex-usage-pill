# Codex Usage Pill

[![Build](https://github.com/zhaoxuandong001-alt/codex-usage-pill/actions/workflows/ci.yml/badge.svg)](https://github.com/zhaoxuandong001-alt/codex-usage-pill/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/zhaoxuandong001-alt/codex-usage-pill)](https://github.com/zhaoxuandong001-alt/codex-usage-pill/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A tiny, unofficial Windows sidebar overlay that shows Codex usage remaining and automatically adapts to your account's usage windows.

![Codex Usage Pill preview](docs/screenshot.png)

## Features

- Shows a compact vertical `Codex` indicator when only a weekly usage window is available.
- Shows a `5h` indicator above a vertical `Week` indicator when five-hour and weekly windows are available.
- Automatically switches layouts after plan upgrades, downgrades, or changes to the account's usage windows.
- Remains visible when you work on another monitor, but stays below any window that covers Codex.
- Uses a 36-pixel-wide indicator that starts in the left icon rail, above the account avatar, and can be dragged inside the Codex window.
- Uses uppercase `CODEX`, `WEEK`, and `5H` inside the indicator. Stacks each letter of `CODEX` and `WEEK` with a 3-pixel gap between the visible letter shapes; percentages remain horizontal.
- Remembers its position between launches.
- Shows 5-hour and weekly windows, reset times, and last refresh time on hover.
- Changes each window's text, background, and border independently from green to amber below 50% and red below 20%.
- Refreshes every five minutes, with manual refresh from the tray icon.
- Supports Codex sign-in without reading browser cookies or handling tokens itself.
- Can start from Codex's Windows launch event and exit after Codex closes, with no resident watcher or service.
- Runs as a single instance.

## Requirements

- Windows 10 or Windows 11, 64-bit.
- Codex desktop or Codex CLI installed.
- A signed-in Codex account with usage limits available to the CLI.

## Install

1. Download `CodexUsagePill.exe`, `install.ps1`, and `SHA256SUMS.txt` from the [latest release](https://github.com/zhaoxuandong001-alt/codex-usage-pill/releases/latest) into the same folder.
2. Optionally verify the SHA-256 checksum:

   ```powershell
   Get-FileHash .\CodexUsagePill.exe -Algorithm SHA256
   ```

3. Install the event-triggered launcher:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
   ```

4. Open the Codex desktop app. Windows starts the pill automatically. Closing Codex also exits the pill after a five-second grace period.

The release executable is not code-signed. Windows may show a SmartScreen warning for a newly published build. Review the source and checksum before deciding whether to run it.

### Which version should I install?

Install the same executable and installer for every supported account. You do not need to choose a Plus or Pro download.

| Windows reported by Codex | Layout selected automatically |
| --- | --- |
| Weekly only, with no five-hour window | Vertical `Codex`, followed by the weekly remaining percentage |
| Five-hour and weekly | `5h` and its remaining percentage above vertical `Week` and its remaining percentage |
| Five-hour only | `5h` and its remaining percentage |
| Usage unavailable | Gray indicator with a dash and an explanation on hover |

The tool uses the actual window durations returned by Codex (300 minutes and 10,080 minutes), rather than guessing from the plan name or the order of the windows. After a plan upgrade or downgrade, it adjusts on the next successful refresh (every five minutes). Use **Refresh now** from the tray to check immediately. A missing window is not displayed as 0% or 100%.

### Updating an existing installation

Download the new release files and run the same installer again. It replaces the installed executable and keeps the Codex launch task. The vertical layout uses a separate position file, so the old horizontal position is preserved and the first vertical launch starts in the left icon rail. Later vertical launches remember your new position.

### Installation prompt for a coding assistant

Copy this prompt if someone is helping you install the tool:

```text
Install the latest published release of zhaoxuandong001-alt/codex-usage-pill on this Windows computer. Download CodexUsagePill.exe, install.ps1, and SHA256SUMS.txt from the same release, verify both file checksums, and run the installer. Use the one automatic-layout build: no five-hour limit means vertical Codex plus the weekly remaining percentage; five-hour and weekly limits mean 5h above vertical Week, each with its own remaining percentage. Do not select a build based on Plus or Pro alone. Confirm that the indicator starts with Codex, appears in the left icon rail above the avatar, can be dragged, and shows the actual account windows. Plan changes should adjust the layout on the next refresh without reinstalling. Keep credentials in Codex's own sign-in flow.
```

### Manual use

You can run `CodexUsagePill.exe` directly without installing the automatic launcher. It waits for Codex to open and exits after a previously detected Codex window closes.

The installer registers a per-user Windows event task named `Codex Usage Pill - Start with Codex`. It does not add the pill to Windows startup.

## Controls

- Drag the pill to move it.
- Hover over it for detailed usage windows and reset times.
- Double-click the green tray icon to refresh immediately.
- Right-click the tray icon for refresh, reset position, sign-in, usage page, and exit commands.

## How it works

Codex Usage Pill locates the installed `codex.exe`, launches the local Codex `app-server` over standard input/output, and requests `account/rateLimits/read`. It converts the returned `usedPercent` values into remaining percentages and immediately terminates the helper process.

It does **not**:

- open or parse `auth.json`;
- read Chrome or browser cookies;
- save access or refresh tokens;
- send usage data to this project or another third-party service;
- modify Codex files or settings.

The app stores only the sidebar indicator's last position in `%LOCALAPPDATA%\CodexUsagePill\sidebar-position.txt`. Earlier releases used `position.txt`; that file is preserved.

Automatic launch uses the Windows `Microsoft-Windows-AppModel-Runtime/Admin` event log to detect the stable Codex application identifier. No polling process runs in the background before Codex starts.

## Build from source

No package manager or external dependency is required. On Windows:

```powershell
git clone https://github.com/zhaoxuandong001-alt/codex-usage-pill.git
cd codex-usage-pill
.\build.ps1
.\test.ps1
```

The executable and checksum are written to `dist\`. The layout tests cover weekly-only and dual-window responses, window order, plan transitions, independent warning colors, and missing data. The release workflow runs these checks before publishing.

Useful diagnostic switches:

```powershell
.\dist\CodexUsagePill.exe --preview
.\dist\CodexUsagePill.exe --probe
.\dist\CodexUsagePill.exe --login
```

`--probe` prints a token-free JSON snapshot to the console only when the source is compiled as a console test host; the release build is a windowed application.

## Troubleshooting

**The pill does not appear**

- Make sure the Codex desktop window is open and restored.
- Check the hidden-icons area of the Windows taskbar for the green `C` icon.
- Right-click the tray icon and choose **Reset position**.

**The pill shows `Codex —`**

- Right-click the tray icon and choose **Sign in to Codex…**.
- Confirm `codex.exe` is installed by running `codex --version` in PowerShell.

**Usage stopped updating after a Codex update**

The local app-server protocol can change. Open an issue with the Codex version and the error shown in the tooltip; do not include credentials or the contents of `auth.json`.

## Typography and branding

The published build uses the Windows system font `Segoe UI Semibold`. OpenAI Sans is not bundled or redistributed. Codex is a product of OpenAI. This community project is not affiliated with, endorsed by, or sponsored by OpenAI.

## Project notes

See [Project history and design decisions](docs/PROJECT_HISTORY.md) for the full evolution from the initial status-bar problem to the public release.

## Contributing and security

Contributions are welcome; see [CONTRIBUTING.md](CONTRIBUTING.md). Please report security problems as described in [SECURITY.md](SECURITY.md), not in a public issue.

## License

MIT © 2026 Joshua Dong. See [LICENSE](LICENSE).
