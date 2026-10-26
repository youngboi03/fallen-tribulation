"""Icon pipeline for PixelLab img2img.
  build <weapons|items> <out.png> <out.json>  : grid of the current Kenney icons (tinted as in game, 2x, 40px cells)
  slice <sheet.png> <map.json> <outdir> <job> : cut a redrawn sheet back into one PNG per weapon/item + provenance
Reads ids/sprites/tints from Assets/Scripts/Data/Content.cs."""
import json, math, os, re, sys
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CELL, SCALE = 40, 2
# better stand-in shapes for treasures whose Kenney sprite says little about them
OVERRIDE = {'thon_thien': (66, (0.75, 0.35, 0.9)), 'cuong_chien_y': (117, (1.0, 0.45, 0.35))}

def defs(kind):
    src = open(os.path.join(ROOT, 'Assets', 'Scripts', 'Data', 'Content.cs'), encoding='utf-8').read()
    tag = 'WeaponDef' if kind == 'weapons' else 'ItemDef'
    out = []
    for m in re.finditer(r'new ' + tag + r' \{ id="([a-z_]+)", name="[^"]*", sprite=([A-Za-z0-9.]+)(?:, tint=new Color\(([^)]*)\))?', src):
        sid, spr, tint = m.group(1), m.group(2), m.group(3)
        t = tuple(float(x.strip().rstrip('f')) for x in tint.split(',')[:3]) if tint else (1, 1, 1)
        spr = 200 if 'Talisman' in spr else int(spr)
        if sid in OVERRIDE: spr, t = OVERRIDE[sid]
        out.append((sid, spr, t))
    return out

def talisman():
    rows = ["................", "....OOOOOOOO....", "....OPPPPPPO....", "....OPPRRPPO....", "....OPRPPRPO....", "....OPPRRPPO....",
            "....OPPPPPPO....", "....OPRRRRPO....", "....OPPPRPPO....", "....OPRRRRPO....", "....OPPPRPPO....", "....OPPRPRPO....",
            "....OPRPPPRO....", "....OPPPPPPO....", "....OOOOOOOO....", "................"]
    col = {'O': (60, 30, 25, 255), 'P': (245, 214, 120, 255), 'R': (200, 40, 35, 255), '.': (0, 0, 0, 0)}
    im = Image.new('RGBA', (16, 16))
    for y, r in enumerate(rows):
        for x, ch in enumerate(r): im.putpixel((x, y), col[ch])
    return im

def icon(spr, tint):
    im = talisman() if spr == 200 else Image.open(os.path.join(ROOT, 'Assets', 'Resources', 'Tiles', f'tile_{spr:04d}.png')).convert('RGBA')
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            px[x, y] = (min(255, int(r * tint[0])), min(255, int(g * tint[1])), min(255, int(b * tint[2])), a)
    return im.resize((im.width * SCALE, im.height * SCALE), Image.NEAREST)

if sys.argv[1] == 'build':
    kind, out, mp = sys.argv[2:5]
    d = defs(kind)
    n = math.ceil(math.sqrt(len(d)))
    sheet = Image.new('RGBA', (n * CELL, n * CELL), (0, 0, 0, 0))
    for i, (sid, spr, t) in enumerate(d):
        ic = icon(spr, t)
        sheet.alpha_composite(ic, ((i % n) * CELL + (CELL - ic.width) // 2, (i // n) * CELL + (CELL - ic.height) // 2))
    sheet.save(out)
    json.dump({'kind': kind, 'n': n, 'ids': [x[0] for x in d]}, open(mp, 'w'))
    print(f'{kind}: {len(d)} icons, {n}x{n} grid, {sheet.size}')
else:
    sheet_path, mp, outdir, job = sys.argv[2:6]
    m = json.load(open(mp)); sheet = Image.open(sheet_path).convert('RGBA')
    pre = 'w_' if m['kind'] == 'weapons' else 'i_'
    os.makedirs(outdir, exist_ok=True)
    log = os.path.join(ROOT, 'Assets', 'ThirdParty', 'PixelLab-Assets.md')
    for i, sid in enumerate(m['ids']):
        x, y = (i % m['n']) * CELL, (i // m['n']) * CELL
        sheet.crop((x, y, x + CELL, y + CELL)).save(os.path.join(outdir, pre + sid + '.png'))
    with open(log, 'a', encoding='utf-8') as f:
        f.write(f'| `{outdir}/{pre}*.png` ({len(m["ids"])} files) | create_image_pixflux img2img on a grid of the Kenney icons, sliced into {CELL}px cells | {job} | redraw of the {m["kind"]} icons, see tools/icon_sheet.py |\n')
    print(f'sliced {len(m["ids"])} icons into {outdir}')
