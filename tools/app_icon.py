"""App icon for Fallen Tribulation, drawn procedurally as 64x64 pixel art and scaled 8x to 512.
Motif: a flying sword plunging through a golden cultivation ring while tribulation lightning splits the night sky, clouds below.
Usage: python -I tools/app_icon.py [out.png]   (default Assets/Editor/Icon/app_icon.png)"""
import math, sys
from PIL import Image

N, K = 64, 8
img = Image.new("RGBA", (N, N))
px = img.load()

def put(x, y, c):
    if 0 <= x < N and 0 <= y < N and px[x, y][3]: px[x, y] = c   # never paint outside the rounded square

def lerp(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))

# night sky: rounded square, indigo at the top, deep violet near the horizon, stepped (no smooth gradients in pixel art)
for y in range(N):
    for x in range(N):
        r = 9  # corner radius
        cx, cy = min(max(x, r), N - 1 - r), min(max(y, r), N - 1 - r)
        if (x - cx) ** 2 + (y - cy) ** 2 > r * r: continue
        band = min(5, y // 11)
        px[x, y] = lerp((14, 12, 38, 255), (52, 26, 70, 255), band / 5)

# stars
for x, y in [(8, 7), (54, 10), (47, 5), (12, 20), (57, 24), (5, 30), (40, 13)]:
    put(x, y, (200, 210, 255, 255))

C = (32, 30)
# golden ring (cultivation halo), 2px thick with a lit top-left edge
for y in range(N):
    for x in range(N):
        d = math.hypot(x - C[0] + 0.5, y - C[1] + 0.5)
        if 19 <= d < 21.2:
            lit = (x - C[0]) + (y - C[1]) < 0
            put(x, y, (255, 214, 102, 255) if lit else (214, 150, 48, 255))
        elif 21.2 <= d < 22.2:
            put(x, y, (110, 60, 30, 255))

# tribulation lightning: a zig-zag bolt from the top right down across the ring, cyan core with a pale halo
pts = [(50, 1), (41, 15), (47, 17), (36, 31), (41, 33), (27, 52)]
bolt = set()
for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
    n = max(abs(x1 - x0), abs(y1 - y0))
    for i in range(n + 1):
        bolt.add((round(x0 + (x1 - x0) * i / n), round(y0 + (y1 - y0) * i / n)))
for x, y in bolt:
    for dx, dy in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
        if (x + dx, y + dy) not in bolt: put(x + dx, y + dy, (90, 150, 255, 255))
for x, y in bolt: put(x, y, (225, 245, 255, 255))

# the sword, point down, along a slight diagonal: pommel top left, tip bottom right of centre
def sword_pt(t):  # t: 0 pommel .. 1 tip
    return (22 + 18 * t, 6 + 50 * t)
for i in range(0, 101):
    t = i / 100
    x, y = sword_pt(t)
    xi, yi = round(x), round(y)
    if t < 0.06:      # pommel
        for dx in (-1, 0, 1): put(xi + dx, yi, (255, 214, 102, 255))
    elif t < 0.22:    # wrapped grip
        put(xi, yi, (120, 40, 40, 255) if (yi % 2) else (170, 60, 50, 255)); put(xi + 1, yi, (90, 30, 30, 255))
    elif t < 0.26:    # guard
        for dx in range(-5, 7):
            put(xi + dx, yi, (255, 214, 102, 255) if dx < 3 else (214, 150, 48, 255)); put(xi + dx, yi + 1, (150, 90, 30, 255))
    else:             # blade, tapering to a point
        w = 2 if t < 0.88 else 1
        for dx in range(-w + 1, w + 1):
            put(xi + dx, yi, (240, 248, 255, 255) if dx <= 0 else (150, 170, 200, 255))
        put(xi - w, yi, (60, 70, 110, 255)); put(xi + w + 1, yi, (60, 70, 110, 255))

# clouds along the bottom: puffs of stacked circles, pale lavender with a darker underside
for cx, cy, r in [(8, 58, 7), (20, 61, 8), (34, 60, 6), (46, 61, 8), (58, 57, 7), (27, 55, 5), (52, 53, 4)]:
    for y in range(cy - r, cy + r + 1):
        for x in range(cx - r, cx + r + 1):
            if (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                put(x, y, (215, 205, 240, 255) if y < cy - r // 3 else (150, 130, 190, 255))

out = sys.argv[1] if len(sys.argv) > 1 else "Assets/Editor/Icon/app_icon.png"
img.resize((N * K, N * K), Image.NEAREST).save(out)
print("wrote", out)
