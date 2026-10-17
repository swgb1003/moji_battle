"""Record/verify the working-tree sources and glyph assets used for this release."""
from pathlib import Path
import argparse
import hashlib
import json
import subprocess

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('report')
parser.add_argument('--check', action='store_true')
args = parser.parse_args()
paths = set()
for directory in ['Assets/Scripts', 'Assets/Tests', 'Assets/Resources/Glyphs', 'Assets/Art/Glyphs/Baked', 'Server']:
    for path in (root / directory).rglob('*'):
        if path.is_file() and path.suffix in {'.cs', '.csv', '.png', '.json', '.asset', '.mjs'} and 'node_modules' not in path.parts:
            paths.add(path)
report = {'gitHead': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip(),
          'sourceType': 'working-tree',
          'files': {path.relative_to(root).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted(paths)}}
destination = root / args.report
if args.check:
    previous = json.loads(destination.read_text(encoding='utf-8'))
    assert previous == report, 'Sources changed after the release snapshot.'
else:
    destination.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(f"{'Verified' if args.check else 'Recorded'} {len(paths)} source/glyph files.")
