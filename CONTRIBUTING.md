# Contributing

Bug fixes, documentation corrections and translation improvements are welcome. For a larger change, open an issue first so its scope can be discussed.

## Setup

Use Windows x64 with the .NET 10 SDK. Clone the repository and run these commands from its root:

```powershell
git clone https://github.com/daydrive7/track-layout-creator-plus-csharp.git
cd track-layout-creator-plus-csharp
.\build.ps1
.\verify.ps1
```

The built application is in `release/TLCPlus-CSharp-v1.0.0/`. Keep its `Assets` folder beside `TLCPlus.exe`. Build output, local preferences and verification reports are ignored by Git.

## Making changes

Keep each pull request focused on one problem. Use names that describe the code's purpose and comments that explain assumptions, units or compatibility requirements. Preserve the existing credits and license notices.

Geometry and file formats live in `src/TLCPlus.Core`; the Windows UI and renderers live in `src/TLCPlus.Desktop`. Translation catalogs are under `src/TLCPlus.Core/Assets/locales`. Keep translation keys intact and check changes in the application for clipping or missing text.

TRK5/TED compatibility is covered by reference files in `tests/fixtures`. For a format or geometry fix, include a regression case and explain any expected byte differences. Keep the TRK5 companion metadata behavior in mind when changing decoration or height settings.

## Validation

Run `build.ps1` and `verify.ps1` before submitting code changes. Verification checks TED exports against the Python references, TRK5/TED round trips and desktop behavior. Reports and rendered previews go to `analysis/`.

To run only the format checks:

```powershell
dotnet run --project tests/TLCPlus.Verify -c Release -- .
```

For UI changes, also open the app and exercise the affected controls. Include a screenshot when it helps explain the result. Do not commit generated builds, reports, personal track files or `config.json`.

## Issues and pull requests

For a bug report, include the application version, Windows version, steps to reproduce and what you expected to happen. Attach a small sample layout if the problem depends on a file you can share.

In a pull request, describe the problem, the resulting behavior and the checks you ran. Mention any remaining limitations, including whether a format change has been tested in GT6. Contributions use the project's [MIT license](LICENSE).
