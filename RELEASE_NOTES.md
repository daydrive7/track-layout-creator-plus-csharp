# TLC+ C# v1.0.1

Adds experimental click-to-place **Road Types**, including Andalusia restaurant/parking roads, plus scenery-specific complete road chains. Find it in Geometry, select a type and click the track. Right-click removes a span. Undo/redo and companion metadata retain placements.

- 26 audited recipes / 33 scenery choices; full entrance–middle–exit chains.
- Protects start/finish, rejects overlaps and invalid scenery/flags/bounds.
- Exact single-section editing for existing TEDs, preserving unrelated bytes.
- Corrected restaurant nominal length (150 m), narrow middle lengths and swapped scenery headings in the external reference.
- Existing exports without road placements remain byte-identical to the Python fixtures.

**Experimental game behavior:** file structure and UUID insertion are tested; GT6 appearance and drivability still need in-game validation. This update targets C# and Web. Python is unchanged.

Download the complete win-x64 ZIP and extract all files. Keep Assets beside TLCPlus.exe. The separate EXE download requires the assets ZIP. Source and SHA256 checksums are provided. MIT licensed.
