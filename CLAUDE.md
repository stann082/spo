# spo - notes for Claude

Read `README.md` first for the layout and the coding conventions. This file covers how to work in
the repo and the traps already stepped in.

## Build, test, run

```powershell
dotnet build                    # CLI lands in artifacts\build (no net10.0 subfolder)
dotnet test
.\artifacts\build\spo.exe ...   # try a change against the real app
```

- Only `dotnet publish` is self-contained / single-file (`_IsPublishing` conditions in
  `src/cli/cli.csproj` and `src/monitor/monitor.csproj`), and it builds outside `artifacts\build`.
  Do not make plain builds self-contained or let publish write into `artifacts\build`: a copy of
  the runtime left there stops the framework-dependent dev exe from starting ("You must install
  or update .NET", exit 150).
- `scripts\deploy.ps1` installs the CLI as `spo2.exe` while the legacy spoticli app still owns
  `spo.exe`; the name is the single `$ExeName` variable there. Messages inside the app say `spo`.
- CI (`.github/workflows/build.yml`) builds and tests on Windows. After a push, watch it:
  `gh run watch <id> --exit-status`.

## Code

- Lowercase namespaces (`core.playlists`, `cli.commands`), `#region` blocks, `Nullable` disabled.
- Logic that can be tested without Spotify goes in `core` with NUnit tests in `tests/core.test`
  (e.g. `SplitPlan`, `PlaylistImportResult`, `TrackRecord`). The CLI layer has no tests; verify it
  by running the exe.
- User-facing failures throw `SpoException`; only `Program.Main` maps them to exit codes.
- Request sizes come from `SpotifyLimits` via `Batching.ForEachChunkAsync`; concurrency via
  `Batching.MapAsync`. Never hardcode 50/100 in a command.
- Look up SpotifyAPI.Web signatures by reflecting over
  `~\.nuget\packages\spotifyapi.web\7.0.0\lib\netstandard2.1\SpotifyAPI.Web.dll`; the XML docs
  miss most model properties.

## Running against Spotify

The exe acts on the owner's real account (`%APPDATA%\spo\config.json`).

- Read-only commands (`playlists`, `--show-tracks`, `top`, `tracks`) are fine to run freely.
- Anything that writes (`--create`, `--add`, `--split`, `--move`, `--remove`, `--mark-orphans`): run with `--dry-run`
  first, show the plan, get approval, run it, then verify by reading the result back.
- `trackCount` in `playlists --format json` lags for newly created playlists; count tracks with
  `--show-tracks` instead.
- On first run without `%APPDATA%\spo\config.json` the app imports the legacy spoticli login and
  history (`LegacyImport`). Don't trigger that by accident on a fresh machine.

## Playlist work

1. Export: `spo playlists -q "<name>" --show-tracks --show-genres --format json`.
2. Write the plan file (`--create` / `--add` / `--split` / `--move` / `--remove` format, see `--help`) to a scratch
   folder, never the repo.
3. `--dry-run`, check every search match (album, year, id), swap bad matches for ids.
4. Run, then read back and compare counts.

Data caveats:

- `album.year` is the album's release, so compilations, remasters and re-recordings show a later
  year than the song. `albumType: compilation` flags some of them, not all; the ISRC year is not
  the song's year either.
- Spotify genres are per artist and coarse inside a scene (most industrial acts share the same
  five tags); good for rough sorting across very different music, not for sub-genres.
- Audio features (tempo, key, energy) are not available to this app.
- Search takes the first hit; it can be a remix or a compilation cut, so check the printed match.
