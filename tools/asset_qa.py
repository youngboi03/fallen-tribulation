"""QA gate for PixelLab sprites: one line per finding, exit 1 only on FAIL (meta); WARN = look at it, art call. Read-only.

  python -I tools/asset_qa.py [meta|baseline|seam|palette ...]    (default: all four)
  python -I tools/asset_qa.py intake OUT.png RAW.png   (after a fetch+clean: undo an overreaching clean, log, write a compare sheet)
  python -I tools/asset_qa.py --selftest

meta     .meta import settings per folder (filter, compression, mipmap, PPU, pivot), incl. platform overrides
baseline empty rows under the feet in Chars/Enemies/Bosses: sprites whose feet float above the family median
seam     Floors (16 Wang tiles): colour jump across edges where two tiles meet in game, vs the jump inside a tile
palette  per-file mean lightness / saturation vs its folder (>2 sigma = WARN, report only, never auto-fixed)
"""
import glob
import os
import re
import statistics
import sys
from colorsys import rgb_to_hls
from PIL import Image

ROOT = "Assets/Resources/PixelLab"
PPU = {"Bosses": 96, "Icons": 40}  # every other folder is 48
FEET = ("Chars", "Enemies", "Bosses")
TILE = 32
BASELINE_TOL = 2  # rows; PixelLab draws feet 1-2 rows above the edge, so a wider gap = a floating sprite
SEAM_RATIO = 1.5  # uncalibrated: the four current floors measure 0.67-1.88


def files(folder):
    return sorted(p.replace("\\", "/") for p in glob.glob(f"{ROOT}/{folder}/*.png"))


def opaque(im):
    im = im.convert("RGBA")
    return [(x, y, *im.getpixel((x, y))[:3]) for y in range(im.height) for x in range(im.width) if im.getpixel((x, y))[3] >= 128]


def meta_issues(text, ppu):
    """Return the problems in one texture .meta (pure text, so selftest needs no files)."""
    bad = []
    want = {"filterMode": "0", "enableMipMap": "0", "spritePixelsToUnits": str(ppu), "spritePivot": "{x: 0.5, y: 0.5}"}
    for key, val in want.items():
        m = re.search(rf"^\s*{key}: (.*)$", text, re.M)
        if not m or m.group(1).strip() != val:
            bad.append(f"{key}={m.group(1).strip() if m else '?'} want {val}")
    # a platform block only matters when overridden: 1 (Unity writes compression 1 into unused blocks)
    for blk in re.split(r"buildTarget: ", text)[1:]:
        c = re.search(r"textureCompression: (\d)", blk)
        if "overridden: 1" in blk and c and c.group(1) != "0":
            bad.append(f"{blk.split()[0]} overrides compression={c.group(1)}")
    return bad


def check_meta():
    out = []
    for folder in sorted(os.listdir(ROOT)):
        for p in files(folder):
            try:
                text = open(p + ".meta", encoding="utf-8").read()
            except FileNotFoundError:
                out.append(("WARN", p, "no .meta yet (Unity imports it on next open)"))
                continue
            bad = meta_issues(text, PPU.get(folder, 48))
            if bad:
                out.append(("FAIL", p, "; ".join(bad)))
    return out


def bottom_pad(im):
    box = im.convert("RGBA").getchannel("A").point(lambda a: 255 if a >= 128 else 0).getbbox()
    return im.height - box[3] if box else None


def baseline_issues(pads, tol=BASELINE_TOL):
    """pads: {name: rows empty under the feet}. Flag those further than `tol` from the family median."""
    med = statistics.median(pads.values())
    return {n: (v, med) for n, v in pads.items() if abs(v - med) > tol}


def check_baseline():
    out = []
    for folder in FEET:
        pads = {p: bottom_pad(Image.open(p)) for p in files(folder)}
        pads = {p: v for p, v in pads.items() if v is not None}
        if not pads:
            continue
        for p, (v, med) in baseline_issues(pads).items():
            out.append(("WARN", p, f"{v} empty rows under feet, {folder} median {med:g}"))
    return out


def seam_ratio(im, tile=TILE):
    """Floors are 16 Wang tiles, index = NW*8 + NE*4 + SW*2 + SE (see Arena.cs). Where two tiles agree on the corners of a shared edge
    they sit next to each other in game: mean colour jump across those edges / mean jump between neighbouring pixels inside tiles."""
    im = im.convert("RGBA")
    px = im.load()
    n = im.width // tile

    def d(a, b):
        return sum(abs(a[i] - b[i]) for i in range(3)) if a[3] >= 128 and b[3] >= 128 else None

    def pair(ax, ay, bx, by):
        return d(px[ax, ay], px[bx, by])

    edge, inner = [], []
    for t in range(n):
        for y in range(tile):
            for x in range(tile):
                for dx, dy in ((1, 0), (0, 1)):
                    if x + dx < tile and y + dy < tile:
                        v = pair(t * tile + x, y, t * tile + x + dx, y + dy)
                        if v is not None:
                            inner.append(v)
    for a in range(n):
        for b in range(n):
            if (a >> 2 & 1, a & 1) == (b >> 3 & 1, b >> 1 & 1):  # a's east corners == b's west corners
                edge += [v for y in range(tile) if (v := pair(a * tile + tile - 1, y, b * tile, y)) is not None]
            if (a & 3) == (b >> 2 & 3):  # a's south corners (SW,SE) == b's north corners (NW,NE)
                edge += [v for x in range(tile) if (v := pair(a * tile + x, tile - 1, b * tile + x, 0)) is not None]
    return (statistics.mean(edge) / max(statistics.mean(inner), 1e-6)) if edge and inner else None


def check_seam():
    out = []
    for p in files("Floors"):
        im = Image.open(p)
        r = seam_ratio(im)
        a = 1 - sum(im.convert("RGBA").getchannel("A").point(lambda v: 1 if v >= 128 else 0).histogram()[1:]) / (im.width * im.height)
        if r is not None and r > SEAM_RATIO:
            out.append(("WARN", p, f"tile seam {r:.2f}x the inner colour jump (max {SEAM_RATIO})"))
        if a > 0.02:
            out.append(("WARN", p, f"{a:.0%} transparent: overlay layer or hole?"))
    return out


def check_palette():
    out = []
    for folder in sorted(os.listdir(ROOT)):
        rows = {}
        for p in files(folder):
            px = opaque(Image.open(p))
            if px:
                hls = [rgb_to_hls(r / 255, g / 255, b / 255) for _, _, r, g, b in px]
                rows[p] = (statistics.mean(h[1] for h in hls), statistics.mean(h[2] for h in hls))
        if len(rows) < 4:
            continue
        for k, label in ((0, "lightness"), (1, "saturation")):
            vals = [v[k] for v in rows.values()]
            mu, sd = statistics.mean(vals), statistics.pstdev(vals)
            for p, v in rows.items():
                if sd > 0 and abs(v[k] - mu) > 2 * sd:
                    out.append(("WARN", p, f"mean {label} {v[k]:.2f} vs {folder} {mu:.2f} (+-{sd:.2f})"))
    return out


CHECKS = {"meta": check_meta, "baseline": check_baseline, "seam": check_seam, "palette": check_palette}


REVERT_AT = 0.01  # the clean step may change at most 1% of the opaque pixels, else it is undone (protects eyes, sparks)
INTAKE = "tools/.intake"


def assess(raw, new, folder, fam_pads, fam_cols):
    """Findings for one freshly fetched+cleaned sprite and whether to restore the raw file. Pure on images, so selftest needs no files."""
    out, revert = [], False
    if raw.size != new.size:
        return [("FAIL", f"canvas {new.size} != raw {raw.size}")], True
    r, n = raw.convert("RGBA"), new.convert("RGBA")
    rp, npx = r.load(), n.load()
    w, h = n.size
    opq = sum(1 for y in range(h) for x in range(w) if rp[x, y][3] >= 128) or 1
    changed = sum(1 for y in range(h) for x in range(w) if rp[x, y] != npx[x, y] and (rp[x, y][3] >= 128 or npx[x, y][3] >= 128))
    if changed / opq > REVERT_AT:
        out.append(("WARN", f"clean changed {changed / opq:.1%} of pixels (> {REVERT_AT:.0%}): raw restored"))
        revert, n, npx = True, r, rp
    semi = sum(1 for y in range(h) for x in range(w) if 0 < npx[x, y][3] < 255)
    if semi:
        out.append(("FAIL", f"{semi} semi-transparent pixels"))
    cols = len({npx[x, y][:3] for y in range(h) for x in range(w) if npx[x, y][3] >= 128})
    if fam_cols and cols > fam_cols * 1.1:
        out.append(("WARN", f"{cols} colours, folder max {fam_cols}"))
    if all(npx[x, y] == npx[x - x % 2, y - y % 2] for y in range(h) for x in range(w)):
        out.append(("WARN", "looks 2x-upscaled (every 2x2 block uniform)"))
    lone = sum(1 for y in range(h) for x in range(w) if npx[x, y][3] >= 128 and not any(
        0 <= x + dx < w and 0 <= y + dy < h and npx[x + dx, y + dy][3] >= 128 for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy))
    if lone:
        out.append(("WARN", f"{lone} isolated pixels (check they are eyes/sparks)"))
    box = n.getchannel("A").point(lambda a: 255 if a >= 128 else 0).getbbox()
    if folder == "Icons" and box and (box[0] == 0 or box[1] == 0 or box[2] == w or box[3] == h):
        out.append(("WARN", "icon touches the canvas edge"))
    if folder in FEET and box and fam_pads:
        med = statistics.median(fam_pads)
        if abs(h - box[3] - med) > BASELINE_TOL:
            out.append(("WARN", f"{h - box[3]} empty rows under feet, {folder} median {med:g}"))
    return out, revert


def sheet(raw, new, path):
    """raw | cleaned | changed pixels in red | cleaned 1x on a dark floor colour, all x4 except the last."""
    r, n = raw.convert("RGBA"), new.convert("RGBA")
    w, h = n.size
    diff = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for y in range(h):
        for x in range(w):
            if r.getpixel((x, y)) != n.getpixel((x, y)):
                diff.putpixel((x, y), (255, 0, 0, 255))
    bg = (38, 30, 44, 255)
    cells = [im.resize((w * 4, h * 4), Image.NEAREST) for im in (r, n, diff)] + [n]
    out = Image.new("RGBA", (sum(c.width for c in cells) + 5 * 8, h * 4 + 16), bg)
    x = 8
    for c in cells:
        out.alpha_composite(c, (x, 8))
        x += c.width + 8
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out.save(path)


def intake(target, raw_path):
    """Run after pixellab_fetch + ase_clean: compare, restore raw if the clean step overreached, log, write the sheet."""
    import hashlib
    import shutil
    folder = target.replace("\\", "/").split(ROOT + "/")[-1].split("/")[0]
    raw, new = Image.open(raw_path), Image.open(target)
    peers = [q for q in files(folder) if os.path.abspath(q) != os.path.abspath(target)]
    fam_pads = [v for v in (bottom_pad(Image.open(q)) for q in peers) if v is not None] if folder in FEET else []
    fam_cols = max((len({c[:3] for c in Image.open(q).convert("RGBA").get_flattened_data() if c[3] >= 128}) for q in peers), default=0)
    res, revert = assess(raw, new, folder, fam_pads, fam_cols)
    if revert:
        shutil.copyfile(raw_path, target)
    name = os.path.basename(target)
    sheet(raw, Image.open(target), f"{INTAKE}/sheets/{folder}_{name}")
    digest = hashlib.sha1(open(target, "rb").read()).hexdigest()
    log = f"{INTAKE}/report.jsonl"
    seen = open(log, encoding="utf-8").read() if os.path.exists(log) else ""
    if digest not in seen:
        import json
        with open(log, "a", encoding="utf-8") as f:
            f.write(json.dumps({"file": target, "sha1": digest, "findings": res, "reverted": revert}, ensure_ascii=False) + chr(10))
    for level, msg in res:
        print(f"{level} intake {folder}/{name}: {msg}")
    print(f"intake {folder}/{name}: {len(res)} findings, sheet {INTAKE}/sheets/{folder}_{name}")
    return 1 if any(r[0] == "FAIL" for r in res) else 0


def selftest():
    good = "enableMipMap: 0\nfilterMode: 0\nspritePixelsToUnits: 48\nspritePivot: {x: 0.5, y: 0.5}\n"
    plat = "buildTarget: Standalone\ntextureCompression: 1\noverridden: %d\n"
    assert meta_issues(good, 48) == []
    assert len(meta_issues(good.replace("filterMode: 0", "filterMode: 1"), 48)) == 1
    assert any("PixelsToUnits" in b for b in meta_issues(good, 96))
    assert meta_issues(good + plat % 0, 48) == []  # unused platform block is ignored
    assert any("overrides" in b for b in meta_issues(good + plat % 1, 48))
    assert meta_issues(good.replace("enableMipMap: 0", "enableMipMap: 1"), 48)
    assert set(baseline_issues({"a": 1, "b": 2, "c": 2, "d": 9})) == {"d"}
    assert baseline_issues({"a": 1, "b": 3, "c": 2}) == {}  # within +-2 rows
    flat = Image.new("RGBA", (16 * TILE, TILE), (50, 50, 50, 255))
    assert seam_ratio(flat) <= 1  # one colour everywhere: nothing to jump
    bad = flat.copy()
    bad.paste((90, 90, 90, 255), (5 * TILE, 0, 6 * TILE, TILE))  # tile 5 lighter than its neighbours
    assert seam_ratio(bad) > SEAM_RATIO, seam_ratio(bad)
    sp = Image.new("RGBA", (20, 20), (0, 0, 0, 0))
    sp.paste((200, 50, 50, 255), (3, 2, 17, 17))
    for x in range(3, 17):
        sp.putpixel((x, 8), (220 + x % 5, 60, 60, 255))  # texture so the 2x-block test does not fire
    sp.putpixel((9, 9), (10, 10, 10, 255))
    res, rev = assess(sp, sp, "Chars", [3, 3, 3], 40)
    assert not rev and not any(r[0] == "FAIL" for r in res), res
    big = sp.copy()
    big.paste((0, 200, 0, 255), (3, 2, 17, 10))  # ~40% of the sprite recoloured
    res, rev = assess(sp, big, "Chars", [3, 3, 3], 40)
    assert rev and "restored" in res[0][1], res
    soft = sp.copy()
    soft.putpixel((5, 5), (200, 50, 50, 100))  # semi alpha on a 1-pixel change (<1%)
    res, rev = assess(sp, soft, "Chars", [3, 3, 3], 40)
    assert not rev and ("FAIL", "1 semi-transparent pixels") in res, res
    res, rev = assess(sp, sp.resize((22, 22)), "Chars", [], 0)
    assert rev and res[0][0] == "FAIL", res  # canvas size changed
    floaty = Image.new("RGBA", (20, 20), (0, 0, 0, 0))
    floaty.paste((200, 50, 50, 255), (3, 2, 17, 10))
    floaty.putpixel((5, 4), (90, 20, 20, 255))
    res, _ = assess(floaty, floaty, "Chars", [2, 2, 2], 40)
    assert any("empty rows under feet" in m for _, m in res), res
    print("asset_qa selftest OK")


if __name__ == "__main__":
    a = sys.argv[1:]
    if a == ["--selftest"]:
        selftest()
        sys.exit(0)
    if a and a[0] == "intake":
        if len(a) != 3:
            sys.exit("usage: asset_qa.py intake OUT.png RAW.png")
        sys.exit(intake(a[1], a[2]))
    names = a or list(CHECKS)
    if any(n not in CHECKS for n in names):
        sys.exit(__doc__)
    fails = 0
    for n in names:
        res = CHECKS[n]()
        for level, p, msg in res:
            print(f"{level} {n} {p.replace(ROOT + '/', '')}: {msg}")
        fails += sum(1 for r in res if r[0] == "FAIL")
        print(f"{n}: {len(res)} findings")
    sys.exit(1 if fails else 0)
