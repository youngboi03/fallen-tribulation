using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    /// <summary>
    /// Move coroutines for the primitive kinds of spec section 5, driven by the BossMove fields (legend on BossMove).
    /// Every damaging part is telegraphed first (TeleFloor never undercut). Waiting only counts while Game.Playing,
    /// scaled by the boss freeze factor. Riders (after != null) read the host's lastPoints / lane.
    /// </summary>
    public static class BossMoves
    {
        public static IEnumerator Run(BossBrain b, BossMove m)
        {
            switch (m.kind)
            {
                case BMove.Dash: return m.after != null ? Echo(b, m) : Dash(b, m);
                case BMove.Fan: return Fan(b, m);
                case BMove.Ring: return Ring(b, m);
                case BMove.Spiral: return Spiral(b, m);
                case BMove.Cone: return Cone(b, m);
                case BMove.Zones: return m.after != null ? ZonesRider(b, m) : Zones(b, m);
                case BMove.Pillars: return Pillars(b, m);
                case BMove.Sweep: return Sweep(b, m);
                case BMove.Nova: return Nova(b, m);
                case BMove.Line: return Line(b, m);
                case BMove.Blink: return m.after != null ? BlinkEcho(b, m) : Blink(b, m);
                case BMove.Summon: return Summon(b, m);
                case BMove.Hook: return Todo(b, "hook " + (m.hook ?? m.id));
                default: return Todo(b, m.kind.ToString());
            }
        }

        static IEnumerator Todo(BossBrain b, string what)
        {
            Debug.Log("[BOSS] TODO " + what);
            yield return Wait(b, 0.3f);
        }

        /// <summary>Waits t seconds of boss time: Game.Dt while playing, times the freeze factor.</summary>
        public static IEnumerator Wait(BossBrain b, float t)
        {
            float x = 0f;
            while (x < t) { yield return null; x += Step(b); }
        }

        /// <summary>Boss time elapsed this frame.</summary>
        static float Step(BossBrain b)
        {
            var g = Game.I;
            if (g == null || !g.Playing || b.e == null) return 0f;
            return Game.Dt * b.e.TimeScale;
        }

        // ------------------------------------------------------------------ helpers
        static float Tele(BossBrain b, BossMove m) { float t = b.EffTele(m); b.LastTele = t; return t; }
        static float D(BossBrain b, BossMove m) => b.e.dmg * m.dmg;
        static Vector2 P => Game.I.player.Pos;
        static float Ang(Vector2 d) => Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        static Vector2 Dir(float deg) { float r = deg * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(r), Mathf.Sin(r)); }
        static float AimAt(BossBrain b) => Ang(P - b.e.Pos);

        static BossHazard Hz(BossBrain b, BossMove m, HShape s, Vector2 pos, float tele, float active, float dmg, float tick = 0f)
        {
            var h = BossHazard.Spawn(b.e, s, pos, tele, active, dmg, b.kit.col, tick);
            h.moveId = m.id;
            return h;
        }

        /// <summary>Pure telegraph: a hazard with no live phase (never hurts).</summary>
        static BossHazard Mark(BossBrain b, BossMove m, HShape s, Vector2 pos, float tele) => Hz(b, m, s, pos, tele, 0f, 0f);

        static void Shoot(BossBrain b, BossMove m, float deg, float speed)
        {
            Vector2 d = Dir(deg);
            var p = Projectile.FireHostile(b.e.Pos + d * b.e.radius * 0.6f, d * speed * b.ShotMul, D(b, m), b.kit.col);
            p.owner = b.e; p.moveId = m.id;
        }

        // ------------------------------------------------------------------ Dash
        /// <summary>Lane telegraph tracks the player until 0.2 s before the dash; n &gt; 1 adds faint decoy lanes; radius = end blast; hook "dark" tints the screen.</summary>
        static IEnumerator Dash(BossBrain b, BossMove m)
        {
            float tele = Tele(b, m), spd = b.e.speed * m.speed;
            int reps = Mathf.Max(1, m.reps);
            if (m.hook == "dark") Fx.ScreenTint(Color.black, 0.35f, reps * (tele + m.len / Mathf.Max(0.1f, spd) + 0.3f));
            b.lastPoints.Clear();
            for (int r = 0; r < reps; r++)
            {
                Vector2 start = b.e.Pos;
                float aim = AimAt(b);
                var lane = Mark(b, m, HShape.Bar, start, tele);
                lane.width = m.width;
                var decoys = new List<BossHazard>();
                for (int i = 1; i < m.n; i++)
                {
                    var d = Mark(b, m, HShape.Bar, start, tele);
                    d.width = m.width; d.col = Color.Lerp(b.kit.col, Color.gray, 0.6f) * new Color(1, 1, 1, 0.5f);
                    decoys.Add(d);
                }
                BossHazard blast = null;
                if (m.radius > 0f) { blast = Hz(b, m, HShape.Circle, start, tele, 0.2f, D(b, m)); blast.radius = m.radius; }
                Vector2 end = start; float laneLen = m.len;
                System.Action aimLane = () =>
                {
                    end = Arena.Clamp(start + Dir(aim) * m.len, b.e.radius * 0.5f);
                    laneLen = Mathf.Max(0.5f, (end - start).magnitude);
                    lane.angleDeg = aim; lane.len = laneLen;
                    for (int i = 0; i < decoys.Count; i++)
                    {
                        decoys[i].angleDeg = aim + (i % 2 == 0 ? 35f : -35f) * (1 + i / 2);
                        decoys[i].len = laneLen;
                    }
                    if (blast != null) { blast.pos = end; blast.transform.position = end; blast.tele = tele + laneLen / spd; }
                };
                aimLane();
                Sfx.Play("heavy", 0.6f, 0f, 0.6f);
                float t = 0f;
                while (t < tele)
                {
                    yield return null; t += Step(b);
                    if (t < tele - 0.2f) { aim = AimAt(b); aimLane(); }
                }
                // the run
                Vector2 dv = Dir(aim);
                b.Dashing = true; b.vel = dv * spd;
                Sfx.Play("swing", 0.8f, 0f, 0.5f);
                bool hit = false; float moved = 0f, el = 0f, maxT = laneLen / spd * 1.5f + 0.3f;
                Vector2 last = b.e.Pos;
                float reach = Mathf.Max(b.e.radius, m.width * 0.5f);
                while (moved < laneLen - 0.05f && el < maxT)
                {
                    yield return null;
                    float s = Step(b); el += s;
                    Vector2 now = b.e.Pos; float step = (now - last).magnitude; moved += step; last = now;
                    if (s > 0f && step < spd * s * 0.25f && el > 0.1f) break;   // blocked by the arena wall
                    var pl = Game.I.player;
                    if (!hit && Vector2.Distance(pl.Pos, now) < reach + pl.radius * 0.8f)
                        hit = BossBrain.HurtPlayer(b.e, m.id, D(b, m), now);
                    if (Random.value < 0.6f) Fx.Particle(now, -dv * 3f, new Color(b.kit.col.r, b.kit.col.g, b.kit.col.b, 0.6f), new Color(0, 0, 0, 0), 0.4f, 0.1f, 0.4f);
                }
                b.vel = Vector2.zero; b.Dashing = false;
                Fx.Shake(0.2f, 0.2f);
                b.lastPoints.Add(b.e.Pos);
                b.laneA = start; b.laneB = b.e.Pos; b.laneWidth = m.width;
                if (blast != null) yield return Wait(b, 0.25f);
                if (r < reps - 1) yield return Wait(b, 0.25f);
            }
        }

        /// <summary>Dash rider (Vọng Âm): an afterimage re-runs the host lane after gap s at speed x boss speed.</summary>
        static IEnumerator Echo(BossBrain b, BossMove m)
        {
            Vector2 a = b.laneA, c = b.laneB;
            float len = (c - a).magnitude;
            if (len < 0.5f) yield break;
            float tele = Mathf.Max(Mathf.Max(m.tele, m.gap), KiepNum.TeleFloor(b.kiep));
            b.LastTele = tele;
            float w = b.laneWidth > 0f ? b.laneWidth : 1.4f;
            var lane = Mark(b, m, HShape.Bar, a, tele);
            lane.angleDeg = Ang(c - a); lane.len = len; lane.width = w;
            yield return Wait(b, tele);
            float spd = Mathf.Max(0.5f, b.e.speed * m.speed);
            var ghost = Hz(b, m, HShape.Circle, a, 0.01f, len / spd, D(b, m));
            ghost.radius = w * 0.5f; ghost.vel = (c - a) / len * spd;
            yield return Wait(b, 0.2f);
        }

        // ------------------------------------------------------------------ bullets
        /// <summary>n bullets spread over angle toward the player; reps volleys gap s apart.</summary>
        static IEnumerator Fan(BossBrain b, BossMove m)
        {
            float tele = Tele(b, m), aim = AimAt(b);
            var cone = Mark(b, m, HShape.Cone, b.e.Pos, tele);
            cone.radius = 3.5f; cone.arcDeg = Mathf.Max(20f, m.angle); cone.angleDeg = aim;
            float t = 0f;
            while (t < tele)
            {
                yield return null; t += Step(b);
                if (t < tele - 0.2f) { aim = AimAt(b); cone.angleDeg = aim; cone.pos = b.e.Pos; }
            }
            b.lastPoints.Clear();
            int reps = Mathf.Max(1, m.reps);
            for (int v = 0; v < reps; v++)
            {
                if (v > 0) aim = AimAt(b);
                float reach = Mathf.Clamp((P - b.e.Pos).magnitude, 2f, 12f);
                for (int i = 0; i < m.n; i++)
                {
                    float a = m.n <= 1 ? aim : aim - m.angle * 0.5f + m.angle * i / (m.n - 1);
                    Shoot(b, m, a, m.speed);
                    b.lastPoints.Add(Arena.Clamp(b.e.Pos + Dir(a) * reach, 0.5f));
                }
                Sfx.Play("shoot", 0.5f, 0.05f, 0.7f);
                if (v < reps - 1) yield return Wait(b, m.gap);
            }
            yield return Wait(b, 0.2f);
        }

        /// <summary>n bullets over 360 degrees; each later volley is offset by half a step.</summary>
        static IEnumerator Ring(BossBrain b, BossMove m)
        {
            float tele = Tele(b, m);
            var ring = Mark(b, m, HShape.Ring, b.e.Pos, tele);
            ring.radius = b.e.radius + 1.6f; ring.radius2 = b.e.radius + 0.9f;
            yield return Wait(b, tele);
            float a0 = AimAt(b), step = 360f / Mathf.Max(1, m.n);
            int reps = Mathf.Max(1, m.reps);
            for (int v = 0; v < reps; v++)
            {
                for (int i = 0; i < m.n; i++) Shoot(b, m, a0 + step * (i + (v % 2) * 0.5f), m.speed);
                Fx.Ring(b.e.Pos, b.kit.col, 0.5f, 3f, 0.35f);
                Sfx.Play("shoot", 0.6f, 0f, 0.5f);
                if (v < reps - 1) yield return Wait(b, m.gap);
            }
            yield return Wait(b, 0.2f);
        }

        /// <summary>n arms of reps rays each, rotating speed deg/s for dur s, firing every gap s; rotation reverses at radius2 s.</summary>
        static IEnumerator Spiral(BossBrain b, BossMove m)
        {
            float tele = Tele(b, m);
            var mark = Mark(b, m, HShape.Circle, b.e.Pos, tele);
            mark.radius = b.e.radius + 1.4f;
            yield return Wait(b, tele);
            const float BulletSpeed = 4.2f, RaySpread = 12f;
            int arms = Mathf.Max(1, m.n), rays = Mathf.Max(1, m.reps);
            float baseA = AimAt(b), t = 0f, fire = 0f, gap = Mathf.Max(0.05f, m.gap);
            Sfx.Play("bell", 0.6f, 0f, 0.8f);
            while (t < m.dur)
            {
                if (fire <= 0f)
                {
                    fire += gap;
                    for (int i = 0; i < arms; i++)
                        for (int j = 0; j < rays; j++)
                            Shoot(b, m, baseA + i * 360f / arms + (j - (rays - 1) * 0.5f) * RaySpread, BulletSpeed);
                    Sfx.Play("shoot", 0.25f, 0.05f, 0.7f, 0.09f);
                }
                yield return null;
                float s = Step(b);
                t += s; fire -= s;
                float sign = m.radius2 > 0f && t >= m.radius2 ? -1f : 1f;
                baseA += m.speed * sign * s;
            }
        }

        // ------------------------------------------------------------------ hazards
        /// <summary>Cone of radius/angle in front of the boss, aimed at the player each time; reps strikes gap s apart.</summary>
        static IEnumerator Cone(BossBrain b, BossMove m)
        {
            float tele = Tele(b, m);
            int reps = Mathf.Max(1, m.reps);
            for (int r = 0; r < reps; r++)
            {
                var h = Hz(b, m, HShape.Cone, b.e.Pos, tele, 0.15f, D(b, m));
                h.radius = m.radius; h.arcDeg = m.angle; h.angleDeg = AimAt(b);
                Sfx.Play("swing", 0.5f, 0.1f, 0.7f);
                yield return Wait(b, tele + 0.15f);
                if (r < reps - 1) yield return Wait(b, m.gap);
            }
        }

        /// <summary>Point on the arena edge in direction deg from the centre, pulled inward by inset.</summary>
        public static Vector2 EdgePoint(float deg, float inset) { Vector2 d = Dir(deg); return d * Mathf.Max(0f, EdgeDist(Vector2.zero, d) - inset); }

        /// <summary>Quadrant cells (sx, sy = +-1) in random order, at most n of them.</summary>
        public static List<Vector2Int> PickQuadrants(int n)
        {
            var q = new List<Vector2Int> { new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(-1, -1), new Vector2Int(1, -1) };
            for (int i = q.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); var t = q[i]; q[i] = q[j]; q[j] = t; }
            return q.GetRange(0, Mathf.Clamp(n, 0, 4));
        }

        /// <summary>Zone centres for one wave. AroundPlayer/Random/Grid as before; EdgeRing = n points evenly round the arena edge,
        /// Radial = n points on a ring of radius 4 round the boss, Between = n points evenly between the boss and the player.</summary>
        static List<Vector2> ZonePoints(BossBrain b, BossMove m)
        {
            var pts = new List<Vector2>();
            Vector2 pp = P;
            if (m.layout == ZLayout.EdgeRing)
            {
                float a0 = Random.Range(0f, 360f);
                for (int i = 0; i < m.n; i++) pts.Add(EdgePoint(a0 + 360f * i / Mathf.Max(1, m.n), 1.2f));
                return pts;
            }
            if (m.layout == ZLayout.Radial)
            {
                float a0 = Random.Range(0f, 360f);
                for (int i = 0; i < m.n; i++) pts.Add(Arena.Clamp(b.e.Pos + Dir(a0 + 360f * i / Mathf.Max(1, m.n)) * 4f, 0.5f));
                return pts;
            }
            if (m.layout == ZLayout.Between)
            {
                for (int i = 0; i < m.n; i++) pts.Add(Arena.Clamp(Vector2.Lerp(b.e.Pos, pp, (i + 1f) / (m.n + 1f)), 0.5f));
                return pts;
            }
            int near = m.layout == ZLayout.AroundPlayer ? m.n : Mathf.Min(m.n, m.gaps);
            for (int i = 0; i < near; i++)
                pts.Add(i == 0 ? pp : Arena.Clamp(pp + Random.insideUnitCircle.normalized * m.radius * Random.Range(1.6f, 2.4f), 0.5f));
            if (m.layout == ZLayout.Grid)
            {
                int rest = m.n - near, cols = Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, rest)));
                for (int i = 0; i < rest; i++)
                {
                    int cx = i % cols, cy = i / cols, rows = Mathf.CeilToInt(rest / (float)cols);
                    pts.Add(new Vector2(-Arena.HalfW + (cx + 0.5f) * 2f * Arena.HalfW / cols, -Arena.HalfH + (cy + 0.5f) * 2f * Arena.HalfH / rows));
                }
            }
            else
                while (pts.Count < m.n)
                    pts.Add(new Vector2(Random.Range(-Arena.HalfW + 1f, Arena.HalfW - 1f), Random.Range(-Arena.HalfH + 1f, Arena.HalfH - 1f)));
            return pts;
        }

        /// <summary>Telegraphed circles that explode (life 0) or stay as puddles ticking dmg every tick s; reps waves gap s apart.
        /// dur &gt; 0: the circles follow the player for dur s before they lock. Checker: n x n cells over the arena, alternating halves.</summary>
        static IEnumerator Zones(BossBrain b, BossMove m)
        {
            float tele = Tele(b, m);
            int reps = Mathf.Max(1, m.reps);
            float active = m.life > 0f ? m.life : 0.2f, tick = m.life > 0f ? m.tick : 0f;
            for (int w = 0; w < reps; w++)
            {
                if (m.layout == ZLayout.Checker)
                {
                    float cell = Mathf.Max(1.5f, m.n);
                    int cols = Mathf.CeilToInt(2f * Arena.HalfW / cell), rows = Mathf.CeilToInt(2f * Arena.HalfH / cell);
                    for (int i = 0; i < cols; i++)
                        for (int j = 0; j < rows; j++)
                        {
                            if ((i + j + w) % 2 != 0) continue;
                            var h = Hz(b, m, HShape.Bar, new Vector2(-Arena.HalfW + i * cell, -Arena.HalfH + (j + 0.5f) * cell), tele, active, D(b, m), tick);
                            h.len = cell; h.width = cell; h.angleDeg = 0f;
                        }
                    Sfx.Play("tick", 0.5f, 0f, 0.8f);
                    yield return Wait(b, tele + active);
                    if (w < reps - 1) yield return Wait(b, m.gap);
                    continue;
                }
                if (m.layout == ZLayout.Quadrants)
                {   // one rectangle per picked quadrant of the arena; the rest stays safe
                    foreach (var q in PickQuadrants(m.n))
                    {
                        var h = Hz(b, m, HShape.Bar, new Vector2(q.x > 0 ? 0f : -Arena.HalfW, q.y * Arena.HalfH * 0.5f), tele, active, D(b, m), tick);
                        h.len = Arena.HalfW; h.width = Arena.HalfH; h.angleDeg = 0f;
                    }
                    Sfx.Play("tick", 0.5f, 0f, 0.8f);
                    yield return Wait(b, tele + active);
                    if (w < reps - 1) yield return Wait(b, m.gap);
                    continue;
                }
                var pts = ZonePoints(b, m);
                var hs = new List<BossHazard>(); var offs = new List<Vector2>();
                foreach (var p in pts)
                {
                    var h = Hz(b, m, HShape.Circle, p, tele + m.dur, active, D(b, m), tick);
                    h.radius = m.radius; hs.Add(h); offs.Add(p - P);
                }
                Sfx.Play("tick", 0.5f, 0f, 0.8f);
                if (m.dur > 0f)
                {   // follow the player, then lock
                    float t = 0f;
                    while (t < m.dur)
                    {
                        yield return null; t += Step(b);
                        for (int i = 0; i < hs.Count; i++)
                            if (hs[i] != null) { var q = Arena.Clamp(P + offs[i], 0.5f); hs[i].pos = q; hs[i].transform.position = q; }
                    }
                }
                yield return Wait(b, w < reps - 1 ? tele + m.gap : tele + 0.2f);
            }
        }

        /// <summary>Zones rider: puddles/rings where the host move left points (dash end, bullet landings, pillars); 0.5 s forming; n &gt; 1 caps live ones.</summary>
        static IEnumerator ZonesRider(BossBrain b, BossMove m)
        {
            if (m.gap > 0f) yield return Wait(b, m.gap);
            if (!b.owned.TryGetValue(m.id, out var list)) b.owned[m.id] = list = new List<BossHazard>();
            list.RemoveAll(h => h == null || !BossHazard.All.Contains(h));
            float active = m.life > 0f ? m.life : 0.2f, tick = m.life > 0f ? m.tick : 0f;
            foreach (var p in new List<Vector2>(b.lastPoints))
            {
                if (m.n > 1 && list.Count >= m.n) break;
                var h = Hz(b, m, HShape.Circle, p, m.tele, active, D(b, m), tick);
                h.radius = m.radius; list.Add(h);
            }
            yield return null;
        }

        /// <summary>n circles one after another, each locked on the player's position when its telegraph starts, gap s apart.</summary>
        static IEnumerator Pillars(BossBrain b, BossMove m)
        {
            float tele = Tele(b, m);
            b.lastPoints.Clear();
            if (m.ult) Fx.Apparition(Res.Tile(b.e.def.sprite), P, b.kit.col, 4f, tele + m.n * m.gap);
            for (int i = 0; i < m.n; i++)
            {
                Vector2 p = P;
                var h = Hz(b, m, HShape.Circle, p, tele, 0.2f, D(b, m));
                h.radius = m.radius;
                b.lastPoints.Add(p);
                Sfx.Play("tick", 0.4f, 0.1f, 1f);
                if (i < m.n - 1) yield return Wait(b, m.gap);
            }
            yield return Wait(b, tele + 0.2f);
        }

        // ------------------------------------------------------------------ Sweep
        /// <summary>Distance from o along d to the arena edge.</summary>
        public static float EdgeDist(Vector2 o, Vector2 d)
        {
            float best = 99f;
            if (d.x > 0.001f) best = Mathf.Min(best, (Arena.HalfW - o.x) / d.x); else if (d.x < -0.001f) best = Mathf.Min(best, (-Arena.HalfW - o.x) / d.x);
            if (d.y > 0.001f) best = Mathf.Min(best, (Arena.HalfH - o.y) / d.y); else if (d.y < -0.001f) best = Mathf.Min(best, (-Arena.HalfH - o.y) / d.y);
            return Mathf.Max(0.5f, best);
        }

        /// <summary>n beams of length len, each rotating angle deg over dur s around the boss; the telegraph is a faint wedge over the exact swept arc.
        /// radius = angular spacing of simultaneous beams; radius2 = safe gap (deg) behind the boss for a long arc; gap = s between consecutive beams
        /// (they then sweep side by side); speed = start offset from the player (deg). The aim is locked when the telegraph starts.</summary>
        static IEnumerator Sweep(BossBrain b, BossMove m)
        {
            float tele = Tele(b, m);
            int n = Mathf.Max(1, m.n);
            float aim = AimAt(b), arc = Mathf.Max(1f, m.angle), dur = Mathf.Max(0.1f, m.dur);
            float rot = Random.value < 0.5f ? -1f : 1f;
            bool rear = m.radius2 > 0f, seq = !rear && n > 1 && m.radius <= 0f;
            float interval = dur + Mathf.Max(0f, m.gap);
            var start = new float[n]; var at = new float[n];
            var wedges = new List<BossHazard>(); var beams = new BossHazard[n];
            float halfW = Mathf.Asin(Mathf.Clamp01(m.width * 0.5f / Mathf.Max(0.5f, m.len))) * Mathf.Rad2Deg;
            for (int i = 0; i < n; i++)
            {
                if (rear) start[i] = aim + 180f + rot * m.radius2 * 0.5f;
                else if (m.radius > 0f) start[i] = aim + i * m.radius;
                else if (seq) start[i] = aim + rot * (i - (n - 1) * 0.5f) * arc - rot * arc * 0.5f;
                else start[i] = aim - rot * (m.speed > 0f ? m.speed : arc * 0.5f);
                at[i] = tele + (seq ? i * interval : 0f);
                var w = Mark(b, m, HShape.Cone, b.e.Pos, at[i]);
                w.radius = m.len; w.arcDeg = Mathf.Min(359f, arc + 2f * halfW + 2f); w.angleDeg = start[i] + rot * arc * 0.5f;
                wedges.Add(w);
            }
            Sfx.Play("heavy", 0.5f, 0f, 0.7f);
            float t = 0f, total = at[n - 1] + dur;
            while (t < total)
            {
                yield return null; t += Step(b);
                Vector2 c = b.e.Pos;
                for (int i = 0; i < n; i++)
                {
                    if (beams[i] == null && t >= at[i])
                    {
                        var h = Hz(b, m, HShape.Bar, c, 0.01f, dur, D(b, m));
                        h.len = m.len; h.width = m.width; h.angleDeg = start[i]; h.spinDeg = rot * arc / dur;
                        beams[i] = h;
                        if (i == 0 || seq) Sfx.Play("swing", 0.5f, 0.1f, 0.6f);
                    }
                    if (beams[i] != null) { beams[i].pos = c; beams[i].transform.position = c; }
                }
                foreach (var w in wedges) if (w != null) { w.pos = c; w.transform.position = c; }
            }
            yield return Wait(b, 0.2f);
        }

        // ------------------------------------------------------------------ Nova
        public const float NovaBand = 0.9f;

        /// <summary>Damaging ring that grows radius to radius2 over dur, or collapses onto the player's position at telegraph start when radius &gt; radius2.
        /// gaps safe gaps of angle deg are fixed at telegraph time and drawn in it; reps rings gap s apart, each rotating its gaps by speed deg.
        /// hook "annulus": fire from the arena edge (radius) closing in to the safe circle radius2, then held for life s ticking every tick s.</summary>
        static IEnumerator Nova(BossBrain b, BossMove m)
        {
            float tele = Tele(b, m);
            bool collapse = m.radius > m.radius2, annulus = m.hook == "annulus";
            int reps = Mathf.Max(1, m.reps), gaps = Mathf.Max(0, m.gaps);
            float r0 = m.radius, r1 = m.radius2, dur = Mathf.Max(0.1f, m.dur);
            float interval = Mathf.Max(m.gap, KiepNum.TeleFloor(b.kiep));
            Vector2 c = collapse ? P : b.e.Pos;
            float baseAng = Random.Range(0f, 360f);
            var dirs = new List<float>();
            for (int i = 0; i < gaps; i++) dirs.Add(baseAng + 360f * i / gaps);
            float hold = annulus ? Mathf.Max(0f, m.life) : 0f;

            System.Action<int, float> telegraph = (k, time) =>
            {
                var mk = Mark(b, m, HShape.Annulus, c, time);
                mk.radius = Mathf.Max(r0, r1); mk.radius2 = annulus ? Mathf.Min(r0, r1) : collapse ? 0f : Mathf.Max(0f, Mathf.Min(r0, r1) - NovaBand);
                mk.gapDirsDeg = dirs; mk.gapWidthDeg = m.angle; mk.angleDeg = k * m.speed;
            };
            telegraph(0, tele);
            Sfx.Play("heavy", 0.5f, 0f, 0.7f);
            var live = new List<BossHazard>(); var ages = new List<float>();
            int next = 0; float t = 0f, total = tele + (reps - 1) * interval + dur + hold;
            while (t < total)
            {
                yield return null;
                float s = Step(b); t += s;
                while (next < reps && t >= tele + next * interval)
                {
                    var h = Hz(b, m, annulus ? HShape.Annulus : HShape.Ring, c, 0.01f, dur + hold, D(b, m), annulus ? m.tick : 0f);
                    h.radius = r0; h.radius2 = annulus ? r0 : Mathf.Max(0f, r0 - NovaBand);
                    h.gapDirsDeg = dirs; h.gapWidthDeg = m.angle; h.angleDeg = next * m.speed;
                    live.Add(h); ages.Add(0f);
                    Fx.Ring(c, b.kit.col, 0.4f, 2.5f, 0.3f);
                    Sfx.Play("heavy", 0.6f, 0.05f, 0.5f);
                    next++;
                    if (next < reps) telegraph(next, interval);
                }
                for (int i = 0; i < live.Count; i++)
                {
                    ages[i] += s;
                    if (live[i] == null) continue;
                    float f = Mathf.Clamp01(ages[i] / dur);
                    if (annulus) live[i].radius2 = Mathf.Lerp(r0, r1, f);
                    else { live[i].radius = Mathf.Lerp(r0, r1, f); live[i].radius2 = Mathf.Max(0f, live[i].radius - NovaBand); }
                }
            }
            yield return Wait(b, 0.2f);
        }

        // ------------------------------------------------------------------ Line
        /// <summary>Straight dangers: Radial = n lines from the boss to the arena edge (one aimed at the player), Grid = n vertical x reps horizontal lines across the arena,
        /// Between = a segment joining the two live pools nearest the player (skipped with fewer than two). Hits slow the player by slow for dur s.</summary>
        static IEnumerator Line(BossBrain b, BossMove m)
        {
            float tele = Tele(b, m), active = m.life > 0f ? m.life : 0.2f;
            System.Action<BossHazard> prep = h => { h.width = m.width; h.slow = m.slow; if (m.dur > 0f) h.slowDur = m.dur; };
            if (m.layout == ZLayout.Grid)
            {
                int rows = Mathf.Max(0, m.reps);
                for (int i = 0; i < m.n; i++)
                {
                    float x = -Arena.HalfW + (i + 1) * 2f * Arena.HalfW / (m.n + 1);
                    var h = Hz(b, m, HShape.Bar, new Vector2(x, -Arena.HalfH), tele, active, D(b, m));
                    h.angleDeg = 90f; h.len = 2f * Arena.HalfH; prep(h);
                }
                for (int j = 0; j < rows; j++)
                {
                    float y = -Arena.HalfH + (j + 1) * 2f * Arena.HalfH / (rows + 1);
                    var h = Hz(b, m, HShape.Bar, new Vector2(-Arena.HalfW, y), tele, active, D(b, m));
                    h.angleDeg = 0f; h.len = 2f * Arena.HalfW; prep(h);
                }
            }
            else if (m.layout == ZLayout.Between)
            {
                var pools = BossHazard.All.FindAll(h => IsPool(b, h));
                pools.Sort((x, y) => (x.pos - P).sqrMagnitude.CompareTo((y.pos - P).sqrMagnitude));
                if (pools.Count < 2) { Debug.Log("[BOSS] line between skipped: pools=" + pools.Count); yield return Wait(b, 0.3f); yield break; }
                var h = Hz(b, m, HShape.Segment, pools[0].pos, tele, active, D(b, m));
                h.a = pools[0].pos; h.b = pools[1].pos; prep(h);
            }
            else
            {
                Vector2 o = b.e.Pos; float aim = AimAt(b);
                for (int i = 0; i < m.n; i++)
                {
                    float a = aim + i * 360f / Mathf.Max(1, m.n);
                    var h = Hz(b, m, HShape.Bar, o, tele, active, D(b, m));
                    h.angleDeg = a; h.len = EdgeDist(o, Dir(a)); prep(h);
                }
            }
            Sfx.Play("tick", 0.5f, 0f, 0.8f);
            yield return Wait(b, tele + 0.4f);
        }

        static bool IsPool(BossBrain b, BossHazard h) => h != null && h.owner == b.e && h.shape == HShape.Circle && h.tick > 0f && !h.Telegraphing;

        // ------------------------------------------------------------------ Blink
        /// <summary>Telegraph circle (radius) at a point len..radius2 from where the player stands; the boss vanishes dur s before it goes live, reappears there and slashes.
        /// Rider (Huyet Phan): an afterimage slashes the same place again after gap s.</summary>
        static IEnumerator Blink(BossBrain b, BossMove m)
        {
            float tele = Tele(b, m);
            float lo = m.len > 0f ? m.len : 2.5f, hi = Mathf.Max(lo, m.radius2 > 0f ? m.radius2 : 4f);
            Vector2 pp = P, spot = Arena.Clamp(pp + Dir(Random.Range(0f, 360f)) * lo, 0.5f);
            for (int i = 0; i < 16; i++)
            {
                Vector2 q = pp + Dir(Random.Range(0f, 360f)) * Random.Range(lo, hi);
                if ((Arena.Clamp(q, 0.5f) - q).sqrMagnitude < 0.0001f) { spot = q; break; }
            }
            var h = Hz(b, m, HShape.Circle, spot, tele, 0.2f, D(b, m));
            h.radius = m.radius;
            Sfx.Play("tick", 0.5f, 0f, 0.8f);
            float vanish = Mathf.Min(tele, Mathf.Max(0.1f, m.dur));
            yield return Wait(b, tele - vanish);
            b.e.SetHidden(true);
            Fx.Burst(b.e.Pos, b.kit.col, 8, 3f, 0.1f, 0.4f, true);
            yield return Wait(b, vanish);
            b.e.transform.position = spot;
            b.e.SetHidden(false);
            b.lastPoints.Clear(); b.lastPoints.Add(spot);
            Fx.Ring(spot, b.kit.col, 0.4f, m.radius, 0.3f);
            Fx.Shake(0.2f, 0.2f);
            Sfx.Play("swing", 0.7f, 0f, 0.5f);
            yield return Wait(b, 0.35f);
        }

        static IEnumerator BlinkEcho(BossBrain b, BossMove m)
        {
            if (b.lastPoints.Count == 0) yield break;
            Vector2 p = b.lastPoints[0];
            float tele = Mathf.Max(Mathf.Max(m.tele, m.gap), KiepNum.TeleFloor(b.kiep));
            b.LastTele = tele;
            var h = Hz(b, m, HShape.Circle, p, tele, 0.2f, D(b, m));
            h.radius = m.radius;
            yield return Wait(b, tele + 0.2f);
        }

        // ------------------------------------------------------------------ Summon
        static float MinionHpFrac(string id) => id == "bat_small" ? 0.20f : 0.15f;

        static int Alive(BossBrain b, string id = null)
        {
            int n = 0;
            foreach (var e in Enemy.All) if (e != null && !e.dead && e.summoner == b && (id == null || e.def.id == id)) n++;
            return n;
        }

        /// <summary>Minions hp = fraction of the expected trash hp of the boss wave (times the Kiep factor), contact damage 0.3 x boss damage.</summary>
        static void OnMinion(BossBrain b, BossMove m, string defId, Enemy e)
        {
            if (b.pending.TryGetValue(defId, out var c)) b.pending[defId] = Mathf.Max(0, c - 1);
            if (b == null || b.e == null || b.e.dead) { e.Vanish(); return; }
            e.summoner = b;
            e.maxHp = e.hp = MinionHpFrac(defId) * WaveDirector.ExpectedTrashHp(b.e.wave) * KiepNum.HpMul(b.kiep, b.e.wave);
            e.dmg = 0.3f * b.e.dmg;
            e.lifeT = m.life;
        }

        /// <summary>Spawns n minions (def id in hook) around the boss through the wave director's spawn warning, never above kit.summonCap alive.
        /// Rider (Phun Moi): every gaps-th cast of the host move spawns one at a live puddle, at most n alive from this move.</summary>
        static IEnumerator Summon(BossBrain b, BossMove m)
        {
            var def = Content.Minion(m.hook);
            if (def == null) { Debug.Log("[BOSS] Summon unknown minion " + m.hook); yield return Wait(b, 0.3f); yield break; }
            bool rider = m.after != null;
            if (rider)
            {
                int c = (b.casts.TryGetValue(m.after, out var v) ? v : 0) + 1; b.casts[m.after] = c;
                if (m.gaps > 1 && c % m.gaps != 0) yield break;
            }
            int pend = 0; foreach (var kv in b.pending) pend += kv.Value;
            int room = b.kit.summonCap - Alive(b) - pend;
            if (rider) room = Mathf.Min(room, m.n - Alive(b, def.id) - (b.pending.TryGetValue(def.id, out var pi) ? pi : 0));
            int count = Mathf.Min(rider ? 1 : m.n, room);
            if (count <= 0) { yield return Wait(b, 0.2f); yield break; }

            var spots = new List<Vector2>();
            if (rider)
            {
                var pools = BossHazard.All.FindAll(h => IsPool(b, h));
                spots.Add(pools.Count > 0 ? pools[Random.Range(0, pools.Count)].pos : Arena.Clamp(b.e.Pos + Dir(Random.Range(0f, 360f)) * (b.e.radius + 1.5f), 0.5f));
            }
            else
            {
                float a0 = Random.Range(0f, 360f), tele = Tele(b, m);
                for (int i = 0; i < count; i++)
                {
                    Vector2 p = Arena.Clamp(b.e.Pos + Dir(a0 + 360f * i / count) * (b.e.radius + 1.8f), 0.5f);
                    spots.Add(p);
                    var mk = Mark(b, m, HShape.Circle, p, Mathf.Max(0.85f, tele));
                    mk.radius = 0.5f;
                }
                Sfx.Play("bell", 0.5f, 0.1f, 1f);
            }
            foreach (var p in spots)
            {
                b.pending[def.id] = (b.pending.TryGetValue(def.id, out var q) ? q : 0) + 1;
                string id = def.id;
                Game.I.dir.QueueSpawn(p, def, e => OnMinion(b, m, id, e));
            }
            yield return Wait(b, 0.3f);
        }
    }
}
