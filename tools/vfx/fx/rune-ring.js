// Top-down rune seal burnt into the floor: two hand-drawn, slightly uneven rings, runes riding between them, a chasing light. Loops.
// Authored with the pixelfx core (MIT, Patch / Hugo Duprez); glyphs from its arcane library.
import { fit, rand, tileFbm, TAU, WHITE } from "../../core/index.js";
import { glyph } from "../arcane/_lib.js";

// a quarter turn a loop keeps the 12 runes (a pattern of three) seamless
const D = 4, NR = 12;

export const meta = {
  name: "Rune seal (top view)",
  desc: "A seal seen from above: uneven burnt rings, turning runes, a light running round it.",
  tags: ["arcane", "rune", "seal", "topdown", "loop"],
  size: [112, 112],
  origin: [56, 56],
  duration: D,
  loop: true,
  fps: 30,
  bg: "ground",
  options: { palette: ["#1f0a3a", "#3a1470", "#5a1fa8", "#7d2fe0", "#a24dff", "#c285ff", "#efdcff"], radius: 46, seed: 3 },
};

const gap = (a, b) => Math.abs(Math.atan2(Math.sin(a - b), Math.cos(a - b)));

export function draw(p, t, o) {
  const C = fit(o.palette, 7), R = o.radius, RI = R * 0.62, RR = R * 0.81, S = o.seed, ph = t / D, spin = (ph * TAU) / 4, chase = ph * TAU * 2;
  const lit = (a, w) => gap(a, chase) < w || gap(a, chase + Math.PI) < w;
  p.shade(-R - 5, -R - 5, R + 5, R + 5, (x, y, out) => {
    const d = Math.hypot(x, y), a = Math.atan2(y, x), h = rand(Math.floor(x), Math.floor(y), S);
    const w = 1 + 0.022 * Math.sin(5 * a + S) + 0.016 * Math.sin(11 * a + 2 * S) + 0.03 * tileFbm(Math.cos(a) * 3 + 9, Math.sin(a) * 3 + 9, 0, 0, S, 2);
    const e1 = Math.abs(d - R * w), e2 = Math.abs(d - RI * w);
    if (e1 < 1.7) { out.c = lit(a, 0.2) ? WHITE : lit(a, 0.5) ? C[6] : e1 < 0.8 ? C[5] : C[3]; return; }
    if (e2 < 1.2) { out.c = lit(a + 1, 0.25) ? C[6] : e2 < 0.6 ? C[4] : C[2]; return; }
    if (d > RI * w && d < R * w) { if (h < 0.22) { out.c = C[0]; out.a = 0.5; } return; }
    if (d < RI * w && h < 0.1 + 0.2 * (1 - d / RI)) { out.c = C[1]; out.a = 0.5; }
    if (d > R * w && d < R * w + 4 && h < 0.3 - (d - R * w) * 0.07) { out.c = C[1]; out.a = 0.7; }  // ragged soot outside the ring
  });
  for (let k = 0; k < NR; k++) {
    const g = spin + ((k + 0.5) * TAU) / NR, x = Math.cos(g) * RR, y = Math.sin(g) * RR, hot = lit(g, 0.25);
    glyph(p, [0, 3, 5][k % 3], x, y, hot ? WHITE : C[6], 1, hot ? C[5] : C[2]);
    p.glow(x, y, hot ? 9 : 5, C[4], hot ? 0.45 : 0.18);
  }
  p.glow(0, 0, R * 0.55, C[3], 0.12);
}
