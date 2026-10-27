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

        /// <summary>Zone centres for one wave (layouts AroundPlayer, Random, Grid; others fall back to Random).</summary>
        static List<Vector2> ZonePoints(BossBrain b, BossMove m)
        {
            var pts = new List<Vector2>();
            Vector2 pp = P;
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
    }
}
