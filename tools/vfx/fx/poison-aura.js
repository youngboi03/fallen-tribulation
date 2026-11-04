// Poison status on a character, no body drawn: green bubbles rise round it and pop, drips of slime fall, a sickly glow
// and a dotted ring on the floor. Loops. Authored with the pixelfx core (MIT, Patch / Hugo Duprez).
import { cycle, fit, TAU, WHITE, ease } from "../../core/index.js";

export const meta = {
  name: "Poison aura",
  desc: "Bubbles of poison rising round a body, slime drips, a sickly green glow, a ring on the floor.",
  tags: ["status", "poison", "aura", "loop"],
  size: [64, 80],
  origin: [32, 70],
  duration: 1.6,
  loop: true,
  fps: 30,
  bg: "ground",
  options: { palette: ["#0d1a0c", "#173a14", "#265e18", "#3f8a1e", "#6cb82a", "#a8e04a", "#e0ff8a"], seed: 8 },
};

const D = meta.duration;

export function draw(p, t, o) {
  const C = fit(o.palette, 7), S = o.seed, ph = t / D;
  p.glow(0, -22, 30, C[4], 0.2);
  // the ring on the floor: dotted, pulsing outward
  const rr = 15 + 6 * Math.sin(TAU * ph) * 0 + 5 * ph;
  for (let a = 0; a < TAU; a += 0.32) p.dot(Math.cos(a) * rr * 1.3, Math.sin(a) * rr * 0.45 + 1, C[5], 1 - ph);
  // bubbles rising round the body
  cycle(t, D, 8, S * 3 + 1, 1.3, (age, L, r) => {
    const x0 = r.range(-15, 15), k = age / L, y = -4 - 52 * ease.outQuad(k), x = x0 + 2.5 * Math.sin(age * 6 + x0), big = r.range(1.6, 3.4) * (1 - 0.4 * k);
    if (k > 0.86) {
      const e = (k - 0.86) / 0.14, q = big + 3 * e;
      for (let a = 0; a < TAU; a += 0.9 / q) p.dot(x + Math.cos(a) * q, y + Math.sin(a) * q, C[6], 1 - e);
      return;
    }
    p.ellipse(x, y, big, big, (d) => (d > 0.75 ? C[2] : d > 0.4 ? C[4] : C[5]));
    p.px(x - big * 0.35, y - big * 0.4, WHITE);
  });
  // drips falling off the body
  cycle(t, D, 4, S * 5 + 3, 0.7, (age, L, r) => {
    const x = r.range(-9, 9), y = -30 + r.range(0, 14) + 60 * age * age, k = age / L;
    p.px(x, y, C[5]), p.px(x, y - 1, C[4]);
    if (k > 0.8) p.dot(x, 2, C[5], 1 - (k - 0.8) * 5);
  });
}
