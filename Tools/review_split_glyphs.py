"""Build review sheets from Unity's alpha exports; never changes game assets."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
rows = [line.split(',') for line in (root / 'Assets/Resources/Glyphs/SplitKanji.csv').read_text(encoding='utf-8').splitlines()
        if line and not line.startswith('#')]
font = ImageFont.truetype(str(root / 'Assets/Art/Fonts/Gothic/NotoSansJP-Bold.ttf'), 20)
out = root / 'Reports/SplitExpansion'
for style in ['Gothic', 'Mincho', 'RoundedGothic', 'Brush']:
    for layout in ['lr', 'tb', 'enc']:
        entries = [row for row in rows if row[3] == layout]
        columns, cell_w, cell_h = 5, 300, 116
        sheet = Image.new('RGB', (columns * cell_w, ((len(entries) + columns - 1) // columns) * cell_h), '#eee8db')
        draw = ImageDraw.Draw(sheet)
        for index, row in enumerate(entries):
            x, y = (index % columns) * cell_w, (index // columns) * cell_h
            draw.text((x + 4, y), f'{row[0]} : {row[1]} + {row[2]}', fill='black', font=font)
            for part, suffix in enumerate(['source', '0', '1']):
                path = out / f'{row[0]}_{style}_{suffix}.png'
                if not path.exists():
                    draw.text((x + part * 98 + 4, y + 45), 'MISSING', fill='red', font=font)
                    continue
                image = Image.open(path).convert('RGB').resize((90, 90))
                sheet.paste(image, (x + part * 98, y + 24))
        path = out / f'structure-{layout}-{style}.png'
        sheet.save(path)
        print(path.relative_to(root))
