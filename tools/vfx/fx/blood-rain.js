// Top-down blood rain over an irregular patch: slanted streaks fall out of the sky and burst into small crowns on the ground. Loops.
// Authored with the pixelfx core (MIT, Patch / Hugo Duprez).
import { cycle, fit, TAU } from "../../core/index.js";

export const meta = {
  name: "Blood rain (top view)",
  desc: "Slanted drops of blood falling onto a patch of floor, each ending in a little crown splash.",
  tags: ["blood", "rain", "hazard", "topdown", "loop"],
  size: [112, 136],
  origin: [56, 96],
  duration: 1,
  loop: true,
  fps: 30,
  bg: "ground",
  options: { palette: ["#2a0508", "#661018", "#b81a20", "#e0302a", "#ff9070", "#ffc4a8"], radius: 36, seed: 7, height: 78, wind: 0.32 },
};

const D = meta.duration, FALL = 0.72;  // part of a drop's life spent in the air

export function draw(p, t, o) {
  const C = fit(o.palette, 6), R = o.radius, H = o.height, W = o.wind;
  cycle(t, D, 30, o.seed * 3 + 1, (g) => g.range(0.38, 0.55), (age, L, r) => {
    const a = r.range(0, TAU), rad = Math.sqrt(r.range(0, 1)) * R, gx = Math.cos(a) * rad, gy = Math.sin(a) * rad * 0.92;
    const q = age / L, big = r.chance(0.3), tail = big ? 13 : 8;
    if (q < FALL) {
      const k = q / FALL, y = gy - H * (1 - k * k), x = gx + W * H * (1 - k * k);
      p.line(x + W * tail, y - tail, x, y, big ? C[3] : C[2]);
      p.px(x, y, big ? C[5] : C[4]);
      return;
    }
    const s = (q - FALL) / (1 - FALL), rr = 1 + (big ? 7 : 4.5) * s, n = big ? 7 : 5, off = r.range(0, TAU);
    for (let i = 0; i < n; i++) {
      if (s > 0.6 && (i + Math.floor(s * 7)) % 2) continue;  // the crown breaks up as it fades
      const g = off + (i * TAU) / n;
      p.px(gx + Math.cos(g) * rr, gy + Math.sin(g) * rr * 0.8 - 2 * Math.sin(s * Math.PI), s < 0.5 ? C[4] : C[3]);
    }
    p.px(gx, gy, C[1]);
  });
}
