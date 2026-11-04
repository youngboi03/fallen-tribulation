#!/usr/bin/env python
"""Generate game SFX/BGM candidates with Stable Audio 3 (audio-studio-mcp) and measure them.

Run with the audio-studio-mcp venv, caches on D (the models are gated, see Assets/ThirdParty/stability-NOTICE.txt):
  set HF_HOME=D:/Workspace/GameProject/_vendor/hf_cache
  _vendor/audio-studio-mcp/.venv/Scripts/python tools/audio_gen.py gen hit crit --n 6 --cpu
  ... tools/audio_gen.py measure OUT_GROUP_DIR
  ... tools/audio_gen.py finish      (PICK/LOOP below: trimmed, levelled clips and loops in audio_gen/final, plus audition files)
  ... tools/audio_gen.py selftest
Candidates go to D:/Workspace/GameProject/audio_gen/<name>/<name>_s<seed>.wav (outside the repo) with a .json of prompt + seed.
The ear decides; measure only rejects clips that are clipped, too long, too bright or silent.
"""
import argparse, json, re, shutil, subprocess, sys, time
from pathlib import Path

import numpy as np

OUT = Path("D:/Workspace/GameProject/audio_gen")
SR_FALLBACK = 44100

# name: (kind, seconds, bpm, prompt). Prompts follow the server's guide: source, action, material, space.
SPEC = {
    "hit": ("sfx", 1.0, None, "Quick light sword slash cutting through cloth, soft swish ending in a muted thud, very fast decay, warm, no metallic ring, close-up, dry"),
    "chime": ("sfx", 1.5, None, "Single small jade bell struck once, clear bright glassy tone, instant attack, short ringing decay, close-up, light reverb"),
    "boss_cast": ("sfx", 2.0, None, "Rushing wind and airy hiss swirling and rising in pitch, sparkling magical shimmer growing brighter, breath of air, windy, high frequency air noise, wide reverb"),
    "boss_strike": ("sfx", 3.0, None, "Huge bronze temple gong struck once with a deep low boom and a rush of air, long warm decay, large stone hall"),
    "boss_dash": ("sfx", 1.0, None, "Fast cloth and wind swoosh passing by, airy white noise sweep rising then falling, breathy hiss, sword whoosh, close-up"),
    # second direction: same instrument family as the approved menu track, so SFX and music share one world
    "qi_hit": ("sfx", 1.0, None, "Thin silvery sword energy slicing the air with a soft plucked guqin string at the end, very short, fast decay, ethereal, no metal clang, close-up, dry"),
    "qi_hit_str": ("instrument", 1.0, None, "Single muted guqin string pluck, short percussive attack, warm wooden body, instantly damped, dry close microphone"),
    "qi_cast": ("instrument", 2.0, None, "Breathy xiao bamboo flute swell rising in pitch with a soft guzheng glissando, gathering spiritual energy, airy and mysterious, temple reverb"),
    "qi_cast_sfx": ("sfx", 2.0, None, "Spiritual energy gathering, soft bamboo flute breath and tiny wind chimes rising slowly, gentle swell, ethereal, temple reverb"),
    "qi_strike": ("instrument", 3.0, None, "Large ancient bronze bianzhong bell struck once with a soft mallet, clear pitched tone, warm inharmonic overtones, long fading resonance, stone temple hall"),
    "qi_strike_sfx": ("sfx", 3.0, None, "Bronze temple bell struck once, clear ringing pitched tone with a soft wooden mallet attack, long resonance fading away, large stone hall"),
    "qi_dash": ("sfx", 1.0, None, "Silk ribbon whipping quickly through the air, soft fluttering swoosh, light and graceful, rising then falling, close-up, dry"),
    # the user's direction: a sword stab per hit, and one signature sound per character skill
    "stab": ("sfx", 1.0, None, "Sword blade stabbing into flesh, short deep wet thrust, thick muffled thud with a quick squelch, punchy, no metal ring, close-up, dry"),
    "sk_kiem": ("sfx", 1.5, None, "Many flying swords launching at once, rapid blades singing through the air, sharp swoosh, wuxia, close-up"),
    "sk_dan": ("sfx", 2.0, None, "Big fire burst igniting with a powerful whoosh, flames roaring then crackling and dying down, close-up"),
    "sk_loi": ("sfx", 3.0, None, "Sharp lightning crack followed by long rolling thunder rumble, deep roaring, fading away, outdoors, wide"),
    "sk_the": ("sfx", 2.0, None, "Stone ground cracking apart under a heavy punch, sharp rock crack and crumbling gravel debris scattering, punchy mid impact, close-up"),
    "sk_phu": ("sfx", 1.5, None, "Burst of magic fire from a burning paper charm, quick soft whoosh of flame with crackling sparks, fading fast, close-up"),
    "sk_ma": ("sfx", 2.0, None, "Dark demonic blood energy surge, deep guttural whoosh with a wet tearing sound, ominous, close-up"),
    # one signature sound per boss, on its Base-tier ultimate
    "ult_b1": ("sfx", 3.0, None, "Evil eye beam charging then sweeping, ominous deep humming energy ray with a sizzling crackle, slowly rising, dark magic"),
    "ult_b1b": ("sfx", 2.0, None, "Huge bat diving fast with leathery wings flapping and a piercing shriek, swooping past, wet blood spray, close-up"),
    "ult_b2": ("sfx", 3.0, None, "Burning meteor falling from the sky with a rising fiery roar, then a huge explosive impact and debris, wide outdoors"),
    "ult_b2b": ("sfx", 2.0, None, "Giant spider hissing as sticky web strands shoot out and snap taut, wet silk whip, skittering legs, close-up"),
    "ult_b3": ("sfx", 3.0, None, "Massive river of blood surging and swirling, thick liquid roar with deep gurgling waves, ominous, wide"),
    "ult_b4": ("sfx", 3.0, None, "Demon lord descending from the heavens, deep rumbling thunder with a dark reverberant boom shaking the ground, huge and ominous"),
    # replacements for the sounds that failed review: no bells, no metal, no wood, nothing dry or bright
    "r_bell": ("instrument", 2.0, None, "Gentle upward guzheng harmonic glissando, soft airy shimmer, warm and clear, light reverb"),
    "r_chime": ("instrument", 2.0, None, "Single clear guzheng harmonic note, round and pure, soft attack, long gentle fade, light reverb"),
    "r_coin": ("sfx", 1.0, None, "Small water droplet falling into a still pool, soft round plink with a tiny ripple, close-up"),
    "r_breakthrough": ("instrument", 2.0, None, "Rising dizi bamboo flute run with a guzheng glissando swelling into a warm uplifting note, clear, light reverb"),
    "r_error": ("instrument", 1.0, None, "Single low soft guqin note bending gently downward, warm and muted, short, dry"),
    "r_open": ("sfx", 1.0, None, "Soft silk cloth unrolling, gentle airy flutter, warm and light, close-up"),
    "r_tick": ("sfx", 1.0, None, "Single tiny water drop, very soft round plip, instantly gone, close-up, dry"),
    "r_click": ("instrument", 1.0, None, "Very soft short guzheng string tap, round warm tone, instantly damped, dry"),
    "r_confirm": ("instrument", 1.0, None, "Two gentle ascending guzheng notes, soft round attack, warm and clear, short"),
    "r_pickup": ("sfx", 1.0, None, "Tiny water bubble pop, quick soft round bloop, gentle, close-up, dry"),
    "r_shoot": ("sfx", 1.0, None, "Quick soft puff of energy, short breathy air pulse, warm and rounded, close-up"),
    "r_zap": ("sfx", 1.0, None, "Short soft electric spark, warm gentle crackle of static, quick decay, close-up"),
    "r_pluck": ("instrument", 1.0, None, "Single warm guzheng pluck, round soft attack, clear tone, gentle decay"),
    "r_heavy": ("sfx", 1.0, None, "Deep soft padded body thump, warm heavy impact with a push of air, fast decay, close-up"),
    "r_swing": ("sfx", 1.0, None, "Soft breathy swish of air, gentle quick movement, warm, rounded, close-up"),
    "menu": ("music", 60.0, 72, "Serene traditional Chinese fantasy instrumental with slow guzheng arpeggios, breathy dizi bamboo flute melody, soft erhu pads and a distant temple bell, misty mountain mood, calm and hopeful, no drums"),
    "battle": ("music", 90.0, 100, "Traditional Chinese fantasy instrumental with brisk guzheng arpeggios, lively dizi bamboo flute melody and flowing erhu, light hand drums and small cymbals, wuxia journey through misty mountains, agile and spirited, melodic, no brass, no orchestra"),
    "boss": ("music", 90.0, 150, "Fast traditional Chinese battle instrumental with one clear erhu lead melody over steady driving guzheng ostinato and tanggu drums, simple clean arrangement, few instruments, tension builds section by section into a powerful heroic peak, then sustains, no pauses, no slow intro"),
    "boss_climax": ("music", 90.0, 170, "Furious relentless traditional Chinese final battle instrumental, blazing nonstop guzheng and pipa ostinato, thundering tanggu drums driving every beat, fierce erhu and suona calls, desperate and heroic, full intensity from the first second, no pauses, no silence, no slow intro"),
}


def backend(model, device):
    from audio_studio_mcp.backends.sa3_torch import SA3TorchBackend
    return SA3TorchBackend(model=model, device=device)


def gen(names, n, seed0, device, seconds=None, cfg=1.0, init=None, strength=1.0, tag=""):
    from audio_studio_mcp import post, prompts
    from audio_studio_mcp.backends.base import GenerationRequest
    for model, kind in (("sm-sfx", "sfx"), ("sm-music", "instrument"), ("sm-music", "music")):
        todo = [x for x in names if SPEC[x][0] == kind]
        if not todo: continue
        be = backend(model, device)
        for name in todo:
            _, secs, bpm, text = SPEC[name]
            secs = seconds or secs
            prompt, _ = prompts.prepare_prompt(text, kind, secs, bpm)
            seeds = list(range(seed0, seed0 + n))
            res = be._generate(seeds, GenerationRequest(prompt=prompt, seconds=secs, cfg=cfg, init_audio=init, init_strength=strength))
            d = OUT / (name + tag); d.mkdir(parents=True, exist_ok=True)
            for seed, audio in zip(seeds, res.audios):
                p = d / f"{name}_s{seed}.wav"
                post.write(p, audio / max(1.0, float(np.abs(audio).max()) / 0.98), be.sample_rate, "wav")   # the model overshoots 1.0: scale instead of clipping
                p.with_suffix(".json").write_text(json.dumps({"model": model, "seed": seed, "seconds": secs, "cfg": cfg, "init": init, "strength": strength, "prompt": prompt}, indent=1), encoding="utf-8")
            vram = ""
            if be.device.startswith("cuda"):
                vram = f" peak VRAM {be._torch.cuda.max_memory_allocated() / 2**30:.2f} GB"
            print(f"{name}: {n} clips in {res.wall_seconds:.1f} s{vram}", flush=True)


def metrics(a, sr):
    """a: mono float. dur = audible length, centroid/hi = brightness, attack = ms to the peak."""
    env = np.abs(a)
    peak = float(env.max()) if a.size else 0.0
    if peak <= 1e-5: return dict(dur=0.0, peak_db=-120.0, rms_db=-120.0, centroid=0.0, hi=0.0, attack_ms=0.0)
    on = np.nonzero(env > peak * 0.03)[0]   # -30 dB relative to the peak
    body = a[on[0]:on[-1] + 1]
    spec = np.abs(np.fft.rfft(body * np.hanning(body.size))) ** 2
    f = np.fft.rfftfreq(body.size, 1 / sr)
    return dict(dur=round(body.size / sr, 3), peak_db=round(20 * np.log10(peak), 1),
                rms_db=round(10 * np.log10(np.mean(body ** 2) + 1e-12), 1),
                centroid=round(float((spec * f).sum() / (spec.sum() + 1e-12))),
                hi=round(float(spec[f > 6000].sum() / (spec.sum() + 1e-12)), 3),
                attack_ms=round((int(env.argmax()) - on[0]) / sr * 1000, 1))


def gate(a, sr):
    """a: (samples, channels). Technical faults only, never taste. burst = samples jumping by more than 60% of the clip's peak: a digital click."""
    m = a.mean(axis=1); peak = float(np.abs(a).max()) + 1e-12
    steps = np.abs(np.diff(a, axis=0)).max(axis=1)
    S = np.abs(np.fft.rfft(m)) ** 2; f = np.fft.rfftfreq(m.size, 1 / sr)
    r = dict(burst=int((steps > 0.6 * peak).sum()), dc=round(float(m.mean()), 4), clip=int((np.abs(a) >= 0.999).sum()),
             phone_db=round(float(10 * np.log10(S[f >= 300].sum() / (S.sum() + 1e-12) + 1e-12)), 1))   # small speakers reproduce little below 300 Hz
    r["fails"] = [k for k, bad in (("nonfinite", not np.isfinite(a).all()), ("burst", r["burst"] > 8), ("dc", abs(r["dc"]) > 0.01), ("clip", r["clip"] > 0), ("silent", peak < 1e-3)) if bad]
    return r


def lufs(path):
    ff = shutil.which("ffmpeg")
    if not ff: return None
    r = subprocess.run([ff, "-hide_banner", "-nostats", "-i", str(path), "-af", "ebur128=framelog=quiet", "-f", "null", "-"], capture_output=True, text=True)
    m = re.findall(r"I:\s+(-?[\d.]+) LUFS", r.stderr)
    return float(m[-1]) if m else None


def measure(d):
    import soundfile as sf
    print(f"{'file':30} {'dur':>6} {'peak':>6} {'rms':>6} {'lufs':>6} {'centr':>6} {'hi>6k':>6} {'atk':>6} {'burst':>6} {'phone':>6} fails")
    for p in sorted(Path(d).glob("*.wav")):
        a, sr = sf.read(p, always_2d=True)
        m = metrics(a.mean(axis=1), sr)
        g = gate(a, sr)
        print(f"{p.stem:30} {m['dur']:6.2f} {m['peak_db']:6.1f} {m['rms_db']:6.1f} {lufs(p) or 0:6.1f} {m['centroid']:6d} {m['hi']:6.3f} {m['attack_ms']:6.0f} {g['burst']:6d} {g['phone_db']:6.1f} {','.join(g['fails'])}")


# game name: (source clip, target RMS dB of the body, low-pass Hz or None). RMS targets match the Kenney clips they replace (-16 dB).
PICK = {
    "hit0": ("stab_c2/stab_s1001", -16, 8000), "hit1": ("stab_c2/stab_s1004", -16, 8000), "hit2": ("stab/stab_s1003", -16, 8000),
    "hit3": ("stab/stab_s1006", -16, 8000), "hit4": ("stab/stab_s1010", -16, 8000),
    "sk_dan": ("sk_dan_c2/sk_dan_s1005", -18, None), "sk_loi": ("sk_loi_c2/sk_loi_s1009", -18, None), "sk_ma": ("sk_ma/sk_ma_s1009", -18, None),
    "sk_the": ("sk_the/sk_the_s1009", -18, 4000),
    "ult_b1": ("ult_b1_c2/ult_b1_s1007", -19, None), "ult_b1b": ("ult_b1b_c2/ult_b1b_s1005", -19, 6500), "ult_b2": ("ult_b2_c2/ult_b2_s1002", -19, None),
    "ult_b2b": ("ult_b2b_c2/ult_b2b_s1009", -19, 6500), "ult_b3": ("ult_b3/ult_b3_s1001", -19, None), "ult_b4": ("ult_b4/ult_b4_s1000", -19, None),
}
PICK.update({   # replacements; game name = file name. The model failed chime and click, so they reuse bell and drop takes
    "bell": ("r_bell/r_bell_s1010", -20, None), "sk_chime": ("r_bell/r_bell_s1009", -20, None), "breakthrough": ("r_breakthrough/r_breakthrough_s1000", -19, None),
    "coin": ("r_coin_c2/r_coin_s1004", -18, None), "pickup": ("r_pickup/r_pickup_s1000", -18, None), "tick": ("r_tick/r_tick_s1000", -18, None), "click": ("r_tick/r_tick_s1002", -18, None),
    "confirm": ("r_confirm/r_confirm_s1002", -20, None), "error": ("r_error/r_error_s1000", -18, None), "open": ("r_open/r_open_s1000", -20, 3500),
    "shoot": ("r_shoot_c2/r_shoot_s1000", -18, None), "zap": ("r_zap/r_zap_s1006", -19, 3000), "pluck": ("r_pluck/r_pluck_s1007", -18, None),
    "heavy": ("r_heavy/r_heavy_s1000", -17, None), "swing0": ("r_swing/r_swing_s1000", -18, None), "swing1": ("r_swing/r_swing_s1004", -18, None), "swing2": ("r_swing/r_swing_s1001", -18, None),
})
# a second clip mixed under the pick, dB relative to it: the crack alone has no weight, the demon boom alone vanishes on phones
LAYER = {"sk_the": ("old_v3/sk_the/sk_the_s1000", 3), "ult_b4": ("sk_loi_c2/sk_loi_s1005", -5)}
# track: (source clip, bpm, loop crossfade ms, target LUFS)
LOOP = {"menu": ("menu/menu_s1005", 72, 1500, -18), "battle": ("battle_init85/battle_s1005", 100, 300, -16),
        "boss_a": ("boss/boss_s1005", 150, 300, -16), "boss_b": ("boss_cl7/boss_s1004", 150, 300, -16), "boss_c": ("boss_climax_ib85/boss_climax_s1000", 150, 300, -16),
        "boss_climax": ("boss_climax_ib85/boss_climax_s1000", 170, 300, -16)}


def lowpass(a, sr, hz):
    """Gentle FFT low-pass: rolls off over one octave above hz."""
    S = np.fft.rfft(a); f = np.fft.rfftfreq(a.size, 1 / sr)
    return np.fft.irfft(S * np.clip(2 - f / hz, 0, 1), a.size)


def polish(a, sr, hit=False):
    """Clearer SFX: no sub rumble, less 200-450 Hz mud, spectral denoise for the small model's rasp, then real upper harmonics (the model makes little above 2 kHz)."""
    import soundfile as sf, tempfile
    ff = shutil.which("ffmpeg")
    if not ff: raise RuntimeError("polish needs ffmpeg on PATH")
    with tempfile.TemporaryDirectory() as td:
        i, o = Path(td) / "i.wav", Path(td) / "o.wav"
        sf.write(i, a, sr, subtype="FLOAT")
        base = "afftdn=nr=16:nf=-48:tn=1"
        if hit: chain = "highpass=f=35,equalizer=f=320:t=q:w=1:g=-2.5,afftdn=nr=12:nf=-50:tn=1,equalizer=f=3000:t=q:w=1:g=2"   # a stab needs its body: a deep cut turns it thin and bright
        elif metrics(a, sr)["centroid"] < 1500: chain = "highpass=f=70,equalizer=f=320:t=q:w=1.3:g=-6," + base + ",aexciter=amount=0.8:drive=4:blend=0:freq=3000:ceil=12000"   # dull clips only; exciter after denoise so it adds harmonics, not noise
        else: chain = "highpass=f=70," + base
        subprocess.run([ff, "-hide_banner", "-loglevel", "error", "-y", "-i", str(i), "-af", chain, str(o)], check=True)
        return sf.read(o)[0].astype(np.float64)


def jade_chime(sr):
    """Self-made bell partials for the crit layer (the model only gave noise for this)."""
    t = np.arange(int(sr * 0.5)) / sr
    return sum(g * np.sin(2 * np.pi * f * t) * np.exp(-t * d) for f, g, d in ((2093, 1.0, 9), (2093 * 2.76, 0.35, 16), (2093 * 5.4, 0.12, 30)))


def shape(a, sr, rms_db, ceiling_db=-1.5):
    from audio_studio_mcp import post
    a = post.trim_silence(a[None], sr)[0] if a.ndim == 1 else a
    a = post.fade(a[None], sr, 2, min(40, a.size / sr * 300))[0]
    m = metrics(a, sr); a = a * 10 ** ((rms_db - m["rms_db"]) / 20)
    return a * min(1.0, 10 ** (ceiling_db / 20) / float(np.abs(a).max()))


def best_loop(m, sr, bar, lo=50.0, hi=76.0):
    """Start and length (whole bars) whose 1 s windows match best, on a 4x decimated mono mix."""
    d, s2 = m[::4], sr // 4; best = (-1.0, 0, 0)
    for bars in range(int(np.ceil(lo / bar)), int(hi / bar) + 1):
        L = int(bars * bar * s2)
        for st in range(4 * s2, 14 * s2, s2 // 20):
            if st + L + s2 >= d.size - 3 * s2: break
            x, y = d[st:st + s2], d[st + L:st + L + s2]
            c = float((x * y).sum() / (np.sqrt((x * x).sum() * (y * y).sum()) + 1e-9))
            if c > best[0]: best = (c, st * 4, L * 4)
    return best


def finish():
    import soundfile as sf
    from audio_studio_mcp import post
    F = OUT / "final"; F.mkdir(exist_ok=True); done = {}; bad = []
    for name, (src, rms, lp) in PICK.items():
        a, sr = sf.read(OUT / f"{src}.wav", always_2d=True); m = a.mean(axis=1)
        if lp: m = lowpass(m, sr, lp)
        if name in LAYER:
            src2, db = LAYER[name]; w = sf.read(OUT / f"{src2}.wav", always_2d=True)[0].mean(axis=1); n = min(w.size, m.size)
            m = m[:max(m.size, n)].copy(); m[:n] += w[:n] * np.sqrt((m ** 2).mean() / ((w ** 2).mean() + 1e-12)) * 10 ** (db / 20)
        done[name] = shape(polish(m, sr, name.startswith("hit")), sr, rms)
    sr = 44100
    # Phu Tu: the model only gave noise, so a lighter flame (the fire clip pitched up by half) with a small bell on top
    f = sf.read(OUT / "sk_dan/sk_dan_s1001.wav", always_2d=True)[0].mean(axis=1); f = np.interp(np.arange(0, f.size, 1.5), np.arange(f.size), f)
    b = jade_chime(sr) * np.sqrt((f ** 2).mean()) * 2; n = max(f.size, b.size); x = np.zeros(n); x[:f.size] += f; x[:b.size] += b
    done["sk_phu"] = shape(polish(x, sr), sr, -18)
    for name, a in done.items():
        sf.write(F / f"{name}.wav", a, sr, subtype="PCM_16"); g = gate(a[:, None], sr); print(name, metrics(a, sr), g)
        bad += [name] if g["fails"] else []
    seams = []
    for name, (src, bpm, xf, target) in LOOP.items():
        a, sr = sf.read(OUT / f"{src}.wav", always_2d=True); a = a.T.astype(np.float32)
        if name == "boss_c":   # the approved climax track slowed to 150 BPM (pitch drops with it): same arc, a step darker
            x = np.arange(0, a.shape[1], 150 / 170); a = np.stack([np.interp(x, np.arange(c.size), c) for c in a]).astype(np.float32)
        c, st, L = best_loop(a.mean(axis=0), sr, 240 / bpm)
        n = int(sr * xf / 1000)
        loop = post.make_loop(a[:, st:st + L + n], sr, xf)
        loop, info = post.ffmpeg_process(loop, sr, sr, target, -1.5)
        post.write(F / f"bgm_{name}.ogg", loop, sr, "ogg")
        seams.append(np.concatenate([loop[:, -6 * sr:], loop[:, :6 * sr], np.zeros((2, sr))], axis=1))
        g = gate(loop.T, sr); bad += [name] if g["fails"] else []
        print(name, f"match {c:.2f} start {st / sr:.2f} len {loop.shape[1] / sr:.2f}", info, g)
    gap = lambda s: np.zeros(int(sr * s))
    seq = [("hit0", "hit1", "hit2", "hit3", "hit4"), ("sk_dan", "sk_loi", "sk_the", "sk_phu", "sk_ma"), ("ult_b1", "ult_b1b", "ult_b2", "ult_b2b", "ult_b3", "ult_b4")]
    seq2 = [("bell", "sk_chime", "breakthrough"), ("coin", "pickup", "tick", "click"), ("confirm", "error", "open"), ("shoot", "zap", "pluck", "heavy"), ("swing0", "swing1", "swing2")]
    sf.write(F / "nghe_sfx_thay_the.wav", np.concatenate([np.concatenate([np.concatenate([done[n], gap(0.6)]) for n in g] + [gap(1.2)]) for g in seq2]), sr, subtype="PCM_16")
    sf.write(F / "nghe_sfx.wav", np.concatenate([np.concatenate([np.concatenate([done[n], gap(0.6)]) for n in g] + [gap(1.2)]) for g in seq]), sr, subtype="PCM_16")
    post.write(F / "nghe_moi_noi_vong_lap.ogg", np.concatenate(seams, axis=1), sr, "ogg")
    print("GATE FAIL:" if bad else "gates ok", *bad)


# Recorded CC0 clips (OpenGameArt, see Assets/ThirdParty/CC0-Sounds.md). name: (layers as [pack/stem, dB], target RMS dB, max seconds, top cut dB above 7 kHz).
# Keep the full band: only trim, level, layer. Muffled output came from heavy EQ and low-passing the model's clips.
CC0 = {
    "hit0": ([("water/slime_15", 0), ("breaking/bfh1_hit_07", -5)], -16, .30, 0), "hit1": ([("water/slime_06", 0), ("breaking/bfh1_hit_07", -5)], -16, .30, 0),
    "hit2": ([("water/slime_13", 0), ("breaking/bfh1_hit_02", -6)], -16, .30, 0), "hit3": ([("creature/slime_03", 0), ("breaking/bfh1_hit_07", -5)], -16, .26, 0),
    "hit4": ([("creature2/slime_01", 0), ("breaking/bfh1_hit_02", -6)], -16, .26, 0),
    "sk_dan": ([("audition3/spell_fire_05", 0)], -17, 1.3, 0), "sk_loi": ([("bang/fw_03", 0), ("sfx100b/sfx100v2_thunder_01", -14)], -17, 3.2, 0),
    "sk_the": ([("breaking/bfh1_rock_breaking_02", 0), ("sfx100b/slam_01", -2)], -17, .9, 0), "sk_phu": ([("sfx100/paper_02", 0), ("audition2/spell_01", -3)], -18, .6, 0),
    "sk_ma": ([("creature/roar_03", 0), ("audition2/magical_2", -5)], -17, 1.2, 0),
    "ult_b1": ([("audition2/magical_5", 0)], -18, 1.6, 0), "ult_b1b": ([("creature/scream_02", 0)], -18, .9, 0), "ult_b2": ([("bang/cannon_04", 0), ("audition3/spell_fire_03", -2)], -17, 1.5, 3),
    "ult_b2b": ([("creature/bug_10", 0), ("water/slime_13", -3)], -18, .6, 0), "ult_b3": ([("water/splash_11", 0), ("water/loop_water_02", -6)], -18, 1.6, 0),
    "ult_b4": ([("bang/fw_04", 0), ("creature/roar_03", -3)], -17, 2.2, 0),
    "bell": ([("audition2/magical_2", 0)], -19, 1.5, 0), "sk_chime": ([("audition2/magical_7_0", 0)], -19, 1.5, 3), "breakthrough": ([("audition2/magical_6_0", 0)], -18, 1.2, 0),
    "coin": ([("water/splash_03", 0)], -18, .2, 0), "pickup": ([("sfx100/plop_01", 0)], -18, .1, 0), "tick": ([("sfx100/plop_02", 0)], -19, .1, 0), "click": ([("sfx100/paper_02", 0)], -20, .1, 0),
    "confirm": ([("audition2/spell_01", 0)], -19, .46, 0), "error": ([("sfx100b/sfx100v2_hit_01", 0)], -18, .17, 0), "open": ([("sfx100/paper_02", 0)], -19, .5, 0),
    "shoot": ([("creature/breath_02", 0)], -18, .22, 0), "zap": ([("audition2/spell_02", 0)], -19, .36, 4), "pluck": ([("water/bubble_03", 0)], -18, .43, 0),
    "heavy": ([("sfx100b/slam_01", 0)], -17, .28, 0), "swing0": ([("audition1/blade_01", 0)], -18, .2, 6), "swing1": ([("audition1/blade_03", 0), ("audition1/blade_01", -8)], -18, .2, 7), "swing2": ([("audition1/blade_03", 0)], -18, .2, 6),
}


# Designed sounds, built by feel rather than by name (mostly BigSoundBank, CC0). A layer is (id, start s, length s or 0 for the rest, pitch factor, gain dB, placed at s, reversed).
# name: (layers, target RMS dB, max seconds, room wet dB or None). Layers are peak-normalised so a boom and a tail mix by the dB shown.
L = lambda i, st=0, d=0, p=1, db=0, at=0, r=False: (i, st, d, p, db, at, r)
DESIGN = {
    # a stab: a knife through a courgette + a recorded punch
    "hit0": ([L("2068", .44, .2, 1, -4), L("2459", .1, .12, .8, 0)], -15, .22, None), "hit1": ([L("2068", 2.01, .16, 1, -4), L("2461", .06, .14, .8, 0)], -15, .2, None),
    "hit2": ([L("2068", 3.16, .16, 1, -4), L("2463", .05, .12, .8, 0)], -15, .2, None), "hit3": ([L("2068", 4.18, .18, 1, -4), L("2465", .11, .1, .8, 0)], -15, .2, None),
    "hit4": ([L("2068", 2.01, .16, 1.15, -4), L("2459", .1, .12, .7, 0)], -15, .2, None),
    # moves
    "dash": ([L("1796", .2, .24), L("0127", .05, .3, 1, -6)], -17, .26, None), "swing0": ([L("1798", .2, .14), L("0129", .02, .3, 1.3, -6)], -17, .15, None), "swing1": ([L("1795", .26, .16), L("0127", .05, .3, 1.2, -6)], -17, .17, None), "swing2": ([L("0127", .05, .3, .85)], -18, .28, None),
    "shoot": ([L("0549", .12, .14), L("1798", .2, .12, 1.2, -6)], -17, .18, None), "zap": ([L("3063", 0, .22)], -18, .22, None), "heavy": ([L("2463", .05, .14, .7), L("1836", .41, .14, 1, -3)], -15, .2, None),
    # character skills
    "sk_dan": ([L("1340", 0, 1.3), L("0987", .09, 1.3, 1, -2, .1)], -13, 1.3, None),
    "sk_loi": ([L("2949", .2, .1, .7, 0), L("3063", 0, .3, 1, -2), L("3115", .85, 2.6, 1, -3, .03)], -14, 2.6, None),
    "sk_the": ([L("1808", 0, .4, .55), L("1836", .41, .2, .8, -2), L("0576", .91, .12, .9, -3), L("1634", 5.79, 1.0, 1, -7, .02)], -11, 1.1, -12),
    "sk_phu": ([L("2961", .3, .34), L("1340", 0, .5, 1.3, -5, .12)], -15, .62, None),
    "sk_ma": ([L("1796", .2, .24, .85, -3), L("0486", 4.01, 1.1, .9, 0, .12), L("1881", .03, .8, .7, -5, .12)], -14, 1.2, None),
    # one signature per boss
    "ult_b1": ([L("3061", 0, 1.0, .85), L("3063", 0, .3, 1, -4, .05)], -15, 1.0, -14),
    "ult_b1b": ([L("2892", .19, .93), L("0127", .05, .3, .8, -4, .15)], -15, 1.0, None),
    "ult_b2": ([L("1796", .2, .24, .6), L("1807", 0, .2, .55, 0, .34), L("1023", .02, .6, 1, -2, .34), L("1634", 5.79, 1.0, 1, -7, .36)], -12, 1.7, -12),
    "ult_b2b": ([L("2962", .3, .3), L("2961", .3, .3, 1.25, -4, .1), L("slime_13", 0, .3, 1, -6, .1)], -15, .6, None),
    "ult_b3": ([L("1521", .13, 1.5, .72), L("1519", .15, 1.3, .55, -3, .05)], -13, 1.6, None),
    "ult_b4": ([L("1808", 0, .4, .6, -2), L("2949", .2, .1, .7, 0), L("3063", 0, .3, .9, -3), L("3115", .85, 2.2, 1, -3, .04)], -12, 2.3, -14),
    # events and UI: small real things, no chimes
    "bell": ([L("0127", .05, .32, .75)], -18, .36, None), "sk_chime": ([L("2894", .19, .9, 1.2)], -18, .8, None),
    "breakthrough": ([L("r_breakthrough_s1011"), L("0129", .02, .5, 1, -9, 0, True)], -16, 1.3, -16),   # level up climbs and brightens: a bamboo flute run, not a burst that dies away
    "coin": ([L("0648", .23, .1, 1.3)], -18, .1, None), "pickup": ([L("1074", .18, .06, 1.15)], -18, .07, None), "tick": ([L("0483", 1.14, .06)], -20, .06, None), "click": ([L("0483", 3.39, .06)], -20, .06, None),
    "confirm": ([L("0211", .02, .1, 1.1), L("1074", 1.51, .06, 1, -4, .03)], -18, .13, None), "error": ([L("0576", .91, .14, .8)], -17, .15, None), "open": ([L("0576", 1.77, .14), L("0127", .05, .2, 1.2, -6)], -18, .17, None),
    "pluck": ([L("1531", .03, .35, 1.2)], -18, .36, None),
}


# seconds each sound may last: the visual effect it belongs to (Skills.cs, BossMoves.cs) plus a short tail, so no sound outlives what is on screen
FIT = {"sk_dan": .7, "sk_loi": .8, "sk_the": .65, "sk_phu": .45, "sk_ma": .65, "ult_b1": 1.0, "ult_b1b": .7, "ult_b2": 1.05, "ult_b2b": .5, "ult_b3": 1.6, "ult_b4": .85,
       "sk_chime": .5, "bell": .3}


def design():
    import soundfile as sf
    root = Path("D:/Workspace/GameProject"); Q = root / "audio_gen" / "final_design"; Q.mkdir(exist_ok=True); done = {}; bad = []; sr = 48000
    def load(i):
        for p in [root / "cc0_src/bsb" / f"{i}.{e}" for e in ("flac", "ogg")] + sorted((root / "cc0_src/water").glob(f"{i}.*")) + [root / "audio_gen/r_breakthrough_c2" / f"{i}.wav"]:
            if p.exists():
                try: a, r = sf.read(p, always_2d=True); break
                except Exception: continue
        else: raise FileNotFoundError(i)
        m = a.mean(axis=1)
        return np.interp(np.arange(0, m.size, r / sr), np.arange(m.size), m) if r != sr else m
    cache = {}
    for name, (layers, rms, cap, wet) in DESIGN.items():
        n = int(cap * sr); mix = np.zeros(n)
        for i, st, d, pitch, db, at, rev in layers:
            m = cache.setdefault(i, load(i)); a = m[int(st * sr):int((st + d) * sr) if d else None]
            if pitch != 1: a = np.interp(np.arange(0, a.size, pitch), np.arange(a.size), a)   # tape-style: pitch and speed together, the classic way to add size
            if rev: a = a[::-1]
            a = a * 10 ** (db / 20) / (np.abs(a).max() + 1e-9); o = int(at * sr); k = min(a.size, n - o); mix[o:o + k] += a[:k]
        if wet is not None:   # a short room from decaying noise, so big hits have a space around them
            ir = np.random.default_rng(1).standard_normal(int(.7 * sr)) * np.exp(-np.arange(int(.7 * sr)) / (.16 * sr))
            N = mix.size + ir.size; w = np.fft.irfft(np.fft.rfft(mix, N) * np.fft.rfft(ir, N), N)[:mix.size]
            mix = mix + w / (np.abs(w).max() + 1e-9) * np.abs(mix).max() * 10 ** (wet / 20)
        S = np.abs(np.fft.rfft(mix)) ** 2; F = np.fft.rfftfreq(mix.size, 1 / sr)
        if F[np.searchsorted(np.cumsum(S) / S.sum(), .85)] < 900:   # bass-heavy mixes read as a covered speaker: pull the sub down
            X = np.fft.rfft(mix); X *= np.clip(F / 160, .3, 1); mix = np.fft.irfft(X, mix.size)
        if name == "breakthrough": mix = polish(mix, sr)[:mix.size]   # the flute comes from the small model: denoise its rasp
        mix = mix - mix.mean(); f = min(int(.012 * sr), mix.size // 2); mix[:40] *= np.linspace(0, 1, 40); mix[-f:] *= np.linspace(1, 0, f)
        if name != "ult_b2":   # no lead-in: the sound must land on the frame of the action. These two rise on purpose, the code starts them early
            st = max(0, int(np.argmax(np.abs(mix) >= np.abs(mix).max() * (.5 if name.startswith(("swing", "ult_b3")) else .2))) - int(.008 * sr)); mix = mix[st:]; mix[:40] *= np.linspace(0, 1, 40)
        if name in FIT and mix.size > FIT[name] * sr:   # a natural decay over the last part, not a cut
            mix = mix[:int(FIT[name] * sr)].copy(); t = int(mix.size * .5); mix[-t:] *= np.cos(np.linspace(0, np.pi / 2, t)) ** 2
        k = 10 ** ((rms - metrics(mix, sr)["rms_db"]) / 20); lim = 10 ** (-1 / 20)
        mix = mix * min(k, lim / float(np.abs(mix).max()))   # plain scaling: soft clipping made big hits sound raspy
        done[name] = mix; sf.write(Q / f"{name}.wav", mix, sr, subtype="PCM_16"); g = gate(mix[:, None], sr)
        if g["fails"] == ["burst"] and float(np.abs(np.diff(mix[int(.03 * sr):])).max()) < .6 * float(np.abs(mix).max()): g["fails"] = []   # only the attack jumps: that is the hit, not a click
        S = np.abs(np.fft.rfft(mix * np.hanning(mix.size))) ** 2; F = np.fft.rfftfreq(mix.size, 1 / sr); roll = float(F[np.searchsorted(np.cumsum(S) / S.sum(), .85)])
        print(f"{name:13} dur {mix.size / sr:5.2f} rms {metrics(mix, sr)['rms_db']:6.1f} roll85 {roll:6.0f} centroid {metrics(mix, sr)['centroid']:5d} burst {g['burst']:3d} {' '.join(g['fails'])}")
        bad += [name] if g["fails"] else []
    gap = lambda t: np.zeros(int(sr * t)); both = lambda gs: np.concatenate([np.concatenate([np.concatenate([done[x], gap(.5)]) for x in g] + [gap(1.0)]) for g in gs])
    sf.write(Q / "nghe_1_chien_dau.wav", both([[f"hit{i}" for i in range(5)], ["dash", "swing0", "swing1", "swing2"], ["shoot", "zap", "heavy"]]), sr, subtype="PCM_16")
    sf.write(Q / "nghe_2_chieu_va_boss.wav", both([["sk_dan", "sk_loi", "sk_the", "sk_phu", "sk_ma"], ["ult_b1", "ult_b1b", "ult_b2", "ult_b2b", "ult_b3", "ult_b4"]]), sr, subtype="PCM_16")
    sf.write(Q / "nghe_3_su_kien_giao_dien.wav", both([["bell", "sk_chime", "breakthrough"], ["coin", "pickup", "tick", "click"], ["confirm", "error", "open", "pluck"]]), sr, subtype="PCM_16")
    print("GATE FAIL:" if bad else "gates ok", *bad)


def cc0():
    import soundfile as sf
    root = Path("D:/Workspace/GameProject"); Q = root / "audio_gen" / "final_cc0"; Q.mkdir(exist_ok=True); done = {}; bad = []
    def load(ref):
        pack, stem = ref.split("/")
        d = root / "sfx_audition" / {"audition1": "1_danh_trung_quai", "audition2": "2_boss_van_chieu", "audition3": "3_boss_ra_don"}[pack] if pack.startswith("audition") else root / "cc0_src" / pack
        p = next(x for dd in (d, *sorted((root / "cc0_src").iterdir())) if dd.is_dir() for x in sorted(dd.iterdir()) if x.stem == stem and x.suffix.lower() in (".wav", ".ogg"))
        a, sr = sf.read(p, always_2d=True); m = a.mean(axis=1)
        if sr != 44100: m = np.interp(np.arange(0, m.size, sr / 44100), np.arange(m.size), m)
        on = np.nonzero(np.abs(m) > np.abs(m).max() * 0.03)[0]
        return m[on[0]:on[-1] + 1]
    for name, (layers, rms, cap, top) in CC0.items():
        parts = [load(r) for r, _ in layers]; base = parts[0]; n = int(min(cap * 44100, max(x.size for x in parts))); mix = np.zeros(n)
        for (r, db), x in zip(layers, parts):
            x = x[:n] * np.sqrt((base ** 2).mean() / ((x ** 2).mean() + 1e-12)) * 10 ** (db / 20); mix[:x.size] += x
        if top:   # mild shelf, not a low-pass
            S = np.fft.rfft(mix); f = np.fft.rfftfreq(n, 1 / 44100); S *= 10 ** (-top / 20 * np.clip((f - 4000) / 3000, 0, 1)); mix = np.fft.irfft(S, n)
        m = np.concatenate([mix[:max(1, n - int(.03 * 44100))], mix[max(1, n - int(.03 * 44100)):] * np.linspace(1, 0, n - max(1, n - int(.03 * 44100)))])
        m = m - m.mean(); k = 10 ** ((rms - metrics(m, 44100)["rms_db"]) / 20); m = m * min(k, 10 ** (-1.5 / 20) / float(np.abs(m).max()))
        done[name] = m; sf.write(Q / f"{name}.wav", m, 44100, subtype="PCM_16"); g = gate(m[:, None], 44100)
        S = np.abs(np.fft.rfft(m * np.hanning(m.size))) ** 2; f = np.fft.rfftfreq(m.size, 1 / 44100); roll = float(f[np.searchsorted(np.cumsum(S) / S.sum(), 0.85)])
        flag = ("MUFFLED " if roll < 1200 else "") + ("BRIGHT " if roll > 7500 else "") + " ".join(g["fails"])
        bad += [name] if g["fails"] else []
        print(f"{name:13} dur {m.size / 44100:5.2f} roll85 {roll:6.0f} centroid {metrics(m, 44100)['centroid']:5d} burst {g['burst']:3d} {flag}")
    gap = lambda s: np.zeros(int(44100 * s)); both = lambda gs: np.concatenate([np.concatenate([np.concatenate([done[n], gap(0.5)]) for n in g] + [gap(1.0)]) for g in gs])
    sf.write(Q / "nghe_1_danh_trung_chieu_boss.wav", both([[f"hit{i}" for i in range(5)], ["sk_dan", "sk_loi", "sk_the", "sk_phu", "sk_ma"], ["ult_b1", "ult_b1b", "ult_b2", "ult_b2b", "ult_b3", "ult_b4"]]), 44100, subtype="PCM_16")
    sf.write(Q / "nghe_2_con_lai.wav", both([["bell", "sk_chime", "breakthrough"], ["coin", "pickup", "tick", "click"], ["confirm", "error", "open"], ["shoot", "zap", "pluck", "heavy"], ["swing0", "swing1", "swing2"]]), 44100, subtype="PCM_16")
    print("GATE FAIL:" if bad else "gates ok", *bad)


def selftest():
    sr = 44100; t = np.arange(sr) / sr
    assert metrics(np.zeros(100), sr)["dur"] == 0.0
    low, high = metrics(0.5 * np.sin(2 * np.pi * 200 * t), sr), metrics(0.5 * np.sin(2 * np.pi * 9000 * t), sr)
    assert abs(low["peak_db"] + 6) < 0.2 and abs(low["dur"] - 1) < 0.01
    assert abs(low["centroid"] - 200) < 20 and low["hi"] < 0.01 and high["hi"] > 0.9
    assert metrics(lowpass(np.sin(2 * np.pi * 9000 * t), sr, 3000), sr)["peak_db"] < -60   # above the roll-off
    tone = 0.5 * np.sin(2 * np.pi * 200 * t)[:, None]; assert not gate(tone, sr)["fails"]
    buzz = tone.copy(); buzz[100:140, 0] = 0.5 * (-1.0) ** np.arange(40); assert "burst" in gate(buzz, sr)["fails"]
    assert gate(tone, sr)["phone_db"] < -30 and gate(0.5 * np.sin(2 * np.pi * 2000 * t)[:, None], sr)["phone_db"] > -1
    x = np.sin(2 * np.pi * 440 * t[:sr * 1]); c, st, L = best_loop(np.tile(x, 90), sr, 2.0)
    assert c > 0.99 and L % int(2.0 * sr // 4 * 4) < 8
    print("selftest ok")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["gen", "measure", "finish", "cc0", "design", "selftest"])
    ap.add_argument("args", nargs="*")
    ap.add_argument("--n", type=int, default=4)
    ap.add_argument("--seed", type=int, default=1000)
    ap.add_argument("--seconds", type=float)
    ap.add_argument("--cfg", type=float, default=1.0)
    ap.add_argument("--init", help="restyle this clip instead of starting from noise")
    ap.add_argument("--strength", type=float, default=0.8)
    ap.add_argument("--tag", default="", help="suffix of the output folder")
    ap.add_argument("--cpu", action="store_true")
    o = ap.parse_args()
    if o.cmd == "selftest": selftest()
    elif o.cmd == "finish": finish()
    elif o.cmd == "cc0": cc0()
    elif o.cmd == "design": design()
    elif o.cmd == "measure": measure(o.args[0])
    else:
        t0 = time.time(); gen(o.args or list(SPEC), o.n, o.seed, "cpu" if o.cpu else "cuda", o.seconds, o.cfg, o.init, o.strength if o.init else 1.0, o.tag)
        print(f"total {time.time() - t0:.0f} s")
