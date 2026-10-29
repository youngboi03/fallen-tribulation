"""Procedural 8-frame walk strip from one static sprite (no AI). Legs lift/slide alternately, body dips on each footfall,
arms swing front-back from the shoulder (character faces right, so the swing is horizontal). Amplitudes scale with the sprite's
visible height, so the same motion fits any size; part boxes (hip row, leg split, arm boxes) come from tools/walk_parts.json.
  python -I tools/walk_strip.py ID [--src DIR] [--out DIR] [--gif PREVIEW.gif]     # reads DIR/ID.png, writes OUT/ID_walk.png
Arm mode: shear = shoulder (box top) fixed, hand moves most; rigid = whole box moves (held props); shear_up = box bottom fixed, top moves (staff planted at the hand/ground).
"legs": false in the json = robed sprite, no leg split (only body dip)."""
import argparse, json, math, os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ap = argparse.ArgumentParser(); ap.add_argument('id'); ap.add_argument('--src', default=os.path.join(ROOT, 'Assets/Resources/PixelLab/Chars'))
ap.add_argument('--out'); ap.add_argument('--gif'); ap.add_argument('--frames', type=int, default=8)
a = ap.parse_args()
cfg = json.load(open(os.path.join(ROOT, 'tools', 'walk_parts.json')))[a.id]
o = Image.open(os.path.join(a.src, a.id + '.png')).convert('RGBA'); W, H = o.size
bh = o.getbbox()[3] - o.getbbox()[1]
amp = lambda f: max(1, round(f * bh))              # fractions of the 47 px Kiem Tu reference
LIFT, SLIDE, SWAY, DIP = amp(2 / 47), amp(2 / 47), 3.2 * bh / 47, amp(1 / 47)
HIP, MID, N = cfg['hip'], cfg.get('mid', 24), a.frames
legs_on = cfg.get('legs', True)
body = o.copy(); body.paste((0, 0, 0, 0), (0, HIP, W, H))
legs = (o.crop((0, HIP, MID, H)), o.crop((MID, HIP, W, H)))
strip = Image.new('RGBA', (W * N, H))
for t in range(N):
    p = 2 * math.pi * t / N; s = math.sin(p); b = body.copy(); arms = []
    for ar in cfg['arms']:
        x0, y0, x1, y1 = ar['box']; box = (x0, y0, x1 + 1, y1 + 1)
        arms.append((box, o.crop(box), ar)); b.paste((0, 0, 0, 0), box)
    for (x0, y0, x1, y1), im, ar in arms:
        v = ar['sg'] * s; h = im.height; out = Image.new('RGBA', im.size)
        for r in range(h):
            dx = round(SWAY * v * {'rigid': 1, 'shear': r / h, 'shear_up': 1 - r / h}[ar['mode']])
            row = Image.new('RGBA', im.size); row.paste(im.crop((0, r, im.width, r + 1)), (dx, r)); out.alpha_composite(row)
        b.alpha_composite(out, (x0, y0))
    f = Image.new('RGBA', (W, H)); dip = DIP if t % (N // 2) == 0 else 0
    if legs_on:
        f.alpha_composite(legs[0], (round(-SLIDE * s), HIP - (LIFT if s > 0.4 else 0)))
        f.alpha_composite(legs[1], (MID + round(SLIDE * s), HIP - (LIFT if s < -0.4 else 0)))
    else: f.alpha_composite(o.crop((0, HIP, W, H)), (0, HIP))  # robed: no leg split, would tear the hem
    f.alpha_composite(b.crop((0, 0, W, H - dip)), (0, dip))
    strip.alpha_composite(f, (t * W, 0))
out = a.out or os.path.join(a.src, a.id + '_walk.png'); strip.save(out)
if a.gif:
    fr = []
    for i in range(N):
        c = Image.new('RGBA', (W, H), (70, 70, 70, 255)); c.alpha_composite(strip.crop((i * W, 0, i * W + W, H)))
        fr.append(c.resize((W * 4, H * 4), Image.NEAREST).convert('P', palette=Image.ADAPTIVE))
    fr[0].save(a.gif, save_all=True, append_images=fr[1:], duration=110, loop=0)
print(out)
