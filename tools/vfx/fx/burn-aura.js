// Burning status on a character, no body drawn: small flames lick up the sides at several heights, embers rise, a warm glow.
// Loops. Authored with the pixelfx core and fire helpers (MIT, Patch / Hugo Duprez); tongue recipe adapted from Sprite Fusion's
// open-source fire (https://www.spritefusion.com/pixel-art-vfx/fire), MIT License, Copyright (c) 2026 Hugo Duprez.
import { fit, Rng } from "../../core/index.js";
import { noiseFor, hotFirst, flameTongue, ember, looped, LAYERS, BACK_LAYERS } from "../fire/_lib.js";

export const meta = {
  name: "Burn aura",
  desc: "Flames licking up both sides of a body at several heights, embers rising.",
  tags: ["status", "fire", "burn", "aura", "loop"],
  size: [64, 80],
  origin: [32, 70],
  duration: 1.2,
  loop: true,
  fps: 30,
  bg: "ground",
  options: { palette: ["#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff"], seed: 3 },
};

const D = meta.duration, NL = noiseFor(D);

export function draw(p, t, o) {
  const F = fit(o.palette, 9), H = hotFirst(F), S = o.seed, N = (rate, seed = 0, off = 0) => NL(t, rate, seed, off);
  p.glow(0, -24, 30, H[4], 0.22);
  // [x, root y, height, half-width, back]
  const T = [[-12, -8, 13, 2.2, 1], [12, -8, 13, 2.2, 1], [-14, -24, 15, 2.4, 0], [14, -24, 17, 2.6, 0], [-9, -40, 12, 2, 0], [9, -40, 13, 2, 0], [0, -4, 10, 2.6, 0]];
  T.forEach(([x, y, h0, w, back], i) => {
    const n = (S * 7 + i) * 13, h = h0 * (0.85 + 0.22 * N(3.1, n) + 0.3 * Math.max(0, N(2.4, n + 70) - 0.5));
    flameTongue(p, x, y, h, w, N(2.2, n + 40) * 2 + x * 0.05, N, n, H, back ? BACK_LAYERS : LAYERS, 0.5, 1.2);
  });
  looped(t, D, 8, S * 3 + 1, (r) => ({ x: r.range(-14, 14), y: r.range(-40, -8), life: r.range(0.7, 1.2), ph: r.range(0, 6.3) }), (q, age) => {
    ember(p, q.x + 2 * Math.sin(age * 5 + q.ph), q.y - 26 * age, 1 - age / q.life, 1, [H[5], H[4], H[3], H[2]], q.ph + age * 9);
  });
}
