using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    /// <summary>Marker on every object a boss hook creates (clone, ghost, hand, gate, patch, safe, caster). Lives under BossHooks.Root.</summary>
    public class BossHookObj : MonoBehaviour
    {
        public BossBrain owner;
        public string kind;
        /// <summary>Boss-time seconds before it removes itself (0 = until its hook removes it).</summary>
        public float life;
        /// <summary>Moves its proxy brain is still running.</summary>
        public int busy;
        public SpriteRenderer sr;
        /// <summary>Brain that runs BossMoves primitives from this object's position (clones, ghosts, gates, casters).</summary>
        public BossBrain proxy;
        float age;

        void Update()
        {
            if (owner == null || owner.e == null || owner.e.dead) { BossHooks.Remove(this); return; }
            age += BossMoves.Step(owner);
            if (life > 0f && age >= life) { BossHooks.Remove(this, true); return; }
            if (sr != null && (kind == "clone" || kind == "ghost") && Game.I != null && Game.I.player != null)
            {
                sr.flipX = Game.I.player.Pos.x < transform.position.x;
                if (kind == "clone") sr.sortingOrder = Mathf.RoundToInt(-transform.position.y * 5f);
            }
        }

        void OnDestroy() { BossHooks.Objs.Remove(this); }
    }

    /// <summary>A cocoon (Enemy, EBeh.Static) that hatches spiderlings after its timer unless it is killed first.</summary>
    public class BossCocoon : MonoBehaviour
    {
        BossBrain b; Enemy e; float left, total; int reps;

        public void Init(BossBrain brain, Enemy en, float hatch, int count) { b = brain; e = en; left = total = Mathf.Max(0.1f, hatch); reps = count; }

        void Update()
        {
            if (e == null || e.dead || b == null || b.e == null || b.e.dead) return;
            left -= BossMoves.Step(b);
            if (left <= 0f) Hatch();
        }

        void Hatch()
        {
            var def = Content.Minion("spiderling");
            Vector2 p = e.Pos;
            if (def != null)
            {
                int room = b.kit.summonCap - BossMoves.Alive(b) - BossMoves.Pending(b);
                int n = Mathf.Min(reps, room);
                for (int i = 0; i < n; i++)
                {
                    var s = Enemy.Spawn(def, Arena.Clamp(p + Random.insideUnitCircle * 0.7f, 0.5f), b.e.wave, 1f, 1f);
                    BossMoves.SetupMinion(b, def.id, s, 0f);
                }
                Fx.Burst(p, def.blood, 14, 5f, 0.14f, 0.45f);
            }
            Sfx.Play("kill", 0.4f, 0.1f, 1.3f);
            e.Vanish();
        }
    }

    /// <summary>
    /// Signature moves that are more than one primitive (spec 6.1-6.6, kind BMove.Hook; the numbers are documented per row in BossKits.cs).
    /// Every object a hook makes sits under one BossHooksRoot, so ClearAll is one Destroy. Breakables (cocoons, war flags) are Enemy objects
    /// with summoner = the brain: weapons hit them, they never give credit or drops. Clones, ghosts and gates carry a proxy BossBrain that
    /// runs the normal BossMoves primitives from their own position (BossBrain.Pos) with a damage multiplier; their hazards and bullets
    /// still belong to the real boss, so BossHazard/Projectile.ClearOwner remove them.
    /// </summary>
    public static class BossHooks
    {
        internal static readonly List<BossHookObj> Objs = new List<BossHookObj>();
        static Transform root;
        public static Transform Root { get { if (root == null) root = new GameObject("BossHooksRoot").transform; return root; } }

        /// <summary>Cocoons alive at once from Kén Độc (spec 6.0); Vạn Trùng Phệ places its 6 regardless.</summary>
        public const int CocoonCap = 3;
        /// <summary>Vạn Ma Quy Tông: the signature move of each earlier boss, in order (boss id, move id).</summary>
        public static readonly string[,] GhostMoves = { { "b1", "ma_nhan_quet" }, { "b1b", "huyet_anh_luot" }, { "b2", "viem_hoan" }, { "b2b", "doc_ty_troi" }, { "b3", "huyet_luan" } };

        /// <summary>The one move per boss whose name is announced on screen (the earlier bosses' ghost moves, plus Vạn Ma Quy Tông for b4).</summary>
        public static string Signature(string bossId)
        {
            for (int i = 0; i < GhostMoves.GetLength(0); i++) if (GhostMoves[i, 0] == bossId) return GhostMoves[i, 1];
            return bossId == "b4" ? "van_ma_quy_tong" : null;
        }

        // ------------------------------------------------------------------ queries and cleanup
        public static IEnumerable<BossHookObj> Objects(BossBrain b, string kind)
        {
            foreach (var o in Objs) if (o != null && o.gameObject.activeSelf && (b == null || o.owner == b) && (kind == null || o.kind == kind)) yield return o;
        }

        public static int Count(BossBrain b = null, string kind = null) { int n = 0; foreach (var o in Objects(b, kind)) n++; return n; }

        public static bool HasClones(BossBrain b)
        {
            foreach (var o in Objs) if (o != null && o.owner == b && o.kind == "clone" && o.gameObject.activeSelf) return true;
            return false;
        }

        /// <summary>Removes one hook object now (deactivated at once, so its proxy coroutines stop this frame).</summary>
        public static void Remove(BossHookObj o, bool fx = false)
        {
            if (o == null) return;
            Objs.Remove(o);
            if (fx && o.gameObject.activeSelf) Fx.Burst(o.transform.position, new Color(0.7f, 0.7f, 0.8f, 0.7f), 8, 3f);
            o.gameObject.SetActive(false);
            Object.Destroy(o.gameObject);
        }

        /// <summary>Everything this boss's hooks left: hook objects, summoned minions, cocoons and flags (no drops). Boss death, Cancel, phase change.</summary>
        public static void ClearOwner(BossBrain b)
        {
            for (int i = Objs.Count - 1; i >= 0; i--)
            {
                var o = Objs[i];
                if (o == null) { Objs.RemoveAt(i); continue; }
                if (o.owner == b) Remove(o);
            }
            var l = Enemy.Snapshot();
            foreach (var e in l) if (e != null && !e.dead && e.summoner == b) e.Vanish();
            Enemy.Release(l);
        }

        /// <summary>Wave end, boss death, run end, menu: every hook object and every boss minion goes, in one Destroy of the root.</summary>
        public static void ClearAll()
        {
            var l = Enemy.Snapshot();
            foreach (var e in l) if (e != null && !e.dead && e.summoner != null) e.Vanish();
            Enemy.Release(l);
            foreach (var o in Objs) if (o != null) o.gameObject.SetActive(false);
            Objs.Clear();
            if (root != null) { Enemy.All.RemoveAll(e => e == null || e.transform.IsChildOf(root)); Object.Destroy(root.gameObject); root = null; }
        }

        // ------------------------------------------------------------------ dispatch
        public static IEnumerator Run(BossBrain b, BossMove m)
        {
            switch (m.hook)
            {
                case "phan_than": return Clones(b, m);
                case "huyet_anh_van_sat": return CloneDashes(b, m);
                case "ken_doc": return Cocoons(b, m, false);
                case "van_trung_phe": return Cocoons(b, m, true);
                case "phan_thien_chien_ky": return Flags(b, m);
                case "van_buc_trieu": return BatWall(b, m);
                case "liet_hoa_thanh": return FireCity(b, m);
                case "doc_hai": return PoisonSea(b, m);
                case "thien_ma_giang_the": return Hand(b, m);
                case "hu_khong_mon": return Gates(b, m);
                case "van_ma_quy_tong": return Ghosts(b, m);
                case "thien_dao_tich_diet": return HeavenEnd(b, m);
                default: return Unknown(b, m);
            }
        }

        static IEnumerator Unknown(BossBrain b, BossMove m)
        {
            Debug.Log("[BOSS] unknown hook " + m.hook);
            yield return BossMoves.Wait(b, 0.3f);
        }

        // ------------------------------------------------------------------ helpers
        static float HW => Arena.HalfW;
        static float HH => Arena.HalfH;

        static BossHookObj Make(BossBrain b, string kind, Vector2 pos, float life)
        {
            var go = new GameObject("hook_" + kind);
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            var o = go.AddComponent<BossHookObj>();
            o.owner = b; o.kind = kind; o.life = life;
            Objs.Add(o);
            return o;
        }

        static SpriteRenderer Look(BossHookObj o, Sprite s, Color c, float scale, int order, bool add = false)
        {
            var g = new GameObject("look");
            g.transform.SetParent(o.transform, false);
            g.transform.localScale = Vector3.one * scale;
            var r = g.AddComponent<SpriteRenderer>();
            r.sprite = s; r.color = c; r.sortingOrder = order;
            if (add) r.sharedMaterial = Res.AddMat;
            if (o.sr == null) o.sr = r;
            return r;
        }

        /// <summary>A copy of the boss body: same sprite, tint and size, no Enemy, no collider.</summary>
        static BossHookObj Clone(BossBrain b, Vector2 p, float life)
        {
            var o = Make(b, "clone", p, life);
            Look(o, Res.Tile(b.e.def.sprite), b.e.Tint, b.e.def.scale, Mathf.RoundToInt(-p.y * 5f));
            Proxy(b, o, b.kit, 1f);
            Fx.Burst(p, b.kit.col, 12, 4f, 0.12f, 0.4f, true);
            return o;
        }

        static BossBrain Proxy(BossBrain b, BossHookObj o, BossKit kit, float dmgMul)
        {
            var p = o.gameObject.AddComponent<BossBrain>();
            p.InitProxy(b, kit ?? b.kit, o.transform, dmgMul);
            o.proxy = p;
            return p;
        }

        /// <summary>Starts a primitive on the object's proxy (its own coroutine, gone with the object).</summary>
        static void Cast(BossHookObj o, BossMove m)
        {
            if (o == null || o.proxy == null) return;
            o.busy++;
            o.proxy.StartCoroutine(BossBrain.Flat(Done(o, BossMoves.Run(o.proxy, m))));
        }

        static IEnumerator Done(BossHookObj o, IEnumerator inner) { yield return inner; o.busy--; }

        static bool Alive(BossHookObj o) => o != null && o.gameObject.activeSelf;

        static IEnumerator WaitBusy(BossBrain b, List<BossHookObj> os, float timeout)
        {
            float t = 0f;
            while (t < timeout)
            {
                bool any = false;
                foreach (var o in os) if (Alive(o) && o.busy > 0) any = true;
                if (!any) yield break;
                yield return null; t += BossMoves.Step(b);
            }
        }

        /// <summary>A primitive row built from a hook row: same id (attribution), name, tier, telegraph and damage.</summary>
        static BossMove Sub(BossMove m, BMove kind) => new BossMove { id = m.id, name = m.name, kind = kind, tier = m.tier, ult = m.ult, tele = m.tele, dmg = m.dmg };

        static BossHazard FullField(BossBrain b, BossMove m, float tele, float active, float dmg, float tick)
        {
            var h = BossMoves.Hz(b, m, HShape.Bar, new Vector2(-HW, 0f), tele, active, dmg, tick);
            h.angleDeg = 0f; h.len = 2f * HW; h.width = 2f * HH;
            return h;
        }

        static Enemy Breakable(BossBrain b, string defId, Vector2 p, float hpFrac)
        {
            var def = Content.Minion(defId);
            if (def == null) return null;
            var e = Enemy.Spawn(def, p, b.e.wave, 1f, 1f);
            e.transform.SetParent(Root, true);
            e.summoner = b;
            e.maxHp = e.hp = Mathf.Max(1f, hpFrac * b.e.maxHp);
            e.dmg = 0f;
            return e;
        }

        // ------------------------------------------------------------------ b3 Ma Ảnh Huyết Y
        /// <summary>n clones appear beside the boss (telegraphed), then the boss and every clone fire a Ring of gaps bullets together; the clones
        /// keep firing every ~2 s until life runs out. The real boss shows a bright outline while they live (Enemy.Highlighted).</summary>
        static IEnumerator Clones(BossBrain b, BossMove m)
        {
            float tele = BossMoves.Tele(b, m);
            var ring = Sub(m, BMove.Ring); ring.n = Mathf.Max(1, m.gaps); ring.speed = m.speed; ring.reps = 1;
            Vector2 c = b.Pos;
            float a0 = BossMoves.Ang(BossMoves.P - c) + 90f;
            var spots = new List<Vector2>();
            for (int i = 0; i < m.n; i++) spots.Add(Arena.Clamp(c + BossMoves.Dir(a0 + 360f * (i + 1) / (m.n + 1)) * 4.5f, 1.5f));
            foreach (var p in spots) { var mk = BossMoves.Mark(b, m, HShape.Circle, p, tele); mk.radius = b.e.radius + 0.4f; }
            yield return BossMoves.Wait(b, tele);
            foreach (var p in spots)
            {
                var o = Clone(b, p, m.life);
                o.proxy.StartCoroutine(BossBrain.Flat(CloneLoop(o, ring)));
            }
            yield return BossMoves.Run(b, ring);
        }

        static IEnumerator CloneLoop(BossHookObj o, BossMove ring)
        {
            while (true)
            {
                yield return BossMoves.Run(o.proxy, ring);
                yield return BossMoves.Wait(o.proxy, 1.4f);
            }
        }

        // ------------------------------------------------------------------ b3 Huyết Ảnh Vạn Sát
        /// <summary>n clones round the player at distance radius; one by one (gap s apart) each dashes through the player's position
        /// (len 2 x radius, width, speed x boss speed), each lane telegraphed like a Dash ultimate.</summary>
        static IEnumerator CloneDashes(BossBrain b, BossMove m)
        {
            BossMoves.Tele(b, m);
            var dash = Sub(m, BMove.Dash); dash.n = 1; dash.reps = 1; dash.len = m.radius * 2f; dash.width = m.width; dash.speed = m.speed; dash.ult = true;
            Vector2 pp = BossMoves.P;
            float a0 = Random.Range(0f, 90f);
            var os = new List<BossHookObj>();
            for (int i = 0; i < m.n; i++) os.Add(Clone(b, Arena.Clamp(pp + BossMoves.Dir(a0 + 360f * i / Mathf.Max(1, m.n)) * m.radius, 1f), 0f));
            for (int i = 0; i < os.Count; i++)
            {
                Cast(os[i], dash);
                if (i < os.Count - 1) yield return BossMoves.Wait(b, m.gap);
            }
            yield return WaitBusy(b, os, 12f);
            foreach (var o in os) Remove(o, true);
            yield return BossMoves.Wait(b, 0.2f);
        }

        // ------------------------------------------------------------------ b2b Kén Độc / Vạn Trùng Phệ
        /// <summary>n cocoons (hp radius2 x boss max hp) beside the boss, or evenly round the arena edge (edge = Vạn Trùng Phệ); each hatches reps
        /// spiderlings after dur s unless killed. Kén Độc keeps at most CocoonCap cocoons alive; spiderlings respect kit.summonCap.</summary>
        static IEnumerator Cocoons(BossBrain b, BossMove m, bool edge)
        {
            int count = edge ? m.n : Mathf.Min(m.n, CocoonCap - BossMoves.Alive(b, "cocoon"));
            if (count <= 0) { yield return BossMoves.Wait(b, 0.2f); yield break; }
            float tele = BossMoves.Tele(b, m), a0 = Random.Range(0f, 360f);
            var spots = new List<Vector2>();
            for (int i = 0; i < count; i++)
            {
                float a = a0 + 360f * i / count;
                spots.Add(edge ? BossMoves.EdgePoint(a, 1.5f) : Arena.Clamp(b.Pos + BossMoves.Dir(a) * (b.e.radius + 1.6f), 1f));
            }
            foreach (var p in spots) { var mk = BossMoves.Mark(b, m, HShape.Circle, p, tele); mk.radius = 0.8f; }
            yield return BossMoves.Wait(b, tele);
            foreach (var p in spots)
            {
                var e = Breakable(b, "cocoon", p, m.radius2);
                if (e == null) continue;
                e.gameObject.AddComponent<BossCocoon>().Init(b, e, m.dur, m.reps);
                Fx.Burst(p, e.def.blood, 10, 3f);
            }
            yield return BossMoves.Wait(b, 0.3f);
        }

        // ------------------------------------------------------------------ b2 Phần Thiên Chiến Kỳ
        /// <summary>n war flags (hp radius2 x boss max hp) at the corners +-(13, 7); after the telegraph each carries one beam (len, width) turning
        /// angle deg over dur s. The beam is anchored to its flag: a dead flag ends it. Flags still standing vanish when the beams end.</summary>
        static IEnumerator Flags(BossBrain b, BossMove m)
        {
            float tele = BossMoves.Tele(b, m), dur = Mathf.Max(0.1f, m.dur);
            var corners = new[] { new Vector2(13f, 7f), new Vector2(-13f, 7f), new Vector2(-13f, -7f), new Vector2(13f, -7f) };
            float rot = Random.value < 0.5f ? -1f : 1f;
            var flags = new List<Enemy>(); var starts = new List<float>();
            for (int i = 0; i < Mathf.Min(m.n, 4); i++)
            {
                Vector2 p = corners[i];
                var f = Breakable(b, "war_flag", p, m.radius2);
                if (f == null) continue;
                float start = BossMoves.Ang(-p);   // beams start pointing at the arena centre
                var w = BossMoves.Mark(b, m, HShape.Cone, p, tele);
                w.radius = m.len; w.arcDeg = Mathf.Min(359f, m.angle); w.angleDeg = start + rot * m.angle * 0.5f; w.anchor = f;
                flags.Add(f); starts.Add(start);
                Fx.Pillar(p, b.kit.col, 3f, 0.5f);
            }
            yield return BossMoves.Wait(b, tele);
            for (int i = 0; i < flags.Count; i++)
            {
                var f = flags[i];
                if (f == null || f.dead) continue;
                var h = BossMoves.Hz(b, m, HShape.Bar, f.Pos, 0.01f, dur, BossMoves.D(b, m));
                h.len = m.len; h.width = m.width; h.angleDeg = starts[i]; h.spinDeg = rot * m.angle / dur; h.anchor = f;
            }
            yield return BossMoves.Wait(b, dur + 0.1f);
            foreach (var f in flags) if (f != null && !f.dead) f.Vanish();
        }

        // ------------------------------------------------------------------ b1b Vạn Bức Triều
        /// <summary>A bat wall spanning the arena sweeps across it in dur s; a gap of width cells near the player's height travels with it.
        /// The telegraph covers the whole swept field above and below the gap band.</summary>
        static IEnumerator BatWall(BossBrain b, BossMove m)
        {
            float tele = BossMoves.Tele(b, m), dur = Mathf.Max(0.5f, m.dur), gw = Mathf.Max(1.5f, m.width);
            const float Thick = 1.2f;
            bool fromLeft = BossMoves.P.x > 0f;   // starts on the far side of the player
            float x0 = fromLeft ? -HW : HW, vx = (fromLeft ? 1f : -1f) * 2f * HW / dur;
            float gapY = Mathf.Clamp(BossMoves.P.y + Random.Range(-3f, 3f), -HH + gw * 0.5f + 0.5f, HH - gw * 0.5f - 0.5f);
            float lo = gapY - gw * 0.5f, hi = gapY + gw * 0.5f;
            var a = BossMoves.Mark(b, m, HShape.Bar, new Vector2(-HW, (-HH + lo) * 0.5f), tele); a.angleDeg = 0f; a.len = 2f * HW; a.width = lo + HH; a.SetLook("swarm");
            var c = BossMoves.Mark(b, m, HShape.Bar, new Vector2(-HW, (hi + HH) * 0.5f), tele); c.angleDeg = 0f; c.len = 2f * HW; c.width = HH - hi; c.SetLook("swarm");
            yield return BossMoves.Wait(b, tele);
            var w1 = BossMoves.Hz(b, m, HShape.Bar, new Vector2(x0, -HH), 0.01f, dur, BossMoves.D(b, m));
            w1.angleDeg = 90f; w1.len = lo + HH; w1.width = Thick; w1.vel = new Vector2(vx, 0f);
            var w2 = BossMoves.Hz(b, m, HShape.Bar, new Vector2(x0, hi), 0.01f, dur, BossMoves.D(b, m));
            w2.angleDeg = 90f; w2.len = HH - hi; w2.width = Thick; w2.vel = new Vector2(vx, 0f);
            var bat = Res.Tile(Res.Px.MaBuc);
            Color bc = new Color(Mathf.Lerp(b.kit.col.r, 0.2f, 0.4f), Mathf.Lerp(b.kit.col.g, 0.05f, 0.4f), Mathf.Lerp(b.kit.col.b, 0.1f, 0.4f), 0.95f);
            float t = 0f;
            while (t < dur)
            {
                yield return null;
                float s = BossMoves.Step(b); t += s;
                if (s <= 0f) continue;
                for (int k = 0; k < 2; k++)
                {
                    float y = Random.Range(-HH, HH);
                    if (y > lo && y < hi) continue;
                    Fx.Particle(new Vector2(x0 + vx * t + Random.Range(-0.4f, 0.4f), y), new Vector2(vx * 0.25f, Random.Range(-1f, 1f)), bc, new Color(bc.r, bc.g, bc.b, 0f), 0.8f, 0.6f, 0.35f, bat, false, 1f, 132);
                }
            }
            yield return BossMoves.Wait(b, 0.2f);
        }

        // ------------------------------------------------------------------ b2 Liệt Hỏa Thành
        /// <summary>Fire closes from beyond the arena corners to a safe circle of radius round the arena centre in 1 s, then holds dur s ticking dmg
        /// every tick s; meanwhile the boss fires a telegraphed Fan (n, angle, speed) every gap s.</summary>
        static IEnumerator FireCity(BossBrain b, BossMove m)
        {
            float tele = BossMoves.Tele(b, m), r = Mathf.Max(1f, m.radius), hold = Mathf.Max(0.5f, m.dur);
            const float Close = 1f;
            float R = Mathf.Sqrt(HW * HW + HH * HH) + 0.5f;
            var mk = BossMoves.Mark(b, m, HShape.Annulus, Vector2.zero, tele); mk.radius = R; mk.radius2 = r;
            yield return BossMoves.Wait(b, tele);
            var h = BossMoves.Hz(b, m, HShape.Annulus, Vector2.zero, 0.01f, Close + hold, BossMoves.D(b, m), m.tick);
            h.radius = R; h.radius2 = R;
            var caster = Make(b, "caster", b.Pos, 0f); Proxy(b, caster, b.kit, 1f);
            var fan = Sub(m, BMove.Fan); fan.ult = false; fan.tele = 0f; fan.n = Mathf.Max(1, m.n); fan.angle = m.angle; fan.speed = m.speed; fan.reps = 1;
            float t = 0f, next = Close;   // the fans start once the ring has closed
            while (t < Close + hold)
            {
                yield return null;
                t += BossMoves.Step(b);
                if (h != null) h.radius2 = Mathf.Lerp(R, r, t / Close);
                if (!Alive(caster)) continue;
                caster.transform.position = b.Pos;
                if (t >= next && caster.busy == 0) { Cast(caster, fan); next = t + Mathf.Max(0.3f, m.gap); }
            }
            Remove(caster);
            yield return BossMoves.Wait(b, 0.2f);
        }

        // ------------------------------------------------------------------ b2b Độc Hải
        /// <summary>The whole floor floods for dur s (dmg every tick s) except n safe patches of radius radius that drift at speed units/s and
        /// bounce off the walls; the first patch starts within 2 units of the player. The patches show during the telegraph and drift once live.</summary>
        static IEnumerator PoisonSea(BossBrain b, BossMove m)
        {
            float tele = BossMoves.Tele(b, m), dur = Mathf.Max(0.5f, m.dur), pr = Mathf.Max(0.5f, m.radius), inset = pr + 0.5f;
            var holes = new List<Vector2>(); var vels = new List<Vector2>(); var patches = new List<BossHookObj>();
            Vector2 pp = BossMoves.P;
            for (int i = 0; i < m.n; i++)
            {
                Vector2 q = Arena.Clamp(pp + Random.insideUnitCircle * 2f, inset);
                if (i > 0)
                    for (int k = 0; k < 24; k++)
                    {
                        q = new Vector2(Random.Range(-HW + inset, HW - inset), Random.Range(-HH + inset, HH - inset));
                        bool ok = true;
                        foreach (var o in holes) if ((o - q).magnitude < pr * 2f + 1.5f) ok = false;
                        if (ok) break;
                    }
                holes.Add(q); vels.Add(BossMoves.Dir(Random.Range(0f, 360f)) * m.speed);
                var po = Make(b, "patch", q, 0f);
                Look(po, Res.Circle, new Color(0.85f, 1f, 0.85f, 0.3f), pr * 2f, 140);
                Look(po, Res.Ring, new Color(1f, 1f, 1f, 0.9f), pr * 2f / 0.9f, 141, true);
                patches.Add(po);
            }
            var fl = FullField(b, m, tele, dur, BossMoves.D(b, m), m.tick);
            fl.holes = holes; fl.holeR = pr;
            float t = 0f;
            while (t < tele + dur)
            {
                yield return null;
                float s = BossMoves.Step(b); t += s;
                if (t < tele) continue;
                for (int i = 0; i < holes.Count; i++)
                {
                    Vector2 q = holes[i] + vels[i] * s, v = vels[i];
                    if (Mathf.Abs(q.x) > HW - inset) { v.x = -v.x; q.x = Mathf.Clamp(q.x, -HW + inset, HW - inset); }
                    if (Mathf.Abs(q.y) > HH - inset) { v.y = -v.y; q.y = Mathf.Clamp(q.y, -HH + inset, HH - inset); }
                    holes[i] = q; vels[i] = v;
                    if (Alive(patches[i])) patches[i].transform.position = q;
                }
            }
            foreach (var o in patches) Remove(o);
            yield return BossMoves.Wait(b, 0.2f);
        }

        // ------------------------------------------------------------------ b4 Thiên Ma Giáng Thế
        /// <summary>A giant dark hand (the boss sprite) slams n of the 4 quadrants one after another, gap s apart; every slam is telegraphed from the
        /// start and the untouched quadrant is lit as the safe spot.</summary>
        static IEnumerator Hand(BossBrain b, BossMove m)
        {
            float tele = BossMoves.Tele(b, m);
            var qs = BossMoves.PickQuadrants(4);
            int n = Mathf.Clamp(m.n, 1, 3);
            var hands = new List<BossHookObj>(); var at = new List<float>();
            float total = tele + (n - 1) * m.gap + 0.45f;
            Color dark = new Color(0.12f, 0.06f, 0.18f, 0.9f);
            for (int i = 0; i < 4; i++)
            {
                Vector2 cen = new Vector2(qs[i].x * HW * 0.5f, qs[i].y * HH * 0.5f);
                if (i < n)
                {
                    for (int k = -1; k <= 1; k++)   // three overlapping lobes: the ink spreads unevenly, the corners of the quarter stay dry
                    {
                        var at2 = cen + new Vector2(k * HW * 0.29f, Random.Range(-1.4f, 1.4f));
                        var h = BossMoves.Hz(b, m, HShape.Circle, Arena.Clamp(at2, 1f), tele + i * m.gap, 0.25f, BossMoves.D(b, m));
                        h.radius = Random.Range(5f, 5.8f);
                    }
                    var o = Make(b, "hand", cen, 0f);
                    Look(o, Res.Tile(b.e.def.sprite), new Color(dark.r, dark.g, dark.b, 0f), 10f, 125);
                    hands.Add(o); at.Add(tele + i * m.gap);
                }
                else
                {
                    VfxFx.Ground("rune_gold", cen, 3.6f, total, -185, 0.2f, 0.3f);
                }
            }
            float t = 0f;
            var slammed = new bool[hands.Count];
            while (t < total)
            {
                yield return null;
                t += BossMoves.Step(b);
                for (int i = 0; i < hands.Count; i++)
                {
                    var o = hands[i];
                    if (!Alive(o)) continue;
                    float k = Mathf.Clamp01(1f - (at[i] - t) / 0.7f);
                    o.sr.color = new Color(dark.r, dark.g, dark.b, 0.85f * k);
                    o.sr.transform.localScale = Vector3.one * Mathf.Lerp(10f, 6.5f, k);
                    if (!slammed[i] && t >= at[i])
                    {
                        slammed[i] = true;
                        Fx.Shake(0.5f, 0.3f);
                        Fx.Burst(o.transform.position, b.kit.col, 30, 9f, 0.2f, 0.6f, true);
                        Fx.Ring(o.transform.position, b.kit.col2, 0.5f, 5f, 0.4f);
                        BossMoves.Sig(m);
                    }
                    if (t >= at[i] + 0.35f) Remove(o);
                }
            }
            foreach (var o in hands) Remove(o);
        }

        // ------------------------------------------------------------------ b4 Hư Không Môn
        /// <summary>n gates in arena corners; over dur s each fires reps volleys, each a telegraphed Fan of gaps bullets (angle, speed) at the player,
        /// so the bullets cross the arena diagonally.</summary>
        static IEnumerator Gates(BossBrain b, BossMove m)
        {
            var corners = new List<Vector2> { new Vector2(HW - 1.2f, HH - 1.2f), new Vector2(-HW + 1.2f, HH - 1.2f), new Vector2(-HW + 1.2f, -HH + 1.2f), new Vector2(HW - 1.2f, -HH + 1.2f) };
            for (int i = corners.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); var x = corners[i]; corners[i] = corners[j]; corners[j] = x; }
            var gates = new List<BossHookObj>();
            for (int i = 0; i < Mathf.Min(m.n, 4); i++)
            {
                var o = Make(b, "gate", corners[i], 0f);
                Look(o, Res.Ring, new Color(b.kit.col.r, b.kit.col.g, b.kit.col.b, 0.9f), 1.8f, 120, true);
                Look(o, Res.Glow, new Color(b.kit.col2.r, b.kit.col2.g, b.kit.col2.b, 0.6f), 2.6f, 119, true);
                Proxy(b, o, b.kit, 1f);
                gates.Add(o);
            }
            var fan = Sub(m, BMove.Fan); fan.n = Mathf.Max(1, m.gaps); fan.angle = m.angle; fan.speed = m.speed; fan.reps = 1;
            int reps = Mathf.Max(1, m.reps);
            float interval = Mathf.Max(0.3f, m.dur / reps);
            for (int v = 0; v < reps; v++)
            {
                foreach (var g in gates) if (Alive(g) && g.busy == 0) Cast(g, fan);
                yield return BossMoves.Wait(b, interval);
            }
            yield return WaitBusy(b, gates, 5f);
            foreach (var g in gates) Remove(g, true);
        }

        // ------------------------------------------------------------------ b4 Vạn Ma Quy Tông
        /// <summary>The ghosts of the five earlier bosses appear one by one (gap s apart) round the player; each casts its signature move
        /// (GhostMoves, looked up in its own kit) at dmg x its damage, as an ultimate (telegraph &gt;= 1 s). Banner: the original move name.</summary>
        static IEnumerator Ghosts(BossBrain b, BossMove m)
        {
            int n = Mathf.Min(m.n, GhostMoves.GetLength(0));
            var gs = new List<BossHookObj>();
            for (int i = 0; i < n; i++)
            {
                string bid = GhostMoves[i, 0], mid = GhostMoves[i, 1];
                var kit = Content.Kit(bid); var src = kit != null ? kit.Move(mid) : null;
                EnemyDef def = null; foreach (var d in Content.Bosses) if (d.id == bid) def = d;
                if (src == null || def == null) { Debug.Log("[BOSS] ghost missing " + bid + "/" + mid); continue; }
                Vector2 p = Arena.Clamp(BossMoves.P + BossMoves.Dir(90f + 72f * i) * 5f, 2f);
                var o = Make(b, "ghost", p, 0f);
                Look(o, Res.Tile(def.sprite), new Color(kit.col.r, kit.col.g, kit.col.b, 0.5f), def.scale, 120);
                Proxy(b, o, kit, m.dmg);
                var cast = src.Clone(); cast.ult = true; cast.after = null;
                b.HookLog.Add(mid + " x" + m.dmg.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                Debug.Log("[BOSS] ghost " + bid + " " + mid);
                Fx.Burst(p, kit.col, 16, 4f, 0.12f, 0.5f, true);
                o.proxy.StartCoroutine(BossBrain.Flat(GhostRun(o, cast)));
                gs.Add(o);
                if (i < n - 1) yield return BossMoves.Wait(b, m.gap);
            }
            float t = 0f;
            while (t < 15f && gs.Exists(Alive)) { yield return null; t += BossMoves.Step(b); }
            foreach (var o in gs) Remove(o);
            yield return BossMoves.Wait(b, 0.2f);
        }

        static IEnumerator GhostRun(BossHookObj o, BossMove cast)
        {
            o.busy++;
            yield return BossMoves.Run(o.proxy, cast);
            o.busy--;
            yield return BossMoves.Wait(o.proxy, 0.3f);
            Remove(o, true);
        }

        // ------------------------------------------------------------------ b4 Thiên Đạo Tịch Diệt
        /// <summary>Two stages: (1) n tribulation Pillars of radius radius chase the player gap s apart (tele / dmg); (2) lightning strikes the whole
        /// field except one safe circle of radius radius2 at the arena centre (tele2 floored at 1 s / dmg2). `len` is not used.</summary>
        static IEnumerator HeavenEnd(BossBrain b, BossMove m)
        {
            var p1 = Sub(m, BMove.Pillars); p1.ult = false; p1.n = Mathf.Max(1, m.n); p1.radius = m.radius; p1.gap = m.gap;
            yield return BossMoves.Run(b, p1);
            float t2 = Mathf.Max(m.tele2, 1f), r = Mathf.Max(0.5f, m.radius2);
            b.LastTele = t2;
            var safe = Make(b, "safe", Vector2.zero, 0f);
            Look(safe, Res.Glow, new Color(1f, 0.95f, 0.6f, 0.3f), r * 2.4f, 140, true);
            VfxFx.Ground("rune_gold", Vector2.zero, r, t2 + 0.45f, 141, 0.2f, 0.3f);
            var h = FullField(b, m, t2, 0.25f, b.e.dmg * m.dmg2 * b.dmgMul, 0f);
            h.holes = new List<Vector2> { Vector2.zero }; h.holeR = r;
            Fx.ScreenTint(new Color(0.7f, 0.6f, 1f), 0.3f, t2 + 0.4f);
            yield return BossMoves.Wait(b, t2);
            for (int k = 0; k < 10; k++)
            {
                Vector2 q = new Vector2(Random.Range(-HW, HW), Random.Range(-HH, HH));
                if (q.magnitude < r + 0.5f) q = q.normalized * (r + 1f + Random.value * 3f);
                Fx.Lightning(q + new Vector2(Random.Range(-1f, 1f), 14f), q, b.kit.col2, 0.4f, 0.3f, 12, 0.8f);
            }
            Fx.Shake(0.6f, 0.4f);
            yield return BossMoves.Wait(b, 0.45f);
            Remove(safe);
        }
    }
}
