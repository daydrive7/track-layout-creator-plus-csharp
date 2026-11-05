# TLC+ C#

**Version 1.0.0 — a native Windows port of TLC+ Python 1.3.0.**

Create and edit GT6 track layouts, adjust elevation and banking, place scenery decorations and export TED files. Python and a separate .NET installation are not needed to run the portable Windows build.

## Run

Download `TLCPlus-CSharp-v1.0.0-win-x64.zip` from this repository’s Releases page, extract the whole folder and open `TLCPlus.exe`. Keep `Assets` beside the executable. The separate EXE release asset also needs `TLCPlus-CSharp-v1.0.0-assets.zip` extracted beside it; the complete portable ZIP is recommended.

Windows x64 is supported. Preferences are saved in `config.json` beside the program, so use a writable folder.

## Features

- Track drawing, node properties, selection, undo/redo and geometric editing.
- Terrain contours, manual elevation, banking and an elevation/slope graph.
- Up to 2,000 decorations with the original scenery-specific artwork.
- Procedural generators, reference images and GPX/CSV/TED path imports.
- Dark/light themes, accent colors and custom panel/canvas colors.
- Nine languages under **File → Language**. Untranslated source strings fall back to English.
- Separate isometric preview and PNG/SVG/CSV export.

## Controls

Use **P** for Pen, **V** for Selection and **H** for Pan. Drag nodes to move them; double-click a node or press Enter on a selection to edit its properties. Shift extends a selection and Ctrl-drag snaps to the grid. The mouse wheel zooms; middle-drag pans. Right-drag a node to adjust radius. Ctrl+Z/Y undo and redo.

The decoration tool places items beside the road; drag an item to move it and right-click to remove it. The red position marker is active only while inspecting the elevation graph. Node numbers and property annotations can be toggled separately under **Map & Toggles**.

## File compatibility

TRK5 saves keep the original binary layout. Decorations and extra height settings are stored automatically in a neighboring `.trk5.tlcplus.json` companion because TRK5 has no fields for them. Keep that companion with the track to retain those settings in C#.

The included fixture suite compares 78 TED exports byte-for-byte against Python 1.3.0, with 156 fixture round trips. Additional private local files were checked during development but are not distributed. Opening an existing TED preserves its bytes; importing a TED as an editable path generates new geometry when exported.

This is a native reimplementation: UI, rendering and some interactions differ from Python. Random generators and image tracing are not guaranteed to produce identical point sets. In-game GT6 loading has not been validated by this test suite.

## Updates

**Check for Updates** looks for public stable C# releases in `daydrive7/track-layout-creator-plus-csharp`, compares their version to this application and offers to open the release page. It never installs automatically or checks the Python version as a substitute. Before the first public release, it reports that no public C# release is available. Startup checks can be disabled in Preferences.

## Build and verify

Install the .NET 10 SDK on Windows, then run:

```powershell
.\build.ps1
.\verify.ps1
```

`src/TLCPlus.Core` implements geometry and formats; `src/TLCPlus.Desktop` contains the Windows UI. `tests/fixtures` contains the Python reference cases. Verification writes reports under `analysis/`. The executable is self-contained and published with external assets. GitHub Actions builds and verifies the source, without publishing releases automatically.

## Credits and license

- **eran0004** — original Track Layout Creator.
- **daydrive7** — TLC+ Python and the C# port.

See [CONTRIBUTORS.md](CONTRIBUTORS.md) and [LICENSE](LICENSE). MIT permits modification, redistribution and commercial use, subject to retaining its copyright and license notice.
