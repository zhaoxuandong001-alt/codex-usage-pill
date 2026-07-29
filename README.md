# Codex Usage Pill

[![Build](https://github.com/zhaoxuandong001-alt/codex-usage-pill/actions/workflows/ci.yml/badge.svg)](https://github.com/zhaoxuandong001-alt/codex-usage-pill/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/zhaoxuandong001-alt/codex-usage-pill)](https://github.com/zhaoxuandong001-alt/codex-usage-pill/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A tiny, unofficial Windows overlay that keeps your Codex usage remaining visible while you work.

![Codex Usage Pill preview](docs/screenshot.png)

## Features

- Shows the remaining percentage for the weekly Codex usage window.
- Remains visible when you work on another monitor, but stays below any window that covers Codex.
- Uses a compact `102 × 29` pixel pill that can be dragged anywhere inside the Codex window.
- Remembers its position between launches.
- Shows 5-hour and weekly windows, reset times, and last refresh time on hover.
- Changes from green to amber to red as remaining usage drops.
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

The app stores only the pill's last position in `%LOCALAPPDATA%\CodexUsagePill\position.txt`.

Automatic launch uses the Windows `Microsoft-Windows-AppModel-Runtime/Admin` event log to detect the stable Codex application identifier. No polling process runs in the background before Codex starts.

## Build from source

No package manager or external dependency is required. On Windows:

```powershell
git clone https://github.com/zhaoxuandong001-alt/codex-usage-pill.git
cd codex-usage-pill
.\build.ps1
```

The executable and checksum are written to `dist\`.

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
