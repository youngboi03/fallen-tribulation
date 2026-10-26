"""Download a PixelLab top-down Wang tileset and save it as a 16-tile strip ordered by corner mask
(index = NW*8 + NE*4 + SW*2 + SE*1, where 1 = upper terrain), plus a provenance line.
Usage: python -I tools/pixellab_tileset.py <tileset_id> <out.png> "<lower>" "<upper>" """
import io, json, os, sys, urllib.request
from PIL import Image

tid, out, lower, upper = sys.argv[1:5]
base = f'https://api.pixellab.ai/mcp/tilesets/{tid}'
meta = json.load(urllib.request.urlopen(base + '/metadata'))
sheet = Image.open(io.BytesIO(urllib.request.urlopen(base + '/image?inline=true').read())).convert('RGBA')
tiles = meta['tileset_data']['tiles']
size = tiles[0]['bounding_box']['width']
strip = Image.new('RGBA', (size * 16, size), (0, 0, 0, 0))
found = set()
for t in tiles:
    c = t['corners']
    if any(v not in ('upper', 'lower') for v in c.values()): continue
    idx = sum(b << s for b, s in zip([c['NW'] == 'upper', c['NE'] == 'upper', c['SW'] == 'upper', c['SE'] == 'upper'], [3, 2, 1, 0]))
    bb = t['bounding_box']
    strip.paste(sheet.crop((bb['x'], bb['y'], bb['x'] + bb['width'], bb['y'] + bb['height'])), (idx * size, 0))
    found.add(idx)
missing = sorted(set(range(16)) - found)
os.makedirs(os.path.dirname(out), exist_ok=True)
strip.save(out)
log = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'Assets', 'ThirdParty', 'PixelLab-Assets.md')
with open(log, 'a', encoding='utf-8') as f:
    f.write(f'| `{out}` | create_topdown_tileset standard {size}px (re-ordered into a corner-mask strip) | {tid} | lower: {lower} / upper: {upper} |\n')
print(f'saved {out} tiles={len(found)} missing={missing}')
