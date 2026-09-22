# spo

Personal Spotify toolkit for Windows: a CLI (`spo`) and a daily top-list monitor (`spo-monitor`).
Successor to spoticli, which stays around as the legacy app.

## Layout

| Path | What |
|---|---|
| `src/core` | Config, Spotify auth and client, batching, monitor diff/report/SQLite store, DJ-mix ordering |
| `src/cli` | `spo.exe` - the command line |
| `src/monitor` | `spo-monitor.exe` - daily snapshot + Windows toast, run by a Scheduled Task |
| `tests/core.test` | NUnit tests for `core` |
| `scripts/deploy.ps1` | Publish and install the CLI as `spo2.exe` to `%LOCALAPPDATA%\Programs\spo` (renamed back to `spo.exe` once spoticli is retired) |
| `scripts/install-monitor.ps1` | Publish the monitor and register the daily Scheduled Task |

State lives in `%APPDATA%\spo` (`config.json`, `history.db`, `logs\`). The first time spo runs
without a config it copies the spoticli login and monitor history over; spoticli is left untouched.

## Getting started

```powershell
dotnet build spo.slnx
dotnet test spo.slnx
.\scripts\deploy.ps1
spo2 config --client-id <id> --client-secret <secret>   # or: spo2 config --from-env
spo2 login
spo2 top
.\scripts\install-monitor.ps1
```

The Spotify app needs `http://127.0.0.1:5000/callback` registered as a redirect URI.

## Conventions

These exist because spoticli drifted without them.

- **One verb per action.** Prefer `playlist import` over a `playlists` verb with a flag per mode.
  Options that change *how* a verb runs are fine; options that change *what* it does are a new verb.
- **Errors are exceptions.** Throw `SpoException` with a message that says what to do next. Only
  `Program.Main` turns exceptions into exit codes - nothing calls `Environment.Exit`.
- **Long options by default.** Short flags only where they mean the same thing in every verb:
  `-l` = `--limit`, `-r` = `--range`.
- **Shared option names mean one thing.** If a verb takes `--dry-run`, every write in it honours it.
  Parallelism is `--concurrency`; request sizes are never an option.
- **Respect Spotify's request limits** through `SpotifyLimits` + `Batching.ForEachChunkAsync`.
  Run concurrent calls with `Batching.MapAsync`, which returns results instead of writing to a
  shared collection.
- **Ask for the scopes you use.** Extend `SpotifyScopes.Default` when a command needs more.
