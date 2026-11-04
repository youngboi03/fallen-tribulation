using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    public enum HShape { Circle, Bar, Cone, Ring, Annulus, Segment }

    /// <summary>
    /// Non-projectile boss danger: beam, zone, ring, cone or line with a telegraph then a live phase.
    /// Fairness (spec 3.4): tick 0 = at most one hit; tick above 0 = at most one hit per max(tick, 0.5) s;
    /// tele 0 still gets a 0.5 s forming time. Timers run on Game.Dt scaled by the owner's freeze factor.
    /// For Ring/Annulus the gap directions are rotated by angleDeg (so spinDeg turns the gaps).
    /// slow = fraction of player move speed removed for slowDur (default 1) s after a hit (0.3 = 30% slower).
    /// </summary>
    public class BossHazard : MonoBehaviour
    {
        public static readonly List<BossHazard> All = new List<BossHazard>();
        public static int PlayerHits;
        public const float FormTime = 0.5f, MinTick = 0.5f;

        public Enemy owner;
        public HShape shape;
        public Vector2 pos;
        public float radius, radius2, len, width, angleDeg, arcDeg, gapWidthDeg, slow;
        /// <summary>How long the slow lasts after a hit (s).</summary>
        public float slowDur = 1f;
        public List<float> gapDirsDeg;
        public Vector2 a, b;
        public float tele, active, dmg, tick;
        public Color col;
        public Vector2 vel;
        public float spinDeg;
        /// <summary>Spell look (BossMove.look), see SetLook; empty = generic organic look.</summary>
        public string look;
        /// <summary>Where the boss stood when it cast (a poison glob arcs in from here).</summary>
        public Vector2 src;
        /// <summary>Status the hazard applies: -1 none, 0 Poison, 1 Burn. whileIn: every frame the player stands in it; otherwise only on a real hit.</summary>
        public int status = -1; public bool whileIn;
        float fadeOut; bool scorch, fxDone, meteorDone;
        string pool, seal, strike, tuft = "flame_tuft";   // effect clips of the look
        VfxFx sealFx; string stain; Color stainCol; bool stained;
        int seed;
        VfxFx fx;
        /// <summary>Boss move that made it ([BOSSHIT] attribution).</summary>
        public string moveId;
        /// <summary>Breakable object that carries this hazard (war flag); the hazard ends when it dies.</summary>
        public Enemy anchor { get => anchorE; set { anchorE = value; anchored = value != null; } }
        Enemy anchorE; bool anchored;
        /// <summary>Safe holes cut out of the shape (centres, radius holeR); Contains is false inside one.</summary>
        public List<Vector2> holes;
        public float holeR;
        static bool hinted;

        float age, hitCd;
        bool hitDone, gone, forming;
        readonly List<SpriteRenderer> pieces = new List<SpriteRenderer>();
        int used;
        static Material defMat;

        /// <summary>Sprite pieces drawn last frame (performance checks in tests).</summary>
        public int PieceCount => gone ? 0 : used;
        public bool Live => !gone && age >= tele && age < tele + active;
        /// <summary>Still in its telegraph (or forming) time.</summary>
        public bool Telegraphing => !gone && age < tele;

        public static BossHazard Spawn(Enemy owner, HShape shape, Vector2 pos, float tele, float active, float dmg, Color col, float tick = 0f)
        {
            var go = new GameObject("bosshazard");
            go.transform.position = pos;
            var h = go.AddComponent<BossHazard>();
            h.owner = owner; h.shape = shape; h.pos = pos;
            h.src = owner != null ? (Vector2)owner.transform.position : pos; h.seed = Random.Range(0, 100);
            h.forming = tele <= 0f;
            h.tele = h.forming ? FormTime : tele;
            h.active = active; h.dmg = dmg; h.col = col; h.tick = tick;
            All.Add(h);
            return h;
        }

        public static void ClearOwner(Enemy owner)
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var h = All[i];
                if (h == null) { All.RemoveAt(i); continue; }
                if (h.owner == owner) h.Kill();
            }
        }

        public static void ClearAll()
        {
            for (int i = All.Count - 1; i >= 0; i--) if (All[i] != null) All[i].Kill();
            All.Clear();
            PlayerHits = 0;
            VfxFx.ClearAll();   // scorch marks stay until the wave ends
            if (Game.I != null && Game.I.player != null) Game.I.player.ClearStatus();
        }

        /// <summary>Applies a spell look (BossMove.look) and what it does: pools and burning ground apply Poison / Burn while the player stands in them, rings and cones on a hit.</summary>
        public void SetLook(string l)
        {
            look = l;
            switch (l)
            {
                case "poison_pool": status = 0; whileIn = true; fadeOut = 0.8f; break;
                case "poison_sea": status = 0; whileIn = true; break;
                case "fire_ground": status = 1; whileIn = true; fadeOut = 0.8f; scorch = true; break;
                case "fire_ring": case "fire_cone": status = 1; break;
                case "meteor": scorch = true; break;
                case "ink_pool": case "blood_pool": pool = l; fadeOut = 0.8f; break;
                case "violet_beam": tuft = "violet_tuft"; break;
                case "blood_ring": tuft = "blood_tuft"; break;
                case "blood_rain": seal = "blood_rain"; strike = "blood_splash"; stain = "blood_pool"; stainCol = new Color(0.62f, 0.34f, 0.34f, 1f); break;
                case "dark_slam": stain = "ink_pool"; stainCol = new Color(0.8f, 0.75f, 0.9f, 1f); break;
                case "swarm": holes = new List<Vector2>(); break;
                case "violet_seal": seal = "rune_violet"; break;
                case "blood_seal": seal = "rune_blood"; break;
                case "gold_seal": seal = "rune_gold"; strike = "gold_smite"; break;
                case "thunder": strike = "thunder"; scorch = true; break;
            }
        }

        void Kill()
        {
            if (gone) return;
            gone = true;
            if (fx != null) fx.Stop();
            if (sealFx != null) sealFx.Stop();
            All.Remove(this);
            Destroy(gameObject);
        }

        void OnDestroy() { All.Remove(this); }

        /// <summary>The spell effect that takes over the look when the hazard goes live (pool, burning ground), plus the landing splash.</summary>
        void OnLive()
        {
            float left = Mathf.Max(0.1f, tele + active - age);
            if (pool != null) fx = VfxFx.Ground(pool, pos, radius, left, -190, 0.25f, fadeOut);
            if (strike != null) VfxFx.Play(strike, pos, radius / (strike == "blood_splash" ? 60f : 36f), 0f, 135);
            switch (look)
            {
                case "poison_pool":
                    fx = VfxFx.Ground("poison_pool", pos, radius, left, -190, 0.25f, fadeOut);
                    VfxFx.Play("acid_splash", pos, radius / 60f, 0.45f, 135);   // its last frames are a flat side-view puddle bar
                    break;
                case "fire_ground":
                    fx = VfxFx.Ground("burning_ground", pos, radius, left, -150, 0.15f, fadeOut);
                    if (fx != null) fx.scaleOf = a => (1f + 0.15f * Mathf.Exp(-a * 7f)) * Mathf.Lerp(0.72f, 1f, Mathf.Clamp01((left - a) / 1.2f));   // flares, then burns down
                    break;
            }
        }

        /// <summary>fire/meteor falls onto the circle; its impact frame lands on the moment the hazard goes live.</summary>
        void Meteor()
        {
            const float Impact = 0.62f;   // seconds into the clip (fire/meteor HIT)
            float rest = Mathf.Max(0.05f, tele - age);
            VfxFx.Play("meteor", pos, radius / 42f, 0f, 135, null, default, rest >= Impact ? 1f : Impact / rest, Mathf.Max(0f, rest - Impact));
        }

        // ------------------------------------------------------------------ geometry
        static float AngDiff(float x, float y) => Mathf.Abs(Mathf.DeltaAngle(x, y));

        /// <summary>True when a disc of radius r at p overlaps the hazard shape (ignores the lifecycle phase).</summary>
        public bool Contains(Vector2 p, float r)
        {
            if (holes != null) foreach (var c in holes) if ((p - c).sqrMagnitude <= holeR * holeR) return false;
            switch (shape)
            {
                case HShape.Circle:
                    return (p - pos).sqrMagnitude <= (radius + r) * (radius + r);
                case HShape.Bar:
                {
                    float ar = angleDeg * Mathf.Deg2Rad;
                    Vector2 d = new Vector2(Mathf.Cos(ar), Mathf.Sin(ar)), q = p - pos;
                    float t = Vector2.Dot(q, d), s = Mathf.Abs(q.x * d.y - q.y * d.x);
                    return t >= -r && t <= len + r && s <= width * 0.5f + r;
                }
                case HShape.Segment:
                {
                    Vector2 ab = b - a; float l2 = ab.sqrMagnitude;
                    float t = l2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2) : 0f;
                    return (p - (a + ab * t)).sqrMagnitude <= (width * 0.5f + r) * (width * 0.5f + r);
                }
                case HShape.Cone:
                {
                    Vector2 q = p - pos; float dist = q.magnitude;
                    if (dist > radius + r) return false;
                    if (dist < 0.001f) return true;
                    float margin = Mathf.Asin(Mathf.Min(1f, r / dist)) * Mathf.Rad2Deg;
                    return AngDiff(Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg, angleDeg) <= arcDeg * 0.5f + margin;
                }
                case HShape.Ring:
                case HShape.Annulus:
                {
                    Vector2 q = p - pos; float dist = q.magnitude;
                    if (dist > radius + r || dist < radius2 - r) return false;
                    if (gapDirsDeg != null && gapDirsDeg.Count > 0 && dist > 0.001f)
                    {
                        float ang = Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg;
                        foreach (var g in gapDirsDeg) if (AngDiff(ang, g + angleDeg) <= gapWidthDeg * 0.5f) return false;
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>1 when p is inside a live hazard, or one that goes live within lookahead seconds; else 0.</summary>
        public static float DangerAt(Vector2 p, float lookahead)
        {
            var pl = Game.I != null ? Game.I.player : null;
            float pr = pl != null ? pl.radius : 0.36f;
            foreach (var h in All)
            {
                if (h == null || h.gone) continue;
                if (h.tick <= 0f && h.hitDone) continue;
                bool soon = h.age < h.tele && h.tele - h.age <= lookahead;
                if ((h.Live || soon) && h.Contains(p, pr)) return 1f;
            }
            return 0f;
        }

        // ------------------------------------------------------------------ lifecycle
        void Update()
        {
            var g = Game.I;
            if (!gone && anchored && (anchorE == null || anchorE.dead)) { Kill(); return; }
            if (gone || g == null || !g.Playing) return;
            float dt = Game.Dt;
            if (owner != null && !owner.dead) dt *= owner.TimeScale;
            age += dt;
            if (vel != Vector2.zero) { pos += vel * dt; transform.position = pos; }
            if (spinDeg != 0f) angleDeg += spinDeg * dt;
            if (age >= tele + active)
            {
                if (scorch && radius > 0f && owner != null && !owner.dead) VfxFx.Scorch(pos, radius * 0.9f);
                Kill(); return;
            }
            if (look == "meteor" && !meteorDone) { meteorDone = true; Meteor(); }
            if (seal != null && sealFx == null) sealFx = VfxFx.Ground(seal, pos, radius, tele + active, -185, 0.15f, 0.25f);
            if (stain != null && !stained)
            {   // the liquid seeps up through the floor, reaches its full size as the hit lands, then lingers as a harmless smear
                stained = true;
                float T = Mathf.Max(0.1f, tele);
                int spots = radius > 3f ? 7 : 1;   // one huge stain would blow its pixels up: a wide patch is several small ones
                for (int i = 0; i < spots; i++)
                {
                    Vector2 at = spots > 1 ? pos + Random.insideUnitCircle * radius * 0.72f : pos;
                    var st = VfxFx.Play(stain, at, (spots > 1 ? radius * 0.42f : radius) / 36f, T + 0.7f, -190, null, default, 1f, 0f, stainCol, false, 0f, 0.6f);
                    if (st != null) { st.scaleOf = a => Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(a / T)); st.alphaOf = a => Mathf.Lerp(0.3f, 0.8f, Mathf.Clamp01(a / T)); }
                }
            }
            if (age >= tele && !fxDone) { fxDone = true; OnLive(); }

            if (age >= tele)
            {
                if (!hinted && owner != null && active > 0f && g.ui != null)
                {   // spec 3.4: first live boss hazard explains that bullet-clearing skills do not remove it
                    hinted = true;
                    g.ui.Hint("boss_hazard", "Vùng sát thương của boss không phải đạn: chiêu xoá đạn không xoá được, hãy né ra ngoài.");
                }
                hitCd -= dt;
                var p = g.player;
                if (status >= 0 && whileIn && p != null && p.gameObject.activeInHierarchy && Contains(p.Pos, p.radius)) p.ApplyStatus(status, moveId);
                if (p != null && p.gameObject.activeInHierarchy && (tick > 0f || !hitDone) && hitCd <= 0f && Contains(p.Pos, p.radius))
                {
                    if (BossBrain.HurtPlayer(owner, moveId, dmg, pos))
                    {   // only a real hit consumes the hazard: god/iframe/dash/dodge leave it armed
                        hitDone = true;
                        hitCd = Mathf.Max(tick, MinTick);
                        PlayerHits++;
                        if (status >= 0 && !whileIn) p.ApplyStatus(status, moveId);
                        if (slow > 0f) { float m = 1f - Mathf.Clamp01(slow); p.slowMul = p.slowT > 0f ? Mathf.Min(p.slowMul, m) : m; p.slowT = slowDur; }
                    }
                }
            }
            Draw();
        }

        // ------------------------------------------------------------------ drawing
        // Organic look: textures from Res (Blob / Rag / RagLen) have an irregular, slightly wobbling edge band and are denser at the rim;
        // the wobble is a slow boil between 4 close variants, staggered per piece. Shapes keep the Contains() geometry: the noise only moves
        // a thin band at the edge. Looks (BossMove.look) add spell effects from Res.Vfx on top.
        void Put(Sprite sp, Vector2 center, float sx, float sy, float rotDeg, Color c, int order, bool add, bool outsideMask = false)
        {
            if (used >= pieces.Count)
            {
                var go = new GameObject("p");
                go.transform.SetParent(transform, false);
                var n = go.AddComponent<SpriteRenderer>();
                if (defMat == null) defMat = n.sharedMaterial;
                pieces.Add(n);
            }
            var sr = pieces[used++];
            sr.gameObject.SetActive(true);
            sr.sprite = sp;
            var t = sr.transform;
            var sz = sp.bounds.size;
            t.position = center; t.rotation = Quaternion.Euler(0, 0, rotDeg); t.localScale = new Vector3(sx / sz.x, sy / sz.y, 1f);
            sr.color = c; sr.sortingOrder = order;
            sr.sharedMaterial = add ? Res.AddMat : defMat;
            sr.maskInteraction = outsideMask ? SpriteMaskInteraction.VisibleOutsideMask : SpriteMaskInteraction.None;
        }

        static Vector2 Dir(float deg) { float r = deg * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(r), Mathf.Sin(r)); }

        int Var(int i) => Mathf.FloorToInt(age * 7f) + i * 3 + seed;
        float Wob(int i, float rate = 2f) => Mathf.Sin(age * rate + i * 1.7f + seed);

        /// <summary>One frame of a Vfx strip as a piece: pivot at at, upp world units per effect pixel.</summary>
        void PutFrame(VfxClip c, int frame, Vector2 at, float upp, Color col, int order)
        {
            var sp = c.frames[frame % c.frames.Length];
            Put(sp, at, sp.bounds.size.x * upp * VfxClip.Ppu, sp.bounds.size.y * upp * VfxClip.Ppu, 0f, col, order, false);
        }

        int ClipFrame(VfxClip c, int i) => Mathf.FloorToInt(age * 1000f / Mathf.Max(1f, c.ms)) + i * 5;

        void Draw()
        {
            used = 0;
            bool live = age >= tele;
            if (look == "silk" && !live) HideSilk();
            float prog = live ? 1f : Mathf.Clamp01(age / tele);
            int order = live && fx == null ? 130 : -200;   // a spell effect stays on the ground: the player must stay visible inside it
            float fillA = live ? 0.8f : (forming ? 0.08f : 0.12f) + 0.2f * prog;
            Color fill = new Color(col.r, col.g, col.b, fillA);
            Color core = new Color(col.r, col.g, col.b, 0.35f);
            Color line = live ? new Color(1f, 1f, 1f, 0.9f)
                : new Color(Mathf.Lerp(col.r, 1f, 0.4f), Mathf.Lerp(col.g, 1f, 0.4f), Mathf.Lerp(col.b, 1f, 0.4f), forming ? 0.35f : 0.85f);
            int lo = order + (live ? 5 : 2);
            bool vfxLive = live && fx != null;   // a spell effect carries the live look: keep only a faint organic base under it
            if (vfxLive) { fill.a *= 0.3f; line.a *= 0.35f; }
            float fade = live && fadeOut > 0f ? Mathf.Clamp01((tele + active - age) / fadeOut) : 1f;
            if (live && look != null && look.StartsWith("fire")) fill.a *= 0.55f;
            if (live && look == "silk") { fill.a *= 0.2f; line.a *= 0.15f; }   // the wavy strands carry the look, a solid core reads as a laser
            if (live && (look == "meteor" || strike != null)) { fill.a *= 0.2f; line.a *= 0.3f; }   // the impact frames carry the hit; a solid disc would hide them
            fill.a *= fade; line.a *= fade;
            float step = Platform.Touch ? 1.4f : 1f;

            switch (shape)
            {
                case HShape.Circle:
                {
                    float d = radius * 2f * Res.BlobScale * (1f + 0.012f * Wob(0, 2.3f));
                    if (stain != null) { if (!live) { line.a *= 0.4f; Put(Res.BlobRim(Var(2)), pos, d, d, 0, line, lo, false); } break; }   // the stain and the splash show the patch, the faint rim its true edge
                    Put(Res.Blob(Var(0)), pos, d, d, 0, fill, order, live && !vfxLive);
                    if (!live) { float dc = d * prog; Put(Res.Blob(Var(1)), pos, dc, dc, 0, core, order + 1, false); }
                    if (!vfxLive) Put(Res.BlobRim(Var(2)), pos, d, d, 0, line, lo, live);
                    if (look == "poison_pool" && age < tele && !forming) DrawGlob(prog);
                    break;
                }
                case HShape.Bar:
                case HShape.Segment:
                {
                    Vector2 s = shape == HShape.Bar ? pos : a;
                    Vector2 e = shape == HShape.Bar ? pos + Dir(angleDeg) * len : b;
                    Vector2 dd = e - s; float l = dd.magnitude;
                    float rot = Mathf.Atan2(dd.y, dd.x) * Mathf.Rad2Deg;
                    Vector2 mid = (s + e) * 0.5f;
                    if (holes != null) { DrawField(live, fill, core, prog, order); break; }
                    bool thin = width <= 1.2f;
                    float w = width * (1f + 0.04f * Wob(1, 3f));
                    Put(Res.Rag(Var(0)), mid, l, w, rot, fill, order, live);
                    if (!live) Put(Res.Rag(Var(1)), mid, l, width * prog, rot, core, order + 1, false);
                    else Put(Res.Rag(Var(2)), mid, l, width * 0.35f, rot, line, lo, true);
                    if (thin)
                    {   // tapered, rounded ends
                        float cw = width * 1.5f * Res.BlobScale;
                        Put(Res.Blob(Var(3)), s, cw, width * Res.BlobScale, rot, fill, order, live);
                        Put(Res.Blob(Var(4)), e, cw, width * Res.BlobScale, rot, fill, order, live);
                    }
                    if (look == "silk" && live) DrawSilk(s, e, fade);
                    if ((look == "violet_beam" || look == "fire_beam") && live) DrawBarTufts(s, e, fade);
                    break;
                }
                case HShape.Cone:
                {
                    int n = Mathf.Max(2, Mathf.CeilToInt(arcDeg / 8f));
                    float stp = arcDeg / n;
                    float wd = 2f * radius * Mathf.Sin(stp * 0.5f * Mathf.Deg2Rad) * 1.15f;
                    Color cf = fill; if (look == "sonic") cf.a *= 0.4f;   // the wave fronts carry this look
                    for (int i = 0; i < n; i++)
                    {
                        float ang = angleDeg - arcDeg * 0.5f + stp * (i + 0.5f);
                        float rl = radius * (0.96f + 0.05f * Wob(i, 2.6f));
                        Put(Res.RagLen(Var(i)), pos + Dir(ang) * rl * 0.5f, rl, wd, ang, cf, order, live);
                    }
                    if (look == "fire_cone" && live) DrawTufts(Dir(angleDeg), fade);
                    if (look == "sonic") DrawSonic(order, live, fade);
                    break;
                }
                case HShape.Ring:
                case HShape.Annulus:
                {
                    float rm = (radius + radius2) * 0.5f, th = radius - radius2;
                    int n = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * rm / (0.8f * step)), 20, 64);
                    float stp = 360f / n, arc = 2f * Mathf.PI * rm / n * 1.12f;
                    for (int i = 0; i < n; i++)
                    {
                        float ang = stp * (i + 0.5f);
                        if (InGap(ang)) continue;
                        float tk = th * (1f + 0.1f * Wob(i, 1.9f)), rr = rm + th * 0.04f * Wob(i + 5, 2.4f);
                        Put(Res.Rag(Var(i)), pos + Dir(ang) * rr, arc, tk, ang + 90f, fill, order, live);
                    }
                    if ((look == "fire_ring" || look == "blood_ring") && live) DrawRingTufts(fade);
                    break;
                }
            }
            for (int i = used; i < pieces.Count; i++) pieces[i].gameObject.SetActive(false);
        }

        bool InGap(float ang)
        {
            if (gapDirsDeg != null) foreach (var gd in gapDirsDeg) if (AngDiff(ang, gd + angleDeg) <= gapWidthDeg * 0.5f) return true;
            return false;
        }

        // ---- spider silk: thin wavy strands with sag and shimmer
        static Material lineMat;
        readonly List<LineRenderer> strands = new List<LineRenderer>();

        void DrawSilk(Vector2 s, Vector2 e, float fade)
        {
            const int N = 14, K = 3;
            Vector2 dd = e - s; float l = dd.magnitude; if (l < 0.01f) return;
            Vector2 dir = dd / l, perp = new Vector2(-dir.y, dir.x);
            if (lineMat == null) lineMat = new Material(Shader.Find("Sprites/Default"));
            for (int k = 0; k < K; k++)
            {
                if (k >= strands.Count)
                {
                    var go = new GameObject("silk"); go.transform.SetParent(transform, false);
                    var lr = go.AddComponent<LineRenderer>();
                    lr.sharedMaterial = lineMat; lr.numCapVertices = 2; lr.positionCount = N; lr.useWorldSpace = true; lr.textureMode = LineTextureMode.Stretch;
                    strands.Add(lr);
                }
                var L = strands[k];
                L.gameObject.SetActive(true);
                float off = (k - 1) * width * 0.28f, ph = seed * 0.9f + k * 2.1f;
                for (int i = 0; i < N; i++)
                {
                    float u = i / (float)(N - 1), env = Mathf.Sin(u * Mathf.PI);
                    float wav = 0.06f * Mathf.Sin(u * 11f + ph + age * 1.6f) + 0.035f * Mathf.Sin(u * 27f + ph * 1.7f - age * 2.3f);
                    float sag = -0.24f * env * (1f + 0.15f * Mathf.Sin(age * 1.3f + ph));
                    var p2 = s + dir * (u * l) + perp * (off + (wav + sag) * Mathf.Min(1f, l * 0.2f + 0.3f));
                    L.SetPosition(i, new Vector3(p2.x, p2.y, 0f));
                }
                L.widthMultiplier = k == 1 ? 0.075f : 0.045f;
                float shimmer = 0.65f + 0.35f * Mathf.Sin(age * 7f + k * 1.9f);
                var cc = new Color(0.9f, 1f, 0.85f, 0.9f * shimmer * fade);
                L.startColor = cc; L.endColor = new Color(cc.r, cc.g, cc.b, cc.a * 0.7f);
                L.sortingOrder = 136;
            }
        }

        void HideSilk() { foreach (var s in strands) if (s != null) s.gameObject.SetActive(false); }

        // ---- fire: tufts along ring edges and across cones
        void DrawBarTufts(Vector2 s, Vector2 e, float fade)
        {
            var c = Res.Vfx(tuft); if (c == null) return;
            Vector2 d = e - s; float l = d.magnitude; if (l < 0.01f) return;
            Vector2 perp = new Vector2(-d.y, d.x) / l;
            int n = Mathf.Clamp(Mathf.RoundToInt(l), 3, 14);
            var cl = new Color(1f, 1f, 1f, fade);
            for (int i = 0; i < n; i++)
            {
                Vector2 at = Vector2.Lerp(s, e, (i + 0.5f) / n) + perp * width * (i % 2 == 0 ? 0.42f : -0.42f);
                PutFrame(c, ClipFrame(c, i), at, 0.038f, cl, 131 + Mathf.RoundToInt((pos.y - at.y) * 4f));
            }
        }

        // ---- sound: curved wave fronts running out through the cone, in the telegraph too (the live hit is only a blink)
        void DrawSonic(int order, bool live, float fade)
        {
            Color lt = new Color(Mathf.Lerp(col.r, 1f, 0.55f), Mathf.Lerp(col.g, 1f, 0.55f), Mathf.Lerp(col.b, 1f, 0.55f), 1f);
            for (int k = 0; k < 3; k++)
            {
                float f = Mathf.Repeat(age * 1.6f + k / 3f, 1f), rr = radius * (0.2f + 0.8f * f);
                int n = Mathf.Clamp(Mathf.RoundToInt(rr * arcDeg * Mathf.Deg2Rad / 0.8f), 3, 8);
                float stp = arcDeg / n, arc = rr * stp * Mathf.Deg2Rad * 1.2f;
                lt.a = Mathf.Sin(f * Mathf.PI) * 0.75f * fade;
                for (int i = 0; i < n; i++)
                {
                    float ang = angleDeg - arcDeg * 0.5f + stp * (i + 0.5f);
                    Put(Res.Rag(Var(i + k)), pos + Dir(ang) * rr, arc, 0.14f + 0.14f * f, ang + 90f, lt, order + 3, live);
                }
            }
        }

        void DrawRingTufts(float fade)
        {
            var c = Res.Vfx(tuft); if (c == null) return;
            float r = shape == HShape.Annulus ? Mathf.Max(radius2, 0.5f) + 0.25f : (radius + radius2) * 0.5f;
            int max = Platform.Touch ? 26 : 36;   // ring pieces (64 max) + tufts must stay within the 100-sprite budget
            int n = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * r / 0.95f), 10, max);
            var cl = new Color(1f, 1f, 1f, fade);
            for (int i = 0; i < n; i++)
            {
                float ang = 360f / n * (i + 0.5f) + 8f * Wob(i, 1.1f);
                if (InGap(ang)) continue;
                Vector2 at = pos + Dir(ang) * r;
                PutFrame(c, ClipFrame(c, i), at, 0.034f, cl, 131 + Mathf.RoundToInt((pos.y - at.y) * 4f));
            }
        }

        void DrawTufts(Vector2 dir, float fade)
        {
            var c = Res.Vfx("flame_tuft"); if (c == null) return;
            float aim = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            var cl = new Color(1f, 1f, 1f, fade);
            int rows = Platform.Touch ? 2 : 3, k = 0;
            for (int r = 0; r < rows; r++)
            {
                float f = (r + 1f) / rows, rr = radius * (0.3f + 0.65f * f);
                int cols = Mathf.Clamp(Mathf.RoundToInt(rr * arcDeg * Mathf.Deg2Rad / 1.1f), 2, 8);
                for (int i = 0; i < cols; i++)
                {
                    float ang = aim + arcDeg * 0.46f * ((i + 0.5f) / cols * 2f - 1f) + 4f * Wob(k, 1.3f);
                    Vector2 at = pos + Dir(ang) * rr;
                    PutFrame(c, ClipFrame(c, k++), at, 0.022f + 0.02f * f, cl, 131 + Mathf.RoundToInt((pos.y - at.y) * 4f));
                }
            }
        }

        // ---- poison: a glob arcing in from the boss during the telegraph
        void DrawGlob(float prog)
        {
            float k = Mathf.Clamp01(prog), arc = Mathf.Sin(k * Mathf.PI) * Mathf.Min(3.5f, 1f + Vector2.Distance(src, pos) * 0.3f);
            Vector2 g = Vector2.Lerp(src, pos, k) + Vector2.up * arc;
            float sz = 0.45f * (1f + 0.15f * Mathf.Sin(age * 18f + seed));
            Put(Res.Blob(Var(5)), Vector2.Lerp(src, pos, k), sz * 1.2f, sz * 0.7f, 0, new Color(0f, 0f, 0f, 0.3f), -150, false);
            Put(Res.Blob(Var(6)), g, sz, sz, 0, new Color(0.45f, 1f, 0.2f, 0.95f), 132, false);
            Put(Res.BlobRim(Var(7)), g, sz * 1.15f, sz * 1.15f, 0, new Color(0.85f, 1f, 0.6f, 0.9f), 133, false);
        }

        // ---- poison sea: the whole floor as drifting blobs; the safe holes are cut out with sprite masks and rimmed
        readonly List<SpriteMask> masks = new List<SpriteMask>();

        void DrawField(bool live, Color fill, Color core, float prog, int order)
        {
            float sp = Platform.Touch ? 4.2f : 3.2f;
            float x0 = pos.x, y0 = pos.y - width * 0.5f;
            int cols = Mathf.CeilToInt(len / sp) + 1, rows = Mathf.CeilToInt(width / sp) + 1;
            float dx = len / (cols - 1), dy = width / (rows - 1), d = Mathf.Max(dx, dy) * 1.55f * Res.BlobScale;
            for (int i = 0; i < cols; i++)
                for (int j = 0; j < rows; j++)
                {
                    int id = i * 31 + j * 17;
                    // flowing: each blob drifts on its own slow loop, so the surface slides and swells
                    Vector2 at = new Vector2(x0 + i * dx + 0.45f * Mathf.Sin(age * 0.8f + id), y0 + j * dy + 0.45f * Mathf.Cos(age * 0.7f + id * 1.3f));
                    float sc = d * (1f + 0.08f * Mathf.Sin(age * 1.1f + id * 0.7f));
                    if (look == "swarm") Put(Res.Glow, at, sc * 1.5f, sc * 1.5f, 0f, new Color(0.16f, 0.03f, 0.1f, 0.75f * prog), order, false);   // the shadow of the swarm, a soft dark cloud
                    else Put(Res.Blob(Var(id)), at, sc, sc, id * 13f + age * 4f, fill, order, false, true);
                }
            while (masks.Count < holes.Count)
            {
                var go = new GameObject("hole"); go.transform.SetParent(transform, false);
                var m = go.AddComponent<SpriteMask>(); m.sprite = Res.Blob(0); m.alphaCutoff = 0.2f;
                masks.Add(m);
            }
            for (int i = 0; i < masks.Count; i++)
            {
                bool on = i < holes.Count;
                masks[i].gameObject.SetActive(on);
                if (!on) continue;
                float hd = holeR * 2f * Res.BlobScale * (1f + 0.02f * Wob(i, 2.2f));
                masks[i].transform.position = holes[i]; masks[i].transform.localScale = new Vector3(hd, hd, 1f);
                masks[i].transform.rotation = Quaternion.Euler(0, 0, age * 12f * (i % 2 == 0 ? 1 : -1));
                var rim = new Color(0.85f, 1f, 0.7f, live ? 0.55f : 0.3f);
                Put(Res.BlobRim(Var(i + 8)), holes[i], hd, hd, 0, rim, order + 4, live);
            }
        }
    }
}
