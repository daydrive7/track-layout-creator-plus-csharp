# TLC+ C# v1.0.2

Errors now appear as custom notices at the top of the canvas instead of blocking error popups.

- Smooth fade, fully dismissed after **3.5 seconds** by default.
- Choose **0.5–5 seconds** in **File → Preferences → Error Notices**; the setting is saved automatically.
- Notices keep keyboard focus and let clicks pass through to the canvas. Repeated errors replace the current notice and restart its timer.
- Notices follow window movement and resizing, use the current theme, and include translated preferences in all nine interface languages.
- Retained verification reports and corrected the intermittent road-placement check's pixel-boundary selection.

TRK5/TED compatibility and road-type handling are unchanged. Python and Web are unchanged by this C# update.

Download the complete win-x64 ZIP and extract all files. Keep Assets beside TLCPlus.exe. The separate EXE download requires the assets ZIP. Source and SHA256 checksums are provided. MIT licensed.
