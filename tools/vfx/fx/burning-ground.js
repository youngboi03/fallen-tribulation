// Top-down burning ground: flames standing over a round patch of floor, a bed of coals under them, embers rising.
// Loops. Authored with the pixelfx core and its fire helpers (MIT, Patch / Hugo Duprez); the tongue recipe is adapted from
// Sprite Fusion's open-source fire (https://www.spritefusion.com/pixel-art-vfx/fire), MIT License, Copyright (c) 2026 Hugo Duprez.
import { fit, mix, rand, hex, TAU, Rng } from "../../core/index.js";
import { noiseFor, hotFirst, flameTongue, ember, floorLight, looped, LAYERS, BACK_LAYERS, BIG_LAYERS } from "../fire/_lib.js";

export const meta = {
  name: "Burning ground (top view)",
  desc: "Flames over a round patch of floor: tongues of varied height lick and flicker over glowing coals, embers rise.",
  tags: ["fire", "ground", "hazard", "topdown", "loop"],
  size: [112, 120],
  origin: [56, 76],
  duration: 1.2,
  loop: true,
  fps: 30,
  bg: "ground",
  options: { palette: ["#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff"], radius: 38, seed: 5 },
};

const D = meta.duration, NL = noiseFor(D);

export function draw(p, t, o) {
  const F = fit(o.palette, 9), H = hotFirst(F), R = o.radius, S = o.seed, G = new Rng(S);
  const N = (rate, seed = 0, off = 0) => NL(t, rate, seed, off);
  const COAL = [hex("#120b0b"), hex("#2a1410"), hex("#4a1d12")];
  floorLight(p, 0, 0, R * 1.3, R * 1.3, mix(H[3], H[4], 0.5), 0.35);
  // the bed: dark char with coals breathing in it, ragged edge
  p.shade(-R - 2, -R - 2, R + 2, R + 2, (x, y, out) => {
    const a = Math.atan2(y, x), e = R * (0.88 + 0.06 * Math.sin(4 * a + S) + 0.05 * Math.sin(7 * a + 1) + 0.03 * Math.sin(11 * a)), k = Math.hypot(x, y) / e;
    if (k > 1) return;
    const h = rand(Math.floor(x / 2), Math.floor(y / 2), S);
    const g = (0.5 + 0.5 * Math.sin((t / D) * TAU * 2 + h * 40)) * (1.05 - k);
    if (h > 0.55 && g > 0.35) {
      out.k = 0.25 * g;
      return g > 0.75 ? H[3] : g > 0.55 ? H[4] : H[5];
    }
    return k > 0.9 ? COAL[1] : h > 0.3 ? COAL[1] : COAL[0];
  });
  // the flames: roots scattered over the disc (golden angle), back to front, taller towards the middle
  const roots = [];
  for (let i = 0; i < 12; i++) {
    const r = Math.sqrt((i + 0.5) / 12) * R * 0.86, a = i * 2.39996 + G.range(-0.2, 0.2);
    roots.push({ x: Math.cos(a) * r, y: Math.sin(a) * r, k: 1 - r / R, n: (S * 7 + i) * 13, w: G.range(0.9, 1.15) });
  }
  roots.sort((a, b) => a.y - b.y);
  for (const q of roots) {
    const base = 13 + 24 * Math.pow(q.k, 0.6), h = base * (0.85 + 0.2 * N(3.1, q.n) + 0.18 * Math.max(0, N(2.4, q.n + 70) - 0.5) * 2);
    const w = (3.8 + 3.2 * q.k) * q.w, back = q.y < -R * 0.25;
    flameTongue(p, q.x, q.y, h, w, N(2.2, q.n + 40) * 2 - q.x * 0.04, N, q.n, H, back ? BACK_LAYERS : w > 3 ? BIG_LAYERS : LAYERS, 0.5, 1.1 + h * 0.04);
  }
  // embers rising off the whole patch
  looped(t, D, 14, S * 3 + 1, (r) => ({ x: r.range(-0.8, 0.8) * R, y: r.range(-0.6, 0.6) * R, vx: r.range(-5, 5), life: r.range(0.9, 1.8), s: r.chance(0.2) ? 2 : 1, ph: r.range(0, TAU) }), (q, age) => {
    const k = 1 - age / q.life;
    ember(p, q.x + q.vx * age + 2 * Math.sin(age * 4 + q.ph), q.y - 6 - 34 * age, k, q.s, [H[5], H[4], H[3], H[2]], q.ph + age * 9);
  });
  p.glow(0, -8, R * 0.95, H[4], 0.2);
}
