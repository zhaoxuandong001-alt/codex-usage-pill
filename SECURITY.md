# Security Policy

## Reporting a vulnerability

Please use GitHub's private vulnerability reporting for this repository. Do not open a public issue for a suspected security vulnerability.

Include the affected version, reproduction steps, expected impact, and any relevant logs with credentials and personal data removed.

## Security model

Codex Usage Pill starts the locally installed Codex CLI and communicates with its app-server over standard input/output. It does not directly read Codex authentication files, browser cookies, or tokens. It stores only the overlay position under `%LOCALAPPDATA%\CodexUsagePill`.

Release executables are not code-signed. Verify the published SHA-256 checksum or build from source before running the app.
