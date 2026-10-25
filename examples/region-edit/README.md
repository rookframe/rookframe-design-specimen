# RFG-401 native Region editing mock

A disposable Godot specimen for [RFG-401](https://linear.app/rookframe/issue/RFG-401).
Run this scene to review the proposed interaction; it is not a production patch.
Decisions and visual review belong in that issue.

## Run

```sh
./examples/region-edit/run.sh --resolution 1440x810
```

Stock Godot 4.7.2 Mono, .NET SDK 10.0.401, Python 3, Git and Git LFS are required.
`GODOT_BIN` / `DOTNET_BIN` override installed executable paths. The default Godot
path is `/Applications/Godot_mono.app/Contents/MacOS/Godot`. The bootstrap uses the
application repository's pinned .NET SDK when present. `REGION_GODOT`,
`REGION_UI_KIT`, and `REGION_SDK` select local source repositories; otherwise it
uses `~/Projects/rookframe-*` or fetches those repositories from GitHub.

`sources.json` pins the exact application, UI Kit and SDK commits. `prepare.py`
exports those commits into ignored `.region-edit-runtime`, hydrates their LFS
assets and adds only the specimen scene/adapters. Production source and assets
remain unchanged. The temporary C# project excludes production test compilation.
No World, package installation, credentials, networking or persistence runs here.
All room geometry and history disappear when the scene closes.

## Review controls

Select a room through its existing cube marker, or a connected Building through
its house marker. Drag the cave arrows to move; drag the vertical double arrow
to lift; drag the cave rotation handle beside the room to rotate. The existing
side chevrons change foundation thickness and room height. A stacked room has
its height chevron, planar arrows and rotation handle; it has no independent lift.
Lifting the demonstrated base wing carries its upper room by the same amount.

Click a wall to reveal an insertion point, then drag that point to add an anchor
and create a corner. Drag the wall itself to pull the whole wall. The anchor uses
the application's authored corner handle, including its selected ring.

After any change, the bottom HUD shows Undo, Redo, Delete, Cancel and Commit.
Undo/Redo step through the pending changes. Delete removes the selected room
from the preview. Cancel restores the scene before the edit; Commit saves one
local history step. Enter also commits a valid edit; Escape cancels it.
Ctrl/Cmd-Z and Shift-Ctrl/Cmd-Z undo and redo. Red interior overlap disables
Commit, including Enter. Passing through an invalid position while dragging is
allowed. Shared walls retain separate rooms; detachment creates another group.

The secondary rail's rectangle opens the real drawing HUD. Drag to draw a
rectangle, circle or ellipse. Valid footprints open the real Look picker, including
hover/tap previews and the existing desktop/phone acceptance behavior. Invalid
footprints show their overlapping area in red. Click the active drawing or painting
rail icon again to return to Builder root without leaving Builder mode.

The numbered keys load isolated review cases, resetting local history:

| Key | Case |
| --- | --- |
| 1 | Adjacent rooms; move, rotate, lift, height and anchor interactions |
| 2 | Rotated overlap; Commit blocked |
| 3 | Raised wing carrying the upper room; New storey preview |
| 4 | Selected stacked room; no independent lift |
| 5 | New corner created by pulling a wall anchor |
| 6 | New construction overlapping existing rooms; creation blocked |
| 7 | Whole-Building movement with a separate neighboring Workshop |

These keys are specimen controls, not proposed product shortcuts. The native
window title identifies the mock. No review harness is added inside the product UI.

## Baseline and limits

The real RFG-404 Development World and authored application scenes were inspected.
The mock instances current TabletopWorkspace rails, RegionBrushHud, PaintingHud,
RegionLookPicker, FoundationHandle, RegionBrushOverlay and the production RoomShell
renderer. The foreground scene is temporary architectural content, not a saved
World screenshot. Existing UI Kit and application assets retain their upstream
licenses and provenance. No Props are modeled or placed.

Move arrows use CavePlanVisual's actual mesh builder and camera sizing. Rotation
uses its actual Sprite3D artwork, placement behavior and camera sizing; it is a
world handle, not a floating button. The edit HUD reuses the production frame and
NavigationGlyph controls. Room/building membership and Storey feedback are local
illustrations of the agreed rules, not the production detector or physics system.

Unrelated rails and painting actions are visibly disabled. Polygon drawing is
explicitly disabled in this specimen; the existing feature is not redesigned.
This scene does not demonstrate furnished contents, Portal reconciliation, Undo
across sessions, network publication, arbitrary support graphs, internal wall
rearrangement, or a complete production Storey detector. Foundation and height
controls exercise simple rectangular examples. The mock is not validation for
production geometry or physics.

## Captures and checks

Pass `-- --state NAME --capture NAME --quit-after-capture` after the Godot options.
States are `room`, `overlap`, `lift`, `stacked`, `anchor`, `drawing`, `building`.
`--long` adds long multilingual room names. F9 captures the current state.
`REGION_EVIDENCE` selects the absolute output directory, default
`/tmp/rfg401-region-edit`. Each PNG has a companion JSON state/handle record.
Captures and logs stay outside source control.

Review at 1920×1080, 1024×768 and 844×390. The native drawing/Look flow adapts to
landscape-phone dimensions; desktop pointer review does not establish physical
touch usability. The repository's ordinary specimen check remains
`godot --headless --path . --script res://tests/specimen.gd` after installing the
pinned UI Kit and importing the root project.
