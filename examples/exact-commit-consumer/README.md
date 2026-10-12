# Exact-commit consumer proof

This is a clean, normal Godot 4.7 project. It does not use repository-specific
copy tooling. Its checked-in `plug.gd` pins UI Kit commit
`839da122c44a231a70cc608a9fc121514fe64aa3` and includes only
`rookframe/ui`, so examples and development material are not installed.

Materialize the dependency:

```sh
/Applications/Godot_mono.app/Contents/MacOS/Godot \
  --headless -s plug.gd install
```

Expected public resources include:

- `res://rookframe/ui/theme/rookframe_theme.tres`;
- `res://rookframe/ui/tokens.gd`;
- `res://rookframe/ui/catalogue.json` and the public component scenes;
- licensed fonts and semantic frame assets; and
- `res://rookframe/ui/icons/manifest.json` plus 62 semantic SVGs.

Open the project in Godot and browse the installed subtree in the FileSystem
dock. `consumer.tscn` instances the public Search Field from its canonical
path and applies the installed Theme to ordinary `Label`, `LineEdit`,
`CheckBox`, `Button`, `ProgressBar`, Container, and `TextureRect` nodes. A
Package author can edit the Search Field's exported properties in the
Inspector and use ordinary scene connections for its `value_changed` and
`cleared` signals; the checked-in connections update the status copy. The
installed Theme can also be selected as the project Theme to preview its
variations across author-owned scenes.

The materialized `rookframe/ui` directory and `.plugged` bookkeeping are
ignored deliberately: the declaration and full commit are the reproducible
input. Package build work must also exclude the materialized UI Kit from the
Package artifact; that lifecycle proof is outside the UI Kit source package.

The vendored gd-plug runtime is pinned to
`209276d1f00d14b49b74403d9839f29598e9a8eb`, the Godot 4 branch revision with
the Godot 4.7 process-loop fix. The declaration overrides `request_quit` so
gd-plug's asynchronous success signal returns status zero when it omits an exit
code on Godot 4.7.
