"""Validate a pinned consumer without copying UI Kit runtime resources into Git."""
from pathlib import Path
import re
import hashlib
import json
import subprocess

ROOT = Path(__file__).resolve().parents[1]
plug = (ROOT / "plug.gd").read_text(encoding="utf-8")
pin = re.search(r'"commit": "([0-9a-f]{40})"', plug)
assert pin, "Pin the UI Kit to a complete Git SHA"
tracked = subprocess.check_output(["git", "ls-files", "-z"], cwd=ROOT).decode().split("\0")
assert not any(name.startswith("rookframe/ui/") for name in tracked), "Specimen must consume, not vendor, the UI Kit"
cache = ROOT / ".plugged/rookframe-ui-kit"
installed = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=cache).decode().strip()
assert installed == pin[1], "Reinstall the declared UI Kit revision with gd-plug"
count = 0
for directory in ("examples", "tests"):
    for path in (ROOT / directory).rglob("*"):
        if path.suffix not in {".gd", ".tscn"} or "exact-commit-consumer" in path.parts:
            continue
        for resource in re.findall(r'"res://([^"%]+\.(?:gd|tscn|tres|svg|png))"', path.read_text(encoding="utf-8")):
            assert (ROOT / resource).is_file(), f"Missing {resource} in {path.relative_to(ROOT)}"
            if resource.startswith("rookframe/ui/"):
                assert (ROOT / resource).read_bytes() == (cache / resource).read_bytes(), f"Installed resource differs from pin: {resource}"
            count += 1
manifest = json.loads((ROOT / "rookframe/ui/assets/generated-assets.json").read_text(encoding="utf-8"))
for asset in manifest["assets"]:
    assert hashlib.sha256((ROOT / "rookframe/ui" / asset["path"]).read_bytes()).hexdigest() == asset["sha256"], asset["path"]
print(f"{len(manifest['assets'])} generated-asset checksums verified")
print(f"UI Kit {pin[1]}; {count} resource references checked; no tracked runtime copy")
