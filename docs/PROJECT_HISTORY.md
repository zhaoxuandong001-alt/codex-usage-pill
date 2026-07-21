# Project history and design decisions

## The original problem

Codex usage remaining was available, but checking it repeatedly through a status area interrupted normal work. The goal became a tiny indicator that stayed visible in the regular Codex desktop interface without modifying Codex itself.

## Why an overlay

Injecting code into the Codex application or modifying installed resources would be fragile and unsafe across updates. A separate Windows overlay keeps the boundary clear: Codex remains untouched, while the pill follows its window and disappears when Codex is not active.

## How usage is read

Several approaches were considered. Browser cookies and direct authentication-file parsing were rejected because they would expose sensitive credentials. The tool instead invokes the installed Codex CLI and uses its local app-server protocol to request `account/rateLimits/read`. Authentication remains owned by Codex.

## UI iterations

The first versions proved the data path and window tracking. Later iterations added:

1. drag-to-position with persisted offsets;
2. tray controls and single-instance behavior;
3. green, amber, red, and unavailable states;
4. hover details for both short and weekly windows;
5. foreground-only visibility at first, later replaced with Codex-adjacent Z-order so it remains visible on another monitor without floating over windows that cover Codex;
6. per-monitor DPI handling after Windows scaled an intended `102 × 29` layout to `136 × 39`;
7. a final physical size of `102 × 29` pixels, matching the compact reference design;
8. top-level window enumeration after a Codex update added a small auxiliary window that could otherwise capture the pill's position.

## Typography

Local inspection confirmed that the Codex product name uses OpenAI Sans in the current desktop application. The personal build could load that locally available asset, but the public repository intentionally does not redistribute it. The open-source build uses `Segoe UI Semibold`, the Windows-native fallback that remains compact and visually consistent.

## Public-release boundary

The release contains one C# source file, one PowerShell build script, and no external runtime packages. It does not include OpenAI application resources, user credentials, generated protocol schemas, test artifacts, or private filesystem paths.

## Current limitations

- Windows only.
- Expects the current Windows Codex desktop process and CLI installation layout.
- Relies on a local Codex app-server method that may evolve with future Codex releases.
- Unsigned binaries can trigger Windows SmartScreen reputation warnings.
