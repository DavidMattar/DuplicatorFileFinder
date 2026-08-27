# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

DuplicatorFinder is a Windows desktop app (C# / .NET 8 / WPF) that finds duplicate and
visually-similar files (exact duplicates of any type, similar images, similar videos) and
deletes copies safely (always to the Recycle Bin, never permanently). It's a from-scratch
clone of "Easy Duplicate Finder"'s feature set.

**Convention specific to this repo:** every class and method (public and non-trivial
private) gets an XML `///` doc comment explaining its purpose and, where relevant, which
design pattern it plays — this was an explicit user request, overriding the usual
no-comments default. Keep doing this for new code.

## Commands

```powershell
# .NET 8 SDK must be installed first (winget install --id Microsoft.DotNet.SDK.8), then
# open a new terminal — PATH doesn't refresh in an already-open shell after install.

dotnet build DuplicatorFinder.sln          # build everything
dotnet test DuplicatorFinder.sln           # run all tests (21 total, 2 test projects)
dotnet test tests\DuplicatorFinder.Core.Tests\DuplicatorFinder.Core.Tests.csproj --filter "FullyQualifiedName~ExactHashDetectorTests"   # run one test class
dotnet run --project src\DuplicatorFinder.App\DuplicatorFinder.App.csproj                # run the app
```

There's also `iaInstructions.md` at the repo root — a from-scratch setup runbook (SDK
install, build, troubleshooting table) written for an agent operating on a fresh machine
with no context. Point there for "how do I get this running" questions; this file is for
"how do I change the code" questions.

## Architecture

Four projects, wired together only in one place (see Composition root below):

- **`src/DuplicatorFinder.Core`** — pure logic, zero WPF/Win32/external-process
  dependencies. Models, `Abstractions/` (every cross-cutting interface), `Scanning/`
  (`FileScanner`), `Hashing/` (`FileHasher`, `ImageHasher`), `Detection/` (the three
  detectors + `Support/UnionFind`), `Engine/` (orchestration), `SmartSelect/`. Testable in
  isolation with `System.IO.Abstractions.TestingHelpers.MockFileSystem` and NSubstitute —
  no real disk or ffmpeg process needed.
- **`src/DuplicatorFinder.Infrastructure`** — the concrete, OS/process-dependent
  implementations of Core's interfaces: `Video/` (Xabe.FFmpeg-based frame extraction +
  first-run binary download), `Recycle/` (Windows Recycle Bin via
  `Microsoft.VisualBasic.FileIO`), `Settings/` (JSON file in `%LocalAppData%`).
- **`src/DuplicatorFinder.App`** — WPF, MVVM (CommunityToolkit.Mvvm). `App.xaml.cs` is the
  **single composition root**: every interface→implementation binding lives there and
  nowhere else. Navigation between the three screens (Setup/Progress/Results) is done by
  `MainViewModel` listening to plain C# events raised by the child ViewModels (no
  NavigationService/Messenger — deliberately, the flow is small and linear) and swapping
  `MainViewModel.CurrentViewModel`, which `MainWindow.xaml`'s per-type `DataTemplate`s
  render automatically.
- **`tests/DuplicatorFinder.Core.Tests`**, **`tests/DuplicatorFinder.Infrastructure.Tests`**
  — xUnit + FluentAssertions.

### The detection pipeline (`DuplicateScanEngine.RunAsync`, the Facade)

1. `IFileScanner` walks the configured folders once, producing `FileEntry` for every file
   passing the filters.
2. Each **enabled** `IDuplicateDetector` (Strategy pattern — `ExactHashDetector`,
   `ImageSimilarityDetector`, `VideoSimilarityDetector`) runs concurrently against only the
   subset of files relevant to it (`DuplicateScanEngine` partitions by extension).
3. Results merge into `DuplicateGroup`s; `ISmartSelectStrategy` (`DefaultSmartSelectStrategy`)
   picks which file in each group is "kept" before the UI ever sees the results.
4. Progress from every phase is funneled through one `ProgressAggregator` (turns
   per-phase progress into a single 0–1 global fraction) wrapped in one
   `ThrottledProgress<T>` (rate-limits UI updates) — this is why detectors report progress
   with a plain `IProgress<ScanProgress>?` and don't need to know about throttling/weighting themselves.
5. Adding a fourth detector means: implement `IDuplicateDetector`, register it in
   `App.xaml.cs`'s `ConfigureServices`, add its extension set + phase name(s) to the two
   dictionaries at the top of `DuplicateScanEngine`. Nothing else in the engine changes.

### Non-obvious decisions worth knowing before touching related code

- **`ImageSimilarityDetector` compares every pair O(n²)**, not via an indexed/banded
  lookup. A banded-LSH version was tried first and silently missed real near-duplicates
  (bit differences from resize/recompress are spread across all 64 bits, not clustered in
  one band) — caught by an end-to-end test with actually-encoded images, not the unit
  tests. Don't reintroduce banding without a real recall test against resized/recompressed images.
- **`SixLabors.ImageSharp` is pinned to `2.1.x`** in `DuplicatorFinder.Core.csproj`
  (currently 2.1.13) — `CoenM.ImageSharp.ImageHash` was built against `2.1.3`, and
  ImageSharp 3.x+ requires a commercial license for some usage. Don't bump this without
  confirming `CoenM.ImageSharp.ImageHash` compatibility first (reflect the actual installed
  API, don't assume from memory/docs — its API shape doesn't match most online examples,
  e.g. `Snippets` in `Xabe.FFmpeg` looks public but has an `internal` constructor).
- **`DuplicatorFinder.Infrastructure.csproj` explicitly references `System.Text.Json`
  9.0.0.** `Xabe.FFmpeg` 6.0.2's media-info parsing needs it at runtime and .NET 8's shared
  framework doesn't provide it by default; without this reference, any video scan throws
  `FileNotFoundException` for that assembly the first time media info is read. This was
  only caught by a real end-to-end run (real ffmpeg, real generated video) — the mocked
  `VideoSimilarityDetector` unit tests pass with or without this reference, since they
  never touch the real Xabe.FFmpeg/System.Text.Json code path.
- **Video detection is opt-in** (checkbox unchecked by default in `ScanSetupViewModel`):
  first use downloads ffmpeg/ffprobe (~70MB) to `%LocalAppData%\DuplicatorFinder\ffmpeg\`
  via `FfmpegBootstrap`, which is deliberately not triggered until the user asks for it.
- **Deletion only ever calls `WindowsRecycleBinService.SendToRecycleBinAsync`** — there is
  no permanent-delete code path anywhere in the app. Keep it that way unless explicitly asked.
- **Settings round-trip** happens in `ScanSetupViewModel`: loaded once in the constructor,
  saved on every `StartScan()` call (not on every property change) — so a value the user
  typed and then abandoned without scanning is never persisted.
