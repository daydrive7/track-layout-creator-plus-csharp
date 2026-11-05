# TLC+ C#

Track Layout Creator Plus for Windows, ported from TLC+ Python 1.3.0. Current version: **1.0.1**.

Create and edit GT6 track layouts, adjust elevation and banking, place scenery decorations and export TED files. Python and a separate .NET installation are not needed to run the portable Windows build.

## Run

1. Download [the complete portable ZIP](https://github.com/daydrive7/track-layout-creator-plus-csharp/releases/download/v1.0.1/TLCPlus-CSharp-v1.0.1-win-x64.zip).
2. Extract the whole folder to a writable location.
3. Open `TLCPlus.exe`. Keep the `Assets` folder beside it.

Windows x64 is supported. Preferences are saved in `config.json` beside the program. If you download the separate EXE instead, also extract `TLCPlus-CSharp-v1.0.1-assets.zip` beside it. Downloads, checksums and release notes are on the [Releases page](https://github.com/daydrive7/track-layout-creator-plus-csharp/releases).

## Getting started

Choose a scenery, draw a layout with the Pen tool, then use Selection to adjust nodes. Double-click a node to edit its coordinates, radius, elevation and banking. Save your editable layout as TRK5; export a TED file when you want the generated course.

Use the elevation graph to inspect height and slope, and the isometric preview to check the shape of the track. The procedural generators and path imports offer other starting points.

## Features

- Track drawing, node properties, selection, undo/redo and geometric editing.
- Terrain contours, manual elevation, banking and an elevation/slope graph.
- Up to 2,000 decorations with the original scenery-specific artwork.
- Procedural generators, reference images and GPX/CSV/TED path imports.
- Dark/light themes, accent colors and custom panel/canvas colors.
- Nine languages under **File → Language**. Untranslated source strings fall back to English.
- Separate isometric preview and PNG/SVG/CSV export.

## Road types

In **Geometry → Road Types**, choose a road style, click **Place road type**, then click the track. The complete highlighted span is inserted into the road table. Multi-part styles include their entrance, middle and exit meshes. Right-click a placed span to remove it; undo/redo and TRK5 companion saving retain placements. The orientation toggle uses the road orientation flag, not a proven left/right assignment.

Andalusia includes restaurant/parking, medic, building and forest styles; Eifel/Eifel Flat and Death Valley use their own documented road groups. Start/finish pieces, out-of-bounds placements and overlaps are rejected. If later geometry edits make a placement invalid, the base track stays visible and export is blocked until the placement is corrected or cleared.

For existing TEDs, single-part road types can be selected directly on the track or via **File → Edit TED road sections**. Exact section replacement preserves the other bytes and existing section lengths. Import the TED as a layout for complete multi-part stamps. The reference restaurant asset is nominally **150 m**, consistent with the supplied example; an older community text list incorrectly says 100 m.

This feature is **experimental in-game**: binary structure, road coverage, UUIDs and preservation are verified, but visual appearance and drivability have not been tested in GT6. The code catalog follows the [UUID reference](https://github.com/daydrive7/GT6-TPE-UUID/blob/4dbc4839cd0904568e93ff473e6df4ca92e56258/GT6_UUID_Reference.md), checked against shipped scenery UUIDs because that document's Eifel/Death Valley headings are swapped. Python is unchanged by this update.

## Controls

| Action | Control |
| --- | --- |
| Pen / Selection / Pan | P / V / H |
| Move a node | Drag |
| Edit node properties | Double-click, or Enter on a selection |
| Extend a selection | Shift |
| Snap a dragged node to the grid | Ctrl-drag |
| Zoom / pan | Mouse wheel / middle-drag |
| Adjust node radius | Right-drag a node |
| Undo / redo | Ctrl+Z / Ctrl+Y |

The decoration tool places items beside the road; drag an item to move it and right-click to remove it. The red position marker is active only while inspecting the elevation graph. Node numbers and property annotations can be toggled separately under **Map & Toggles**.

## File compatibility

TRK5 saves keep the original binary layout. Decorations, road-type placements and extra height settings are stored automatically in a neighboring `.trk5.tlcplus.json` companion because TRK5 has no fields for them. Keep that companion with the track to retain those settings in C#.

Opening an existing TED preserves its bytes; importing a TED as an editable path generates new geometry when exported. CSV and GPX imports are also available. CSV coordinates use metres and the columns `x,y,z,radius,camber,segments`; only `x` and `y` are required. Use a decimal point for numeric values.

The included fixture suite compares 78 TED exports byte-for-byte against Python 1.3.0 and checks 156 TRK5/TED round trips.

This is a native reimplementation: UI, rendering and some interactions differ from Python. Random generators and image tracing are not guaranteed to produce identical point sets. In-game GT6 loading has not been validated by this test suite.

## Updates

**Check for Updates** checks this repository for a newer stable release and offers to open its download page. Updates are installed manually. You can disable startup checks in Preferences.

## Build and verify

Install the .NET 10 SDK on Windows, then run these commands from the repository root:

```powershell
.\build.ps1
.\verify.ps1
```

The portable build is written to `release/TLCPlus-CSharp-v1.0.1/`. Open its `TLCPlus.exe`, or use `Start TLC+.cmd` from the repository root. Verification runs the format fixtures and desktop checks, with reports and preview images written to `analysis/`.

| Directory | Contents |
| --- | --- |
| `src/TLCPlus.Core` | Geometry, terrain, importers and file formats |
| `src/TLCPlus.Desktop` | Windows Forms editor and rendering |
| `tests/TLCPlus.Verify` | Format compatibility checks |
| `tests/fixtures` | Python 1.3.0 reference layouts and exports |

GitHub Actions builds and verifies pushes and pull requests. Release publishing uses a separate, manually started workflow. See [CONTRIBUTING.md](CONTRIBUTING.md) for the development workflow.

## Credits and license

- **eran0004** — original Track Layout Creator.
- **daydrive7** — TLC+ Python and the C# port.

See [CONTRIBUTORS.md](CONTRIBUTORS.md) and [LICENSE](LICENSE). MIT permits modification, redistribution and commercial use, subject to retaining its copyright and license notice.
