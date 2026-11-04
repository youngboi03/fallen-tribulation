// A small flame tuft, tiled along the edge of fire rings and cones. Loops. Authored with the pixelfx core and fire helpers
// (MIT, Patch / Hugo Duprez); tongue recipe adapted from Sprite Fusion's open-source fire, MIT License, Copyright (c) 2026 Hugo Duprez.
import { fit } from "../../core/index.js";
import { noiseFor, hotFirst, flameTongue, ember, LAYERS, BACK_LAYERS } from "../fire/_lib.js";

export const meta = {
  name: "Flame tuft",
  desc: "Three small tongues of flame on a short root line, flickering; a lone ember rises.",
  tags: ["fire", "flame", "tuft", "topdown", "loop"],
  size: [40, 48],
  origin: [20, 40],
  duration: 0.9,
  loop: true,
  fps: 30,
  bg: "ground",
  options: { palette: ["#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff"], seed: 2 },
};

const D = meta.duration, NL = noiseFor(D);

export function draw(p, t, o) {
  const F = fit(o.palette, 9), H = hotFirst(F), S = o.seed, N = (rate, seed = 0, off = 0) => NL(t, rate, seed, off);
  const T = [[-6, -2, 11, 2, 1], [6, -2, 12, 2, 1], [0, 0, 19, 3.2, 0]];
  T.forEach(([x, y, h0, w, back], i) => {
    const n = (S * 7 + i) * 13, h = h0 * (0.85 + 0.2 * N(3.1, n) + 0.25 * Math.max(0, N(2.4, n + 70) - 0.5));
    flameTongue(p, x, y, h, w, N(2.2, n + 40) * 2 - x * 0.12, N, n, H, back ? BACK_LAYERS : LAYERS, 0.5, 1.2);
  });
  p.glow(0, -6, 16, H[4], 0.22);
  const age = (t / D) % 1;
  ember(p, 3 * Math.sin(age * 5), -12 - 22 * age, 1 - age, 1, [H[5], H[4], H[3], H[2]], age * 9);
}
