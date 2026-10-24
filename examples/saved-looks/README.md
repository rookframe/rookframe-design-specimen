# RFG-404 native interaction mock

Throwaway specimen. Run from this branch, not from the production application.
Decisions and review feedback belong in [RFG-404](https://linear.app/rookframe/issue/RFG-404).

## Launch

```sh
./examples/saved-looks/run.sh
```

Requires stock Godot 4.7.2 Mono, .NET SDK 10.0.401, Python3, Git and Git LFS.
`GODOT_BIN` and `DOTNET_BIN` select installed executables. On this host the default
Godot path is `/Applications/Godot_mono.app/Contents/MacOS/Godot`; the bootstrap
finds the repository's pinned .NET installation. Exact Git sources are listed in
`sources.json`; they are Git commits, not published releases.

The bootstrap exports the exact application, UI Kit and SDK sources to ignored
`.saved-looks-runtime`. It hydrates Git LFS assets, preserving their upstream
licenses and provenance. It uses local `~/Projects/rookframe-*` repositories when
available and otherwise clones the corresponding repositories. `LOOKS_GODOT`,
`LOOKS_UI_KIT` and `LOOKS_SDK` can select source repository paths.
The first import prepares the host assets; subsequent starts reuse the cache.

The standalone scene runs the actual Manager, wheel, copy swatch, HUD and tool
rail resources. `AppearanceManagement.Mock.cs` adds the proposed Looks views.
A single prototype adapter in `RegionLook.Load` resolves in-memory saved Looks;
no application World, Package installation, credentials, storage or network runs.
The tabletop is a flat synthetic surface using existing appearance resources.

## Routes

- Builder rail: Rooms opens drawing controls; palette opens Painting.
- Click a shape to supply a provisional footprint and open the Look strip.
  Hover or keyboard-focus previews; clicking or Enter creates it. Escape/cross
  cancels. Undo, redo and delete exercise local fixture history.
- Painting → Copy → room/building icon copies a complete Look. Clicking the floor
  copies an atomic finish. The top-right swatch has Clear; unsaved Looks also have
  Save. Save opens the Manager with the copied components.
- Painting's book opens the Manager. Looks supports source groups, search,
  categories, composition, component selection, editing, pinning and deletion.
  The category pencil opens category settings and membership controls.
  Built-in Looks can be inspected and copied, not edited.
- The component chooser instances the UI Kit miniature gallery. Wall cards reuse
  the painter’s assembled 3D sample, including the full-height Surface and Top placement. Floor, ceiling and foundation cards use
  a stock SphereMesh with the actual surface material, in the application's
  existing 3D preview world. Native search, selection and paging remain available;
  choosing a card returns to the Look, while Back preserves the draft.
- Painting → Paint → room/building icon opens the unchanged wheel.

All state resets when the mock closes. No 3D room gesture is simulated.
Unrelated toolbar routes are disabled. The mock is not a production patch.

## Render and inspect

Pass Godot arguments to `run.sh`, for example `--resolution 844x390`.
After `--`, fixture flags include `--manager`, `--copy`, `--strip`, `--compose`,
`--edit`, `--saved-copy`, `--long`. The component routes are `--picker-wall`,
`--picker-floor`, `--picker-ceiling` and `--picker-foundation`. `--long` adds long
multilingual component captions; `--reduced-motion` disables gallery page motion. `--saved-copy --manager` reproduces the saved
copy collection state used for the Manager review.

`--capture NAME --quit-after-capture` captures the native viewport after setup;
`LOOKS_EVIDENCE` chooses an absolute evidence directory (default
`/tmp/rfg404-looks`). F9 captures the current state. F8 toggles long strip names.
Keep all captures and logs outside source control. Review desktop1920×1080,
tablet1024×768 and phone844×390, and exercise pointer/keyboard routes in the app.
The ordinary specimen checks are documented in the repository README.
