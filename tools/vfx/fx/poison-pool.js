// Top-down poison pool for Van Kiep Tu Tien: a slimy puddle with an irregular, slowly flowing edge, dark rim,
// glossy highlight, rising bubbles. Loops. Authored with the pixelfx core (MIT, Patch / Hugo Duprez).
// Copied into <pixelfx>/src/fx/vk/ by tools/vfx_export.py to render.
import { cycle, fit, tileFbm, TAU, WHITE, ease } from "../../core/index.js";

export const meta = {
  name: "Poison pool (top view)",
  desc: "A slimy green puddle seen from above: irregular flowing edge, dark rim, glossy highlight, bubbles that swell and pop.",
  tags: ["nature", "poison", "toxic", "puddle", "hazard", "topdown", "loop"],
  size: [104, 104],
  origin: [52, 52],
  duration: 1.6,
  loop: true,
  fps: 30,
  bg: "ground",
  options: { palette: ["#0d1a0c", "#173a14", "#265e18", "#3f8a1e", "#6cb82a", "#a8e04a", "#e0ff8a"], radius: 42, seed: 4 },
};

const D = meta.duration;

// the edge radius at an angle: lobes and bays that creep slowly, the same at t = 0 and t = D
function edge(a, t, R, S) {
  const ph = t / D;
  return R * (0.86 + 0.06 * Math.sin(3 * a + 1.1 + TAU * ph) + 0.05 * Math.sin(5 * a + 2.3 - TAU * ph) + 0.035 * Math.sin(8 * a + 0.4 + 2 * TAU * ph) + 0.03 * Math.sin(13 * a + S));
}

export function draw(p, t, o) {
  const C = fit(o.palette, 7), R = o.radius, S = o.seed, ph = t / D;
  p.glow(0, 0, R * 1.15, C[4], 0.18);
  p.shade(-R - 4, -R - 4, R + 4, R + 4, (x, y, out) => {
    const d = Math.hypot(x, y), a = Math.atan2(y, x), e = edge(a, t, R, S), k = d / e;
    if (k > 1.04) return;
    if (k > 1) return (out.c = C[0]); // outline
    if (k > 0.9) return (out.c = C[1]); // dark rim
    if (k > 0.82) return (out.c = C[2]); // wet lip
    // flowing body: slow veins of lighter slime drifting across a deep green
    const f = tileFbm(x * 0.09 + 20, y * 0.09 + ph * 4, 0, 4, S, 3) + 0.25 * Math.sin(x * 0.12 + y * 0.07 + TAU * ph);
    const c = f > 0.38 ? C[5] : f > 0.14 ? C[4] : f > -0.12 ? C[3] : C[2];
    if (c === C[5]) out.k = 0.18;
    return c;
  });
  // glossy highlight: a curved band on the upper left plus a hot pixel, drifting a little
  const sh = 1.2 * Math.sin(TAU * ph);
  for (let q = 3.6; q < 4.9; q += 0.05) {
    const r = R * 0.62 + sh * 0.4;
    p.px(Math.cos(q) * r, Math.sin(q) * r * 0.95, q < 4.0 ? C[5] : q < 4.5 ? C[6] : C[5]);
  }
  p.px(-R * 0.34, -R * 0.52 + sh * 0.3, WHITE), p.px(-R * 0.34 + 1, -R * 0.52 + sh * 0.3, C[6]);
  p.px(R * 0.3, R * 0.28, C[6]);
  // bubbles: swell, glint, pop in a ring and a few droplets
  cycle(t, D, 7, S * 5 + 1, 1.6, (age, L, r) => {
    const ang = r.range(0, TAU), rad = r.range(0.1, 0.62) * R, bx = Math.cos(ang) * rad, by = Math.sin(ang) * rad, big = r.range(3, 5.5);
    const k = age / L;
    if (k < 0.72) {
      const g = ease.outCubic(k / 0.72), rr = Math.max(1, big * g);
      p.ellipse(bx, by, rr, rr, (d) => (d > 0.78 ? C[1] : d > 0.45 ? C[4] : C[5]));
      p.px(bx - rr * 0.35, by - rr * 0.4, WHITE);
    } else {
      const e = (k - 0.72) / 0.28, rr = big + 7 * e;
      for (let a = 0; a < TAU; a += 0.7 / rr) p.dot(bx + Math.cos(a) * rr, by + Math.sin(a) * rr, e < 0.5 ? C[6] : C[5], 1 - e);
      if (e < 0.4) for (let j = 0; j < 4; j++) p.px(bx + Math.cos(j * 1.6 + 0.4) * rr * 0.5, by + Math.sin(j * 1.6 + 0.4) * rr * 0.5 - 4 * e, C[5]);
    }
  });
}
