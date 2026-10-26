#!/usr/bin/env python3
"""Create an ignored native host snapshot; production resources remain byte-exact."""
import argparse, hashlib, io, json, os, pathlib, shutil, subprocess, tarfile
HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
RUNTIME = ROOT / '.region-edit-runtime'
PINS = json.loads((HERE / 'sources.json').read_text())

def run(*args, **kwargs):
    return subprocess.check_output(args, **kwargs)

def repository(name):
    override = os.environ.get('REGION_'  + name.upper().replace('-', '_'))
    candidate = pathlib.Path(override) if override else pathlib.Path.home() / 'Projects' / ('rookframe-' + name)
    if candidate.is_dir():
        return candidate
    candidate = ROOT / '.region-edit-cache' / name
    candidate.parent.mkdir(parents=True, exist_ok=True)
    if not candidate.exists():
        subprocess.run(['git', 'clone', '--filter=blob:none', 'https://github.com/rookframe/rookframe-' + name + '.git', str(candidate)], check=True, env={**os.environ, 'GIT_LFS_SKIP_SMUDGE':'1'})
    return candidate

def export(repo, commit, destination, subtree=None):
    present = subprocess.run(['git', '-C', str(repo), 'cat-file', '-e', commit + '^{commit}'],
                             stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0
    bundle = HERE / 'application-fix.bundle'
    if not present and commit == PINS['application'] and bundle.is_file():
        # Source archives carry the unpublished renderer fix as a verified Git
        # bundle. It imports objects into the source cache without changing its checkout.
        subprocess.run(['git', '-C', str(repo), 'bundle', 'verify', str(bundle)], check=True)
        subprocess.run(['git', '-C', str(repo), 'fetch', str(bundle),
                        'refs/heads/codex/rfg-401-wall-joins'], check=True)
    args = ['git', '-C', str(repo), 'archive', commit]
    if subtree:
        args.append(subtree)
    data = run(*args)
    destination.mkdir(parents=True, exist_ok=True)
    with tarfile.open(fileobj=io.BytesIO(data)) as archive:
        archive.extractall(destination, filter='data')
    lfs = pathlib.Path(run('git', '-C', str(repo), 'rev-parse', '--git-common-dir', text=True).strip())
    if not lfs.is_absolute():
        lfs = repo / lfs
    pointers = []
    for f in destination.rglob('*'):
        if f.is_file() and f.stat().st_size < 300:
            raw = f.read_bytes()
            if raw.startswith(b'version https://git-lfs.github.com/spec/v1'):
                oid = raw.decode().split('oid sha256:')[1].splitlines()[0]
                pointers.append((f, oid))
    missing = [oid for _, oid in pointers if not (lfs / 'lfs/objects' / oid[:2] / oid[2:4] / oid).exists()]
    if missing:
        subprocess.run(['git', '-C', str(repo), 'lfs', 'fetch', 'origin', commit], check=True)
    for f, oid in pointers:
        source = lfs / 'lfs/objects' / oid[:2] / oid[2:4] / oid
        shutil.copyfile(source, f)

marker = RUNTIME / 'sources.json'
if not marker.exists() or json.loads(marker.read_text()) != PINS:
    if RUNTIME.exists():
        shutil.rmtree(RUNTIME)
    export(repository('godot'), PINS['application'], RUNTIME)
    export(repository('ui-kit'), PINS['ui_kit'], RUNTIME / 'rookframe', 'rookframe/ui')
    export(repository('sdk'), PINS['sdk'], RUNTIME / 'rookframe', 'rookframe/sdk')
    marker.write_text(json.dumps(PINS))
project = RUNTIME / 'rookframe'
# Use the repository's pinned SDK when present; no SDK substitution.
sdk = repository('godot') / '.dotnet'
if not sdk.exists():
    sdk = pathlib.Path.home() / 'Projects/rookframe-godot/.dotnet'
if sdk.exists() and not (RUNTIME / '.dotnet').exists():
    (RUNTIME / '.dotnet').symlink_to(sdk, target_is_directory=True)
# Only the host's test/development compilation is excluded; all UI implementation is original.
csproj = project / 'Rookframe.csproj'
text = run('git', '-C', str(repository('godot')), 'show', PINS['application'] + ':rookframe/Rookframe.csproj', text=True)
import re
text = re.sub(r'  <ItemGroup Condition="[^\n]*Debug[^\n]*">.*?</ItemGroup>', '', text, flags=re.S)
text = text.replace('  <Import Project="tests/TestTimeouts.targets" />', '')
text = text.replace('</Project>', '<ItemGroup><Compile Remove="tests/**/*.cs;addons/**/*.cs" /></ItemGroup></Project>')
csproj.write_text(text)
shutil.copyfile(HERE / 'project.godot', project / 'project.godot')
target = project / 'prototypes/region-edit'
target.mkdir(parents=True, exist_ok=True)
for f in HERE.iterdir():
    if f.suffix in ('.cs', '.tscn', '.gd', '.tres'):
        shutil.copyfile(f, target / f.name)
print(project)
