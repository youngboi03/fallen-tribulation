// Top-down scorch mark: a charred irregular patch of black and brown with a few embers that fade in and out. Loops.
// Authored with the pixelfx core (MIT, Patch / Hugo Duprez).
import { cycle, fit, hex, rand, tileFbm, TAU } from "../../core/index.js";

export const meta = {
  name: "Scorch mark (top view)",
  desc: "A burnt patch of floor: black soot, brown char, ragged edge, dying embers.",
  tags: ["fire", "scorch", "decal", "topdown", "loop"],
  size: [96, 96],
  origin: [48, 48],
  duration: 2.4,
  loop: true,
  fps: 30,
  bg: "ground",
  options: { palette: ["#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff"], radius: 38, seed: 6 },
};

const D = meta.duration, SOOT = [hex("#0b0809"), hex("#171011"), hex("#271a15"), hex("#3a271c")];

export function draw(p, t, o) {
  const F = fit(o.palette, 9), R = o.radius, S = o.seed;
  p.shade(-R - 6, -R - 6, R + 6, R + 6, (x, y, out) => {
    const d = Math.hypot(x, y), a = Math.atan2(y, x);
    const e = R * (0.8 + 0.09 * Math.sin(3 * a + S) + 0.07 * Math.sin(5 * a + 2 * S) + 0.05 * Math.sin(9 * a + 0.5) + 0.06 * tileFbm(Math.cos(a) * 3 + 9, Math.sin(a) * 3 + 9, 0, 0, S, 2));
    const k = d / e, n = tileFbm(x * 0.12 + 40, y * 0.12 + 40, 0, 0, S + 3, 3), h = rand(Math.floor(x), Math.floor(y), S);
    if (k > 1) {
      // soot halo: a thin ragged dither fading outward
      const fall = 1 - (k - 1) * 4.5;
      if (fall > 0 && h < fall * 0.55 + n * 0.12) (out.c = SOOT[1]), (out.a = 0.9);
      return;
    }
    out.c = k > 0.86 ? SOOT[3] : n > 0.25 ? SOOT[2] : n > -0.1 ? SOOT[1] : SOOT[0];
    if (k < 0.45 && h < 0.18) out.c = SOOT[0];
    if (k > 0.8 && h < 0.3) out.c = SOOT[2];
  });
  // embers: a few coals glowing in the char, each fading in and out on its own beat
  cycle(t, D, 9, S * 3 + 2, D, (age, L, r) => {
    const ang = r.range(0, TAU), rad = r.range(0.05, 0.7) * R, x = Math.cos(ang) * rad, y = Math.sin(ang) * rad, k = Math.sin((age / L) * Math.PI);
    if (k < 0.12) return;
    const c = k > 0.8 ? F[6] : k > 0.55 ? F[5] : k > 0.3 ? F[4] : F[3];
    p.px(x, y, c);
    if (k > 0.5) p.px(x + 1, y, F[2]);
    p.glow(x, y, 4 + 3 * k, c, 0.25 * k);
  });
}
