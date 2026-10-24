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
Prototype adapters in `RegionLook` resolve in-memory saved Looks and add the
fifth Exterior walls role to the pinned single-wall recipe;
no application World, Package installation, credentials, storage or network runs.
The tabletop is a flat synthetic surface using existing appearance resources.

## Routes

- Builder rail: Rooms opens drawing controls; palette opens Painting.
- The drawing HUD uses the painting HUD’s single native frame and internal divider:
  shapes in one section, undo/redo/delete in the other. Click a shape to supply a
  provisional footprint and replace the HUD with the Look picker.
- Desktop uses a continuous shelf with category tabs and real 3D Region thumbnails.
  Hover or keyboard-focus previews; clicking or Enter creates the region. The shelf
  has native previous/next controls when more Looks are available. No instruction row.
- Landscape mobile uses a right-edge palette with a category selector and two Looks
  per page. Tap a Look to preview it, then use the checkmark to create. Changing page
  or category clears the tentative choice; Cancel discards the provisional footprint.
  The builder/right rails hide while this palette is open. Escape/cross cancels on
  either layout. Creation restores the drawing HUD; undo/redo/delete use local history.
- Painting → Copy → room/building icon copies all five Look components, including Foundation. Clicking the floor
  copies an atomic finish. The top-right swatch has Clear; unsaved Looks also have
  Save. Save opens the Manager with the complete copied recipe. Recognition compares
  all five component Content References against the existing Look collection, ignoring
  names, categories and pins. A match hides Save and the extra copied-Look entry in
  Manager; the swatch and Clear remain. A Foundation-only difference is not a match.
  A recognized copy shows the matching Look's name below the swatch; an unsaved Look
  has no caption. Atomic copies show their Content name. The existing scope badge
  identifies the copied subject, presented as a small attached brass medallion using
  the native palette rim/leather textures and unchanged subject glyph. Generic
  “Room style”/“Building style” captions are not used.
- Painting's book opens the Manager. Looks supports source groups, search,
  categories, composition, component selection, editing, pinning and deletion.
  The category pencil opens category settings and membership controls.
  Built-in Looks can be inspected and copied, not edited.
  The Look gallery, copied-Look entry and mobile Look rows show actual 3D Regions,
  using the drawing strip's fixed rectangular cutaway, camera and lighting. Names
  remain beneath gallery thumbnails; source grouping and existing inspection actions
  are retained. Orbit and shape controls remain in the Look editor.
  An unsaved copied Look is the first desktop gallery card, with its Save icon
  beside the caption. Mobile keeps a compact preview/name/Save group. Both disappear once
  the copied recipe matches a saved Look; there is no separate status sentence.
- The component chooser instances the UI Kit miniature gallery. Wall cards reuse
  the painter’s assembled 3D sample, including the full-height Surface and Top placement. Floor, ceiling and foundation cards use
  a stock SphereMesh with the actual surface material, in the application's
  existing 3D preview world. Native search, selection and paging remain available;
  the gallery stays beside the Region and selecting a card updates the preview immediately. Search and page stay in place while trying choices.
- Looks contain Interior walls, Exterior walls, Floor, Ceiling and Foundation.
  The two wall slots use the same gallery independently. Legacy single-wall Looks
  initialize both slots from that style. Copy reuses the native prevailing-style
  rule separately for Region-relative interior and exterior faces. Room/Building
  Copy retains Foundation and copy-paint applies its material to the destination. Application
  writes the existing semantic face appearances. The default edited Look uses
  timber inside and rough stone outside to make the distinction inspectable.
- The Look editor keeps the Region preview on the left and the selected role’s gallery on the right. Seven icon-only role selectors sit below the preview, with tooltips naming the role and current choice. Columns and Brackets exercise the future-role empty states; the pinned source has no style collections for them. They do not change the five-slot recipe or its validation. The local `inline_gallery.gd` adapter changes only split-pane sizing; cards, search, selection and paging remain the native miniature browser.
- The Look editor previews an actual small Region built by the production
  RoomShell renderer. Rectangle/circle buttons change only the inspection shape.
  Drag the preview (or focus it and use arrow keys) to orbit. The ceiling button
  switches between the native cutaway and the complete enclosure, so all five
  selected components can be inspected. Selecting Interior walls/Floor opens the
  cutaway; Exterior walls/Ceiling exposes the enclosure. Shape, orbit and manual
  inspection state survive gallery choices. Draft Looks show a placeholder until
  all five components are chosen.
- Painting → Paint → room/building icon opens the unchanged wheel.

All state resets when the mock closes. No 3D room gesture is simulated.
Unrelated toolbar routes are disabled. The mock is not a production patch.

## Render and inspect

Pass Godot arguments to `run.sh`, for example `--resolution 844x390`.
After `--`, fixture flags include `--manager`, `--copy`, `--strip`, `--compose`,
`--edit`, `--saved-copy`, `--foundation-copy`, `--copy-building`, `--long`. Add `--region-circle` or `--preview-ceiling`
to `--edit` to open those preview states. The component routes are `--picker-wall`,
`--picker-exterior_wall`, `--picker-floor`, `--picker-ceiling`, `--picker-foundation`, `--picker-columns` and `--picker-brackets`. `--long` adds long
multilingual component captions; `--reduced-motion` disables gallery page motion. `--saved-copy --manager` reproduces the saved
copy collection state through the actual Copy route. `--foundation-copy` supplies a
saved recipe differing only in Foundation, so Save remains available. `--copy-building`
uses the ordinary Building icon pickup route. Captures include component references
and recognition/visibility state in their companion JSON files.

`--capture NAME --quit-after-capture` captures the native viewport after setup;
`LOOKS_EVIDENCE` chooses an absolute evidence directory (default
`/tmp/rfg404-looks`). F9 captures the current state. F8 toggles long strip names.
Keep all captures and logs outside source control. Review desktop1920×1080,
tablet1024×768 and phone844×390, and exercise pointer/keyboard routes in the app.
The ordinary specimen checks are documented in the repository README.
