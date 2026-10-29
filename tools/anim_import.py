"""PixelLab clip frames -> one horizontal strip the game reads (Res.Anim): feet on the base sprite's
bottom line, body centred like the base, colours locked to the base sprite's own colours.

  python -I tools/anim_import.py BASE.png OUT.png FRAME.png... [--frames N] [--no-x]
  python -I tools/anim_import.py --selftest

--no-x keeps each frame's horizontal position (lunges); default centres it like the base.
Exit 1 with a message when a frame count/size is wrong or a shifted frame would be cut off.
"""
import sys
from PIL import Image


def snap(im, palette):
    im = im.convert("RGBA")
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            px[x, y] = (0, 0, 0, 0) if a < 128 else (*min(palette, key=lambda c: (c[0] - r) ** 2 + (c[1] - g) ** 2 + (c[2] - b) ** 2), 255)
    return im


def colours(im):
    px = im.load()
    return sorted({px[x, y][:3] for y in range(im.height) for x in range(im.width) if px[x, y][3] >= 128})


def box(im):
    b = im.getchannel("A").getbbox()
    if not b:
        raise ValueError("empty frame")
    return b


def build(base_path, frame_paths, centre_x=True, frames=None):
    base = Image.open(base_path).convert("RGBA")
    bx0, _, bx1, bb = box(base)
    palette = colours(base)
    if frames is not None and len(frame_paths) != frames:
        raise ValueError(f"expected {frames} frames, got {len(frame_paths)}")
    strip = Image.new("RGBA", (base.width * len(frame_paths), base.height))
    for i, path in enumerate(frame_paths):
        fr = snap(Image.open(path), palette)
        if fr.size != base.size:
            raise ValueError(f"{path}: size {fr.size} != base {base.size}")
        x0, _, x1, b = box(fr)
        dx = (bx0 + bx1) // 2 - (x0 + x1) // 2 if centre_x else 0
        dy = bb - b
        if x0 + dx < 0 or x1 + dx > fr.width or box(fr)[1] + dy < 0:
            raise ValueError(f"{path}: shift ({dx},{dy}) cuts the sprite off")
        cell = Image.new("RGBA", base.size)
        cell.paste(fr, (dx, dy))
        strip.paste(cell, (i * base.width, 0))
    return strip


def selftest():
    import os, tempfile
    d = tempfile.mkdtemp()

    def save(name, rect, colour):
        im = Image.new("RGBA", (16, 16))
        im.paste(colour, rect)
        im.save(os.path.join(d, name))
        return os.path.join(d, name)

    base = save("b.png", (6, 8, 10, 16), (200, 0, 0, 255))
    f1 = save("f1.png", (2, 4, 6, 12), (205, 3, 1, 255))  # off-palette red, up and left
    f2 = save("f2.png", (6, 8, 10, 16), (200, 0, 0, 255))
    small = os.path.join(d, "s.png")
    Image.new("RGBA", (8, 8), (200, 0, 0, 255)).save(small)
    s = build(base, [f1, f2])
    assert s.size == (32, 16)
    cell = s.crop((0, 0, 16, 16))
    assert box(cell) == (6, 8, 10, 16), box(cell)  # feet on the bottom, centred
    assert colours(s) == [(200, 0, 0)]  # colours locked
    for bad in (lambda: build(base, [f1, f2], frames=3), lambda: build(base, [small])):
        try:
            bad()
        except ValueError:
            continue
        raise AssertionError("expected ValueError")
    print("anim_import selftest OK")


if __name__ == "__main__":
    a = sys.argv[1:]
    if a == ["--selftest"]:
        selftest()
        sys.exit(0)
    frames = int(a.pop(a.index("--frames") + 1)) if "--frames" in a else None
    a = [x for x in a if x != "--frames"]
    cx = "--no-x" not in a
    a = [x for x in a if x != "--no-x"]
    if len(a) < 3:
        sys.exit(__doc__)
    try:
        build(a[0], a[2:], cx, frames).save(a[1])
    except ValueError as e:
        sys.exit(f"anim_import: {e}")
    print(f"{a[1]}: {len(a) - 2} frames")
