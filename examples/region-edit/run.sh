#!/bin/sh
set -eu
HERE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
GODOT_BIN=${GODOT_BIN:-/Applications/Godot_mono.app/Contents/MacOS/Godot}
python3 "$HERE/prepare.py"
RUNTIME="$HERE/../../.region-edit-runtime/rookframe"
"$GODOT_BIN" --headless --editor --path "$RUNTIME" --import --quit
if [ -z "${DOTNET_BIN:-}" ]; then
    DOTNET_BIN=dotnet
    if [ -x "$RUNTIME/../.dotnet/dotnet" ]; then DOTNET_BIN="$RUNTIME/../.dotnet/dotnet"; fi
fi
(cd "$RUNTIME" && "$DOTNET_BIN" build Rookframe.csproj --nologo --verbosity quiet -p:RunAnalyzers=false)
exec "$GODOT_BIN" --path "$RUNTIME" "$@"
