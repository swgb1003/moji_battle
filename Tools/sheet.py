import glob, sys
from PIL import Image
files = sorted(glob.glob('Reports/Seq/s*.png'))
n = int(sys.argv[1]) if len(sys.argv) > 1 else 12
files = files[:n]
ims = [Image.open(f).crop((0, 100, 960, 500)) for f in files]
w, h = 480, 200
cols = 3
rows = (len(ims) + cols - 1) // cols
sheet = Image.new('RGB', (w * cols, h * rows), 'white')
for i, im in enumerate(ims):
    sheet.paste(im.resize((w, h)), ((i % cols) * w, (i // cols) * h))
sheet.save('Reports/Seq/sheet.png')
import os; print(os.linesep.join(os.path.basename(f) for f in files))
