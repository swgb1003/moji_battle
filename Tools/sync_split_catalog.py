"""Mirror Unity's structural table into the independently deployed room server."""
from pathlib import Path
import argparse

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--check', action='store_true')
args = parser.parse_args()
source = (root / 'Assets/Resources/Glyphs/SplitKanji.csv').read_bytes()
target = root / 'Server/split-kanji.csv'
if args.check:
    assert target.read_bytes() == source, 'Room server split catalog differs from Unity; run Tools/sync_split_catalog.py'
else:
    target.write_bytes(source)
print('Split catalogs match.' if args.check else 'Updated room server split catalog.')
