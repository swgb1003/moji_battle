"""Check enclosed spaces in known box components in Unity's actual split masks."""
from pathlib import Path
from collections import deque
import json
from PIL import Image

root = Path(__file__).resolve().parents[1]
rows = [line.split(',') for line in (root / 'Assets/Resources/Glyphs/SplitKanji.csv').read_text(encoding='utf-8').splitlines()
        if line and not line.startswith('#')]
expected = {'田': 4, '日': 2, '目': 3, '白': 2, '口': 1}
without_counters = {'木', '火', '丁', '氵', '糸', '心', '力', '十', '艹', '立', '生', '水'}
checks, failures = [], []
def count_holes(path):
    img = Image.open(path).convert('L')
    w, h = img.size
    ink = [value >= 128 for value in img.tobytes()]
    visited, holes = set(), 0
    for start in range(w * h):
        if ink[start] or start in visited:
            continue
        queue, area, border = deque([start]), 0, False
        visited.add(start)
        while queue:
            p = queue.popleft()
            x, y = p % w, p // w
            area += 1
            border |= x == 0 or y == 0 or x == w - 1 or y == h - 1
            for nx, ny in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
                n = ny * w + nx
                if 0 <= nx < w and 0 <= ny < h and not ink[n] and n not in visited:
                    visited.add(n)
                    queue.append(n)
        if not border and area >= 24:
            holes += 1
    return holes

for row in rows:
    for hand, label in enumerate(row[1:3]):
        if label not in expected:
            continue
        for style in ['Gothic', 'Mincho', 'RoundedGothic', 'Brush']:
            path = root / 'Reports/SplitExpansion' / f'{row[0]}_{style}_{hand}.png'
            holes = count_holes(path)
            other = row[2 if hand == 0 else 1]
            # Brush fonts can already have open counters. Preserve the actual source,
            # rather than treating its calligraphic gaps as a splitting defect.
            reference = min(expected[label], count_holes(path.with_name(f'{row[0]}_{style}_source.png'))) if other in without_counters else expected[label]
            check = {'character': row[0], 'component': label, 'font': style, 'holes': holes, 'expected': reference,
                     'reference': 'original glyph' if other in without_counters else 'box structure'}
            checks.append(check)
            if holes != reference:
                failures.append(check)
report = {'checks': checks, 'failures': failures}
(root / 'Reports/structural-split-topology.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'Box topology: {len(checks)} checks, {len(failures)} failures.')
for failure in failures:
    print(json.dumps(failure, ensure_ascii=True))
raise SystemExit(1 if failures else 0)
