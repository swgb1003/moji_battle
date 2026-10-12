"""Build a compact offline Japanese stroke-count table from pinned Unicode 17 Unihan.
Prefer a directly encoded Adobe-Japan1 glyph; otherwise use an explicit J-source
alternate stroke count, falling back to the IRG total. No network access in game.
Source: https://www.unicode.org/reports/tr38/
"""
import io
import re
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
URL = 'https://www.unicode.org/Public/17.0.0/ucd/Unihan.zip'

def generate():
    archive = zipfile.ZipFile(io.BytesIO(urllib.request.urlopen(URL, timeout=60).read()))
    values, japanese, adobe = {}, {}, {}
    for filename in ('Unihan_IRGSources.txt', 'Unihan_RadicalStrokeCounts.txt', 'Unihan_DictionaryLikeData.txt'):
        for line in archive.read(filename).decode('utf-8').splitlines():
            if not line.startswith('U+'):
                continue
            code, prop, value = line.split('\t', 2)
            code = int(code[2:], 16)
            if prop == 'kTotalStrokes':
                values[code] = int(value.split()[0])
            elif prop == 'kAlternateTotalStrokes':
                for item in value.split():
                    if ':' in item and 'J' in item.split(':')[1]:
                        japanese[code] = int(item.split(':')[0])
                        break
            elif prop == 'kRSAdobe_Japan1_6':
                direct = re.findall(r'C\+\d+\+\d+\.(\d+)\.(\d+)', value)
                if direct:
                    adobe[code] = int(direct[0][0]) + int(direct[0][1])
    values.update(japanese)
    values.update(adobe)
    # Non-Han Unicode symbols, kana and Latin are excluded from this table.
    out = ROOT / 'Assets/Resources/Achievements/KanjiStrokes.txt'
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text('# Unicode 17.0.0 Unihan; Adobe-Japan1 direct glyph preferred\n' +
                   ''.join(f'{code:X}\t{count}\n' for code, count in sorted(values.items())), encoding='utf-8')
    print(f'{len(values)} stroke counts saved; 龍={values[0x9F8D]}, 鬱={values[0x9B31]}, 舞={values[0x821E]}')
    license_text = urllib.request.urlopen('https://www.unicode.org/license.txt', timeout=60).read().decode('utf-8')
    (out.parent / 'Unicode-LICENSE.txt').write_text(license_text, encoding='utf-8')

if __name__ == '__main__':
    generate()
