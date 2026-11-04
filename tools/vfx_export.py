"""Render the spell effects with pixelfx and repack them as game strips (reproducible).

  python -I tools/vfx_export.py [name ...]        (no name = every effect in EFFECTS)

pixelfx (MIT, Patch / Hugo Duprez) lives outside the repo: PIXELFX_DIR env, default ../_vendor/pixelfx (commit a841596).
Needs node and Pillow. Our own top-down effects are tools/vfx/fx/<name>.js: they are copied into <pixelfx>/src/fx/vk/ only to render.
Each effect is rendered as a transparent sheet, the wanted frames (every `step`-th from `frames`) are cropped to their common
bounding box and packed left to right, top to bottom, Assets/Resources/VFX/<name>.png, pixels unchanged.
Assets/Resources/VFX/vfx.json: {name: {frames, w, h, pivot:[x,y] (px from the top-left of a frame), ms (per frame), loop}}.
"""
import json, os, shutil, subprocess, sys, tempfile
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
PFX = Path(os.environ.get("PIXELFX_DIR", ROOT.parent / "_vendor" / "pixelfx")).resolve()
OUT = ROOT / "Assets" / "Resources" / "VFX"
CUSTOM = ROOT / "tools" / "vfx" / "fx"
MAX_SIZE = 2048  # the widest texture every target GPU takes

# name: strip name; id: pixelfx effect; opts: effect options; fps: render rate (fewer frames); frames: [from, to) of the rendered
# frames (to = None: all); step: keep every step-th frame. Frame time = step / fps.
# erase_band (dx, dy): clears the side-view shock ring, |y - pivot y| <= dy and |x - pivot x| > dx, in a top-down game it reads as a flat bar.
EFFECTS = [
    dict(name="poison_pool", id="vk/poison-pool", fps=20),
    dict(name="burning_ground", id="vk/burning-ground", fps=24),
    dict(name="scorch", id="vk/scorch", fps=10),
    dict(name="flame_tuft", id="vk/flame-tuft", fps=24),
    dict(name="poison_aura", id="vk/poison-aura", fps=20),
    dict(name="burn_aura", id="vk/burn-aura", fps=24),
    dict(name="meteor", id="fire/meteor", fps=20, frames=(0, 32), erase_band=(72, 30)),
    dict(name="acid_splash", id="nature/acid-splash", fps=30, frames=(0, 14)),
    # wave 2: the same top-down effects in each boss's colours, then the seal and the sky strikes
    dict(name="ink_pool", id="vk/poison-pool", fps=20, opts=dict(palette=["#08060c", "#120c1c", "#1f1430", "#2e1d48", "#46307a", "#7a55c8", "#c4a8ff"])),
    dict(name="blood_pool", id="vk/poison-pool", fps=20, opts=dict(palette=["#1a0508", "#3a0a10", "#661018", "#96181f", "#c42a2a", "#e8604a", "#ffb094"])),
    dict(name="violet_tuft", id="vk/flame-tuft", fps=24, opts=dict(palette=["#1f0a3a", "#3a1470", "#5a1fa8", "#7d2fe0", "#a24dff", "#c285ff", "#dcb8ff", "#efdcff", "#ffffff"])),
    dict(name="blood_tuft", id="vk/flame-tuft", fps=24, opts=dict(palette=["#2a0508", "#5a0a10", "#8a1018", "#b81a20", "#e0302a", "#f0604a", "#ff9070", "#ffc4a8", "#ffffff"])),
    dict(name="rune_violet", id="vk/rune-ring", fps=10),
    dict(name="rune_gold", id="vk/rune-ring", fps=10, opts=dict(palette=["#2a1a08", "#5a3a10", "#8a5a14", "#c08a1e", "#f0b830", "#ffe070", "#fff8d0"])),
    dict(name="rune_blood", id="vk/rune-ring", fps=10, opts=dict(palette=["#2a0508", "#5a0a10", "#8a1018", "#b81a20", "#e0302a", "#f0604a", "#ffc4a8"])),
    dict(name="blood_rain", id="vk/blood-rain", fps=20),
    dict(name="blood_splash", id="nature/acid-splash", fps=30, frames=(0, 14), erase_band=(16, 7), opts=dict(palette=["#2a0508", "#661018", "#96181f", "#c42a2a", "#e8604a", "#ffb094", "#ffe0d0"])),
    dict(name="thunder", id="lightning/lightning-strike", fps=20, frames=(17, 32), opts=dict(palette=["#2a1450", "#5a28a8", "#8a50ff", "#c8a0ff", "#ffe9a0", "#ffffff"], sparks=["#8a50ff", "#c8a0ff", "#ffe9a0", "#ffffff"])),
    dict(name="gold_smite", id="holy/smite", fps=20, frames=(11, 24), erase_band=(45, 22)),
]


def render(e, tmp):
    eid = e["id"]
    args = ["node", "tools/render.mjs", eid, "--sheet", "--no-actor", "--bg", "none", "--out", str(tmp)]
    if e.get("fps"): args += ["--fps", str(e["fps"])]
    if e.get("opts"): args += ["--opts", json.dumps(e["opts"])]
    r = subprocess.run(args, cwd=PFX, capture_output=True, text=True)
    if r.returncode: sys.exit(f"{eid}: render failed\n{r.stdout}{r.stderr}")
    base = tmp / eid / eid.split("/")[1]
    return Image.open(str(base) + ".png").convert("RGBA"), json.load(open(str(base) + ".json"))


def pack(e, sheet, meta):
    fr = meta["frames"]
    a, b = e.get("frames") or (0, len(fr))
    pick = fr[a:min(b, len(fr)):e.get("step", 1)]
    imgs = [sheet.crop((f["frame"]["x"], f["frame"]["y"], f["frame"]["x"] + f["frame"]["w"], f["frame"]["y"] + f["frame"]["h"])) for f in pick]
    if e.get("erase_band"):
        dx, dy = e["erase_band"]; cx, cy = meta["meta"]["pixelfx"]["pivot"]
        clear = Image.new("RGBA", imgs[0].size, (0, 0, 0, 0)); W = imgs[0].width
        for im in imgs:
            for box in ((0, cy - dy, cx - dx, cy + dy), (cx + dx, cy - dy, W, cy + dy)): im.paste(clear.crop(box), box[:2])
    boxes = [im.getchannel("A").getbbox() for im in imgs]
    boxes = [bx for bx in boxes if bx]
    l, t = min(bx[0] for bx in boxes), min(bx[1] for bx in boxes)
    r, bt = max(bx[2] for bx in boxes), max(bx[3] for bx in boxes)
    w, h = r - l, bt - t
    cols = min(len(imgs), MAX_SIZE // w)  # long clips wrap into rows, a strip wider than the texture limit gets shrunk by Unity
    strip = Image.new("RGBA", (w * cols, h * ((len(imgs) + cols - 1) // cols)), (0, 0, 0, 0))
    for i, im in enumerate(imgs): strip.paste(im.crop((l, t, r, bt)), (i % cols * w, i // cols * h))
    px = meta["meta"]["pixelfx"]
    pv = px.get("pivot") or [imgs[0].width // 2, imgs[0].height // 2]
    ms = round(1000.0 * e.get("step", 1) / px["fps"])
    return strip, dict(frames=len(imgs), w=w, h=h, pivot=[int(pv[0]) - l, int(pv[1]) - t], ms=ms, loop="loop" if px.get("loop") else "none")


def main():
    names = sys.argv[1:]
    todo = [e for e in EFFECTS if not names or e["name"] in names]
    if not (PFX / "tools" / "render.mjs").exists(): sys.exit(f"pixelfx not found at {PFX} (set PIXELFX_DIR)")
    vk = PFX / "src" / "fx" / "vk"; vk.mkdir(exist_ok=True)
    for f in CUSTOM.glob("*.js"): shutil.copyfile(f, vk / f.name)
    OUT.mkdir(parents=True, exist_ok=True)
    jp = OUT / "vfx.json"
    info = json.load(open(jp)) if jp.exists() and names else {}
    with tempfile.TemporaryDirectory() as td:
        for e in todo:
            strip, meta = pack(e, *render(e, Path(td)))
            strip.save(OUT / (e["name"] + ".png"))
            info[e["name"]] = meta
            print(e["name"], meta)
    keep = {e["name"] for e in EFFECTS}
    info = {k: v for k, v in info.items() if k in keep}
    json.dump(info, open(jp, "w"), indent=1)


if __name__ == "__main__":
    main()
