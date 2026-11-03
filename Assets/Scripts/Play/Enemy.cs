using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    public class Enemy : MonoBehaviour
    {
        public static readonly List<Enemy> All = new List<Enemy>();
        static readonly Stack<List<Enemy>> pool = new Stack<List<Enemy>>();
        public static List<Enemy> Snapshot() { var l = pool.Count > 0 ? pool.Pop() : new List<Enemy>(256); l.Clear(); l.AddRange(All); return l; }
        public static void Release(List<Enemy> l) { l.Clear(); pool.Push(l); }

        public EnemyDef def;
        public float hp, maxHp, dmg, speed, radius;
        public bool boss, elite, dead;
        public int bossIndex, wave;
        public Vector2 Pos => transform.position;

        Transform body;
        SpriteRenderer sr, shadow, auraSr, tele, outline;
        MaterialPropertyBlock outMpb;
        MaterialPropertyBlock mpb;
        Vector2 knock, chargeDir;
        float flash, animT, stateT, shootT, wobble, spawnT, baseScale;
        int state;
        bool awake;
        Vector2 blinkTo;
        SpriteRenderer boom;
        float burnDps, burnT, poisonDps, poisonT, dotTick, frozenT;
        bool poisonSkill;   // poisoned by Độc Đạo Ấn: spreads on death even without the Đạo Độc synergy
        public bool Frozen => frozenT > 0;
        /// <summary>Time factor while frozen (matches the dt Update uses); BossHazard timers follow it.</summary>
        public float TimeScale => frozenT > 0 ? (boss ? 0.5f : 0.15f) : 1f;
        public bool Burning => burnT > 0;
        public int skillHits;
        public bool HasStatus => burnT > 0 || poisonT > 0 || frozenT > 0;
        public void Freeze(float t) { if (brain != null && brain.Staggered) return; frozenT = Mathf.Max(frozenT, boss ? t * 0.4f : t); }
        /// <summary>Frozen by a Thần Thông (Phù Tu): takes <paramref name="bonus"/>× damage and fires Bạo Phù when the ice ends.</summary>
        public bool SkillFrozen { get; private set; }
        float skillBonus = 1.3f;
        public float FrozenBonus => frozenT > 0 && SkillFrozen ? skillBonus : 1.3f;
        public void SkillFreeze(float t, float bonus) { if (brain != null && brain.Staggered) return; Freeze(t); SkillFrozen = true; skillBonus = bonus; }
        /// <summary>Boss AI (null for normal enemies and bosses without a kit).</summary>
        public BossBrain brain;
        /// <summary>Boss that summoned this minion (counts against kit.summonCap); null for everything else.</summary>
        public BossBrain summoner;
        /// <summary>Seconds left before a summoned minion vanishes (0 = no limit).</summary>
        public float lifeT;
        /// <summary>Vanished from sight (Blink): no contact damage, renderers off.</summary>
        public bool Hidden { get; private set; }
        /// <summary>Bright outline on (the real boss while its clones live).</summary>
        public bool Highlighted { get; private set; }
        public void SetHidden(bool hide)
        {
            if (hide == Hidden) return;   // un-hiding a visible enemy must not re-enable renderers that are off on purpose
            Hidden = hide;
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = !hide;
        }
        Color tintCol;
        /// <summary>Current body colour (clones copy it).</summary>
        public Color Tint => tintCol;
        float scaleMul = 1f;
        static readonly int FlashId = Shader.PropertyToID("_Flash");

        public static Enemy Spawn(EnemyDef d, Vector2 pos, int wave, float hpMul, float dmgMul, bool asBoss = false, int bossIdx = 0)
        {
            var go = new GameObject(d.name);
            go.transform.position = pos;
            var e = go.AddComponent<Enemy>();
            e.Init(d, wave, hpMul, dmgMul, asBoss, bossIdx);
            All.Add(e);
            return e;
        }

        /// <summary>Trash HP multiplier by wave (before Kiếp), shared with the Thần Thông power formula.</summary>
        public static float WaveHpMul(int w)
        {
            float wm = w - 1;
            return (1f + 0.38f * wm + 0.085f * wm * wm) * (1f + Mathf.Max(0f, wm - 11f) * 0.06f);
        }

        void Init(EnemyDef d, int w, float hpMul, float dmgMul, bool asBoss, int bossIdx)
        {
            def = d; boss = asBoss; elite = d.elite; bossIndex = bossIdx; wave = w;
            float wm = w - 1;
            float late = Mathf.Max(0f, wm - 11f);   // the last two regions push back against snowballed builds
            maxHp = boss ? d.hp * hpMul : d.hp * WaveHpMul(w) * hpMul;
            hp = maxHp;
            dmg = boss ? d.dmg * dmgMul : d.dmg * (1f + 0.28f * wm + 0.012f * wm * wm) * dmgMul * (1f + late * 0.035f);
            speed = d.speed * (1f + Mathf.Min(0.25f, wm * 0.012f)) * (boss ? 1f : Random.Range(0.9f, 1.1f) * Game.I.dir.SpeedMul);
            radius = 0.34f * d.scale;
            baseScale = d.scale;
            tintCol = d.tint;
            mpb = new MaterialPropertyBlock();

            var sh = new GameObject("shadow");
            sh.transform.SetParent(transform, false);
            sh.transform.localPosition = new Vector3(0, -0.4f * d.scale, 0);
            sh.transform.localScale = new Vector3(0.75f * d.scale, 0.25f * d.scale, 1);
            shadow = sh.AddComponent<SpriteRenderer>();
            shadow.sprite = Res.Circle; shadow.color = new Color(0, 0, 0, 0.35f); shadow.sortingOrder = -120;

            if (elite || boss)
            {
                var au = new GameObject("aura");
                au.transform.SetParent(transform, false);
                au.transform.localScale = Vector3.one * d.scale * 2f;
                auraSr = au.AddComponent<SpriteRenderer>();
                auraSr.sprite = Res.Glow; auraSr.sharedMaterial = Res.AddMat; auraSr.sortingOrder = -110;
                auraSr.color = boss ? new Color(0.8f, 0.2f, 1f, 0.35f) : new Color(1f, 0.2f, 0.2f, 0.3f);
            }

            body = new GameObject("body").transform;
            body.SetParent(transform, false);
            sr = body.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = Res.Tile(d.beh == EBeh.Mimic ? Res.Px.MimicSleep : d.sprite);   // a mimic looks like a plain chest until it wakes
            sr.sharedMaterial = Res.FlashMat;
            sr.color = d.tint;
            body.localScale = Vector3.zero;
            spawnT = 0f;
            if (elite || boss)
            {   // solid-colour silhouette slightly larger than the body = glowing outline
                var og = new GameObject("outline");
                og.transform.SetParent(body, false);
                og.transform.localScale = Vector3.one * 1.14f;
                outline = og.AddComponent<SpriteRenderer>();
                outline.sprite = sr.sprite;
                outline.sharedMaterial = Res.FlashMat;
                outMpb = new MaterialPropertyBlock();
                outline.GetPropertyBlock(outMpb);
                outMpb.SetFloat(FlashId, 1f);
                outMpb.SetColor("_FlashColor", boss ? new Color(0.85f, 0.4f, 1f) : new Color(1f, 0.25f, 0.2f));
                outline.SetPropertyBlock(outMpb);
            }

            shootT = Random.Range(1.2f, 2.6f);
            stateT = boss ? 2f : Random.Range(1.5f, 3f);
            wobble = Random.Range(0f, 10f);
        }

        void Update()
        {
            var g = Game.I;
            if (!g.Playing || dead) return;
            float rawDt = Game.Dt;
            if (lifeT > 0f) { lifeT -= rawDt; if (lifeT <= 0f) { Vanish(); return; } }
            if (frozenT > 0)
            {
                frozenT -= rawDt;
                if (frozenT <= 0 && SkillFrozen) { SkillFrozen = false; Skills.OnSkillFreezeEnd(this); if (dead) return; }
            }
            float dt = frozenT > 0 ? rawDt * (boss ? 0.5f : 0.15f) : rawDt;
            var p = g.player;
            Vector2 pp = p.Pos, pos = Pos, to = pp - pos;
            float dist = to.magnitude;
            Vector2 dir = dist > 0.001f ? to / dist : Vector2.zero;
            Vector2 move = Vector2.zero;

            switch (def.beh)
            {
                case EBeh.Chase:
                case EBeh.Splitter:
                    move = dir * speed; break;
                case EBeh.Swarm:
                    wobble += dt * 5f;
                    move = (dir + new Vector2(-dir.y, dir.x) * Mathf.Sin(wobble) * 0.9f).normalized * speed;
                    break;
                case EBeh.Charger: move = Charger(dt, dir, dist); break;
                case EBeh.Shooter: move = Shooter(dt, dir, dist); break;
                case EBeh.Boss: move = brain != null ? brain.Tick(dt, dir, dist) : dir * speed; break;
                case EBeh.Bomber: move = Bomber(dt, dir, dist); if (dead) return; break;
                case EBeh.Mimic: move = Mimic(dt, dir, dist); break;
                case EBeh.Blinker: move = Blinker(dt, dir, dist); break;
            }

            // soft separation so crowds spread out instead of stacking
            Vector2 sep = Vector2.zero;
            for (int i = 0; i < All.Count; i++)
            {
                var o = All[i];
                if (o == this) continue;
                Vector2 d = pos - (Vector2)o.transform.position;
                float r = radius + o.radius;
                float sq = d.sqrMagnitude;
                if (sq < r * r && sq > 0.0001f) { float dd = Mathf.Sqrt(sq); sep += d / dd * (r - dd); }
            }
            float sepK = boss ? 0.05f : def.beh == EBeh.Static ? 0f : 1f;
            pos += move * dt + knock * rawDt + sep * Mathf.Min(1f, rawDt * 12f) * sepK;
            knock *= Mathf.Max(0f, 1f - rawDt * 9f);
            var clamped = Arena.Clamp(pos, radius * 0.5f);
            pos = clamped;
            transform.position = pos;

            if (frozenT <= 0 && !Hidden && def.beh != EBeh.Static && Vector2.Distance(pos, pp) < radius + p.radius)
            {
                if (brain != null) { if (!brain.Dashing) BossBrain.HurtPlayer(this, "contact", dmg, pos); }
                else p.Hurt(dmg, pos);
            }
            sr.color = frozenT > 0 ? Color.Lerp(tintCol, new Color(0.5f, 0.85f, 1f), 0.65f) : tintCol;

            // damage over time
            if (burnT > 0 || poisonT > 0)
            {
                burnT -= rawDt; poisonT -= rawDt; dotTick -= rawDt;
                if (dotTick <= 0)
                {
                    dotTick = 0.5f;
                    float bd = burnT > 0 ? burnDps * 0.5f : 0, pd = poisonT > 0 ? poisonDps * 0.5f : 0;
                    if (bd > 0) { Damage(bd, false, Vector2.zero, 0, null, new Color(1f, 0.6f, 0.2f)); Fx.Particle(pos, new Vector2(Random.Range(-0.5f, 0.5f), 1.5f), new Color(1f, 0.6f, 0.2f), new Color(1f, 0.2f, 0f, 0f), 0.16f, 0.05f, 0.5f, null, true); }
                    if (!dead && pd > 0) { Damage(pd, false, Vector2.zero, 0, null, new Color(0.5f, 1f, 0.3f)); Fx.Particle(pos, new Vector2(Random.Range(-0.5f, 0.5f), 1.2f), new Color(0.5f, 1f, 0.3f), new Color(0.2f, 0.8f, 0f, 0f), 0.16f, 0.05f, 0.5f, null, true); }
                    if (dead) return;
                }
                if (burnT <= 0) burnDps = 0;
                if (poisonT <= 0) { poisonDps = 0; poisonSkill = false; }
            }

            // visuals
            spawnT += dt;
            float pop = spawnT < 0.25f ? Mathf.SmoothStep(0, 1, spawnT / 0.25f) : 1f;
            if (def.beh != EBeh.Mimic || awake) animT += dt * (6f + speed * 2f);
            float bob = Mathf.Abs(Mathf.Sin(animT)) * 0.07f * baseScale;
            float sq2 = 1f + Mathf.Sin(animT * 2f) * 0.05f;
            float windShake = (state == 1 || state == 3) ? Random.Range(-0.06f, 0.06f) : 0f;
            body.localPosition = new Vector3(windShake, bob, 0);
            body.localScale = new Vector3(baseScale * pop * (2f - sq2), baseScale * pop * sq2, 1);
            if (Mathf.Abs(to.x) > 0.05f) sr.flipX = to.x < 0;
            sr.sortingOrder = Mathf.RoundToInt(-pos.y * 5f);
            if (outline)
            {
                outline.sortingOrder = sr.sortingOrder - 1;
                outline.flipX = sr.flipX;
                bool hl = brain != null && BossHooks.HasClones(brain);
                if (hl != Highlighted)
                {   // the real boss among its clones: bright, thicker outline
                    Highlighted = hl;
                    outMpb.SetColor("_FlashColor", hl ? new Color(1f, 1f, 0.8f) : boss ? new Color(0.85f, 0.4f, 1f) : new Color(1f, 0.25f, 0.2f));
                    outline.SetPropertyBlock(outMpb);
                    outline.transform.localScale = Vector3.one * (hl ? 1.24f : 1.14f);
                }
                outline.color = hl ? Color.white : new Color(1, 1, 1, 0.55f + Mathf.Sin(Time.time * 6f) * 0.25f);
            }
            flash = Mathf.MoveTowards(flash, 0, dt * 8f);
            sr.GetPropertyBlock(mpb);
            mpb.SetFloat(FlashId, Mathf.Max(flash, (state == 1 || state == 3) ? 0.35f + Mathf.Sin(Time.time * 30f) * 0.25f : 0f));
            sr.SetPropertyBlock(mpb);
            if (auraSr) { var ac = auraSr.color; ac.a = (boss ? 0.3f : 0.25f) + Mathf.Sin(Time.time * 4f) * 0.08f + (brain != null && brain.Phase == 2 ? 0.15f : 0); auraSr.color = ac; }
            if (tele)
            {
                bool show = state == 1;
                tele.enabled = show;
                if (show) UpdateTele(pos);
            }
        }

        // ---------------------------------------------------------------- behaviours
        Vector2 Charger(float dt, Vector2 dir, float dist)
        {
            stateT -= dt;
            switch (state)
            {
                case 0:
                    if (stateT <= 0 && dist < 7f) { state = 1; stateT = 0.6f; chargeDir = dir; EnsureTele(); }
                    return dir * speed * 0.75f;
                case 1:
                    chargeDir = Vector2.Lerp(chargeDir, dir, dt * 2f).normalized;
                    if (stateT <= 0) { state = 2; stateT = 0.42f; Sfx.Play("swing", 0.4f, 0.1f, 0.8f); }
                    return Vector2.zero;
                default:
                    if (stateT <= 0) { state = 0; stateT = Random.Range(2f, 3.5f); }
                    if (Random.value < 0.5f) Fx.Particle(Pos, -chargeDir * 2f, new Color(0.8f, 0.7f, 0.6f, 0.5f), new Color(0.8f, 0.7f, 0.6f, 0), 0.2f, 0.05f, 0.3f);
                    return chargeDir * speed * 4.2f;
            }
        }

        /// <summary>Runs at the player, then swells up and explodes; killing it first makes it explode on its kin instead.</summary>
        Vector2 Bomber(float dt, Vector2 dir, float dist)
        {
            const float Fuse = 0.9f, R = 1.9f;
            if (state == 0)
            {
                if (dist < 1.9f)
                {
                    state = 1; stateT = Fuse;
                    var go = new GameObject("boom");
                    go.transform.SetParent(transform, false);
                    boom = go.AddComponent<SpriteRenderer>();
                    boom.sprite = Res.Circle; boom.sortingOrder = -201;
                    Sfx.Play("tick", 0.6f, 0f, 1.4f);
                }
                return dir * speed;
            }
            stateT -= dt;
            float k = 1f - Mathf.Clamp01(stateT / Fuse);
            boom.transform.localScale = Vector3.one * (R * 2f / transform.lossyScale.x) * Mathf.Lerp(0.3f, 1f, k);
            boom.color = new Color(1f, 0.3f, 0.1f, 0.12f + 0.2f * k + Mathf.Sin(Time.time * 30f) * 0.05f);
            baseScale = def.scale * (1f + 0.35f * k);
            if (stateT <= 0)
            {
                Explode(R, true);
                Vanish();
            }
            return dir * speed * 0.15f;
        }

        void Explode(float r, bool hurtsPlayer)
        {
            Vector2 pos = Pos;
            var g = Game.I;
            Fx.Ring(pos, new Color(1f, 0.55f, 0.2f), 0.3f, r * 1.2f, 0.35f);
            Fx.Glow(pos, new Color(1f, 0.5f, 0.15f, 0.9f), r * 3f, 0.3f);
            Fx.Burst(pos, new Color(1f, 0.6f, 0.2f), 18, 7f, 0.14f, 0.45f, true);
            Fx.Shake(0.25f, 0.2f);
            Sfx.Play("heavy", 0.7f, 0.1f, 0.9f);
            if (hurtsPlayer && Vector2.Distance(g.player.Pos, pos) < r + g.player.radius * 0.5f) g.player.Hurt(dmg * 1.15f, pos);
            var list = Snapshot();
            foreach (var e in list)
                if (e != this && !e.dead && !e.boss && Vector2.Distance(e.Pos, pos) < r + e.radius)
                    e.Damage(12f + wave * 6f, false, e.Pos - pos, 1f, null, new Color(1f, 0.6f, 0.2f));
            Release(list);
        }

        /// <summary>Sits still as a treasure chest until the player comes close or hits it, then lunges.</summary>
        Vector2 Mimic(float dt, Vector2 dir, float dist)
        {
            if (!awake)
            {
                if (dist < 2.6f || hp < maxHp)
                {
                    awake = true;
                    stateT = 0.35f;
                    sr.sprite = Res.Tile(def.sprite);
                    if (outline) outline.sprite = sr.sprite;
                    Fx.Text(Pos + Vector2.up * radius * 1.5f, "!", new Color(1f, 0.4f, 0.3f), 1.4f);
                    Sfx.Play("heavy", 0.5f, 0.1f, 1.3f);
                }
                return Vector2.zero;
            }
            return Charger(dt, dir, dist);
        }

        /// <summary>Marks a spot beside the player, vanishes, reappears there and rushes in.</summary>
        Vector2 Blinker(float dt, Vector2 dir, float dist)
        {
            stateT -= dt;
            switch (state)
            {
                case 0:
                    if (stateT <= 0 && dist < 11f)
                    {
                        var p = Game.I.player;
                        blinkTo = Arena.Clamp(p.Pos + p.Vel * 0.3f + Random.insideUnitCircle.normalized * 2.4f, 0.8f);
                        state = 1; stateT = 0.7f;
                        if (!boom)
                        {
                            var go = new GameObject("mark");
                            go.transform.SetParent(transform, false);
                            boom = go.AddComponent<SpriteRenderer>();
                            boom.sprite = Res.Ring; boom.sharedMaterial = Res.AddMat; boom.sortingOrder = -200;
                        }
                        boom.enabled = true;
                        Sfx.Play("zap", 0.3f, 0.1f, 1.5f);
                    }
                    return dir * speed * 0.7f;
                case 1:
                    boom.transform.position = blinkTo;
                    boom.transform.localScale = Vector3.one * (1.2f + stateT) / transform.lossyScale.x;
                    boom.color = new Color(0.7f, 0.4f, 1f, 0.8f - stateT * 0.6f);
                    if (stateT <= 0)
                    {
                        boom.enabled = false;
                        Fx.Burst(Pos, new Color(0.6f, 0.4f, 1f, 0.8f), 8, 3f);
                        Fx.Burst(blinkTo, new Color(0.6f, 0.4f, 1f, 0.8f), 10, 4f);
                        state = 2; stateT = 0.9f;
                        return (blinkTo - Pos) / Mathf.Max(dt, 0.0001f);
                    }
                    return Vector2.zero;
                default:
                    if (stateT <= 0) { state = 0; stateT = Random.Range(2.8f, 4.2f); }
                    return dir * speed * 1.9f;
            }
        }

        Vector2 Shooter(float dt, Vector2 dir, float dist)
        {
            shootT -= dt;
            if (shootT <= 0 && dist < 9.5f)
            {
                shootT = def.id == "ta_tu" ? 2.6f : 2.2f;
                int n = def.id == "ta_tu" ? 3 : 1;
                float spread = 16f;
                for (int i = 0; i < n; i++)
                {
                    float a = (i - (n - 1) / 2f) * spread;
                    Vector2 v = (Vector2)(Quaternion.Euler(0, 0, a) * dir) * 4.6f;
                    Projectile.FireHostile(Pos + dir * radius, v, dmg, def.id == "ta_tu" ? new Color(1f, 0.3f, 0.6f) : new Color(0.6f, 0.7f, 1f));
                }
                Fx.Glow(Pos + dir * radius, new Color(1f, 0.4f, 0.6f, 0.8f), 0.8f, 0.2f);
                Sfx.Play("shoot", 0.25f, 0.1f, 0.6f);
            }
            if (dist > 6.5f) return dir * speed;
            if (dist < 4f) return -dir * speed * 0.8f;
            wobble += dt;
            return new Vector2(-dir.y, dir.x) * speed * 0.5f * Mathf.Sign(Mathf.Sin(wobble * 0.7f));
        }

        void EnsureTele()
        {
            if (tele) return;
            var go = new GameObject("tele");
            go.transform.SetParent(transform, false);
            tele = go.AddComponent<SpriteRenderer>();
            tele.sprite = Res.White;
            tele.sortingOrder = -150;
        }

        void UpdateTele(Vector2 pos)
        {
            float len = boss ? 9f : 4.5f;
            float w = radius * 1.6f;
            float ang = Mathf.Atan2(chargeDir.y, chargeDir.x) * Mathf.Rad2Deg;
            tele.transform.position = pos + chargeDir * len * 0.5f;
            tele.transform.rotation = Quaternion.Euler(0, 0, ang);
            tele.transform.localScale = new Vector3(len / transform.lossyScale.x, w, 1);
            tele.color = new Color(1f, 0.15f, 0.15f, 0.18f + Mathf.Sin(Time.time * 20f) * 0.07f);
        }

        // ---------------------------------------------------------------- looks (boss cutscenes)
        /// <summary>Body colour from now on (Kiếp Biến / Ma Thân recolour).</summary>
        public float ScaleMul => scaleMul;
        public void FlashWhite(float v) { flash = v; }
        public void SetTint(Color c) { tintCol = c; if (sr) sr.color = c; }
        /// <summary>Aura colour (alpha keeps pulsing); adds an aura when the enemy has none.</summary>
        public void SetAura(Color c)
        {
            if (!auraSr)
            {
                var au = new GameObject("aura");
                au.transform.SetParent(transform, false);
                au.transform.localScale = Vector3.one * def.scale * 2f;
                auraSr = au.AddComponent<SpriteRenderer>();
                auraSr.sprite = Res.Glow; auraSr.sharedMaterial = Res.AddMat; auraSr.sortingOrder = -110;
            }
            auraSr.color = new Color(c.r, c.g, c.b, auraSr.color.a > 0f ? auraSr.color.a : 0.35f);
        }
        /// <summary>Size multiplier on the def scale (body, aura, hit radius).</summary>
        public void SetScaleMul(float m)
        {
            scaleMul = Mathf.Max(0.1f, m);
            baseScale = def.scale * scaleMul;
            radius = 0.34f * def.scale * scaleMul;
            if (auraSr) auraSr.transform.localScale = Vector3.one * def.scale * scaleMul * 2f;
        }

        // ---------------------------------------------------------------- damage
        public void ApplyBurn(float dps) { burnDps = Mathf.Max(burnDps, dps); burnT = 2f; if (dotTick <= 0) dotTick = 0.5f; }
        public void ApplyPoison(float dps, bool fromSkill = false) { poisonDps = Mathf.Max(poisonDps, dps) + dps * 0.25f; poisonDps = Mathf.Min(poisonDps, dps * 3f); poisonT = 3f; if (dotTick <= 0) dotTick = 0.5f; if (fromSkill) poisonSkill = true; }
        public bool Poisoned => poisonT > 0;

        public void Damage(float d, bool crit, Vector2 dir, float knockPow, WeaponInst src, Color? col = null)
        {
            if (dead) return;
            var run = Game.I.run;
            if (frozenT > 0) d *= FrozenBonus;
            float hp0 = hp;
            hp -= d;
            if (d < 99999f)   // revive shockwave kills are not real hits
            {
                run.dmgDealt += d;
                float eff = Mathf.Min(d, Mathf.Max(0f, hp0));
                run.dmgEff += eff;
                if (Skills.InHit) run.skillEff += eff;
                if (d > run.bestHit) run.bestHit = d;
                if (d >= 500) Ach.Unlock("bighit");
            }
            if (knockPow > 0)
            {
                flash = 1f;
                float res = boss ? 0.06f : elite ? 0.35f : 1f;
                if (dir.sqrMagnitude > 0.0001f) knock += dir.normalized * knockPow * 4.5f * res;
            }
            int dn = Game.I.save.dmgNumbers;
            if (dn == 0 || (dn == 1 && crit))
            {
                Color c = col ?? (crit ? new Color(1f, 0.85f, 0.2f) : Color.white);
                string txt = d >= 10 ? Mathf.RoundToInt(d).ToString() : StatInfo.Num(Mathf.Round(d * 10f) / 10f);
                Fx.Text(Pos + Vector2.up * radius, crit ? txt + "!" : txt, c, crit ? 1.25f : (col.HasValue ? 0.6f : 0.8f));
            }
            if (knockPow > 0)
            {
                Sfx.Play(crit ? "crit" : "hit", crit ? 0.5f : 0.3f, 0.12f);
                Fx.Burst(Pos, def.blood, crit ? 5 : 2, 4f, 0.1f, 0.3f);
            }
            if (hp <= 0) Die(d);
        }

        void Die(float lastDmg)
        {
            dead = true;
            All.Remove(this);
            if (summoner != null)
            {   // boss minions and breakables (cocoons, flags): no kill credit, no drops, no XP, no on-kill effects
                Fx.Burst(Pos, def.blood, 9, 6f, 0.14f, 0.45f);
                Sfx.Play("kill", 0.3f, 0.15f, 1.1f);
                Destroy(gameObject);
                return;
            }
            var g = Game.I;
            var run = g.run;
            run.kills++;
            Skills.OnKill(this);
            g.save.See("e:" + def.id);
            Vector2 pos = Pos;

            Fx.Burst(pos, def.blood, boss ? 70 : elite ? 24 : 9, boss ? 11f : 6f, boss ? 0.25f : 0.14f, boss ? 1f : 0.45f);
            if (brain != null) brain.OnDeath();
            Fx.Glow(pos, new Color(def.blood.r, def.blood.g, def.blood.b, 0.6f), radius * 3f, 0.25f);
            Sfx.Play("kill", boss ? 1f : 0.35f, 0.15f, boss ? 0.6f : 1f);

            g.OnKill(pos);
            if (boss) Orb.DropChest(pos + Vector2.right * 0.5f);
            else if (elite && Random.value < run.EliteChestChance) { run.eliteChests++; Orb.DropChest(pos + Vector2.right * 0.5f); }
            int orbs = def.orbs;
            if (run.activeRoute == 1 && Random.value < 0.4f) orbs++;
            if ((elite || boss) && run.Has("thon_thien"))
            {
                run.levelStats[Stat.MaxHP] += 3; run.Recalc(); run.hp += 3;
                Fx.Text(g.player.Pos + Vector2.up, "Thôn Thiên +3 Sinh Lực", new Color(0.8f, 0.5f, 1f), 0.9f);
            }
            if (orbs > 0) Orb.Drop(pos, orbs);
            if (!boss && Random.value < 0.025f) Orb.DropHeal(pos);

            if (run.Has("huyet_kiem_tam") && run.kills % 25 == 0)
            {
                run.bloodStacks++;
                Fx.Text(g.player.Pos + Vector2.up, "Huyết Kiếm +1%", new Color(1f, 0.3f, 0.3f), 0.8f);
            }
            if (run.Has("hoa_long_chau") && !boss)
            {
                float r = 1.7f;
                Fx.Ring(pos, new Color(1f, 0.5f, 0.15f), 0.2f, r, 0.3f);
                Fx.Glow(pos, new Color(1f, 0.5f, 0.15f, 0.7f), r * 2f, 0.25f);
                Sfx.Play("heavy", 0.25f, 0.2f, 1.4f, 0.08f);
                var list = Snapshot();
                foreach (var e in list)
                    if (!e.dead && Vector2.Distance(e.Pos, pos) < r + e.radius)
                        e.Damage(lastDmg * 0.5f, false, e.Pos - pos, 0.5f, null, new Color(1f, 0.55f, 0.2f));
                Release(list);
            }
            if (poisonT > 0 && (run.PoisonSpreads || (poisonSkill && run.HasCard("doc_an"))))
            {
                var list = Snapshot();
                int spread = 0;
                foreach (var e in list)
                    if (!e.dead && spread < 3 && Vector2.Distance(e.Pos, pos) < 2.5f) { e.ApplyPoison(poisonDps); spread++; }
                Release(list);
            }
            if (def.beh == EBeh.Bomber) Explode(1.9f, false);
            if (def.beh == EBeh.Splitter)
            {
                for (int i = 0; i < 2; i++)
                    Spawn(Content.MiniSlime, pos + Random.insideUnitCircle * 0.4f, wave, g.dir.HpMul, g.dir.DmgMul).knock = Random.insideUnitCircle.normalized * 4f;
            }
            if (boss)
            {
                Fx.Hitstop(0.35f);
                Fx.Shake(0.8f, 0.8f);
                Fx.Ring(pos, Color.white, 0.5f, 10f, 0.8f);
                Fx.Burst(pos, new Color(1f, 0.85f, 0.4f), 50, 14f, 0.2f, 1.2f, true);
                Sfx.Play("j_boss", 0.9f, 0f);
                Ach.Unlock("boss1");
                g.dir.OnBossDeath();
            }
            else if (elite) { Fx.Shake(0.2f, 0.2f); Fx.Hitstop(0.05f); }
            Destroy(gameObject);
        }

        public void Vanish()
        {
            if (dead) return;
            dead = true;
            All.Remove(this);
            if (brain != null) brain.Stop();
            Fx.Burst(Pos, new Color(0.7f, 0.7f, 0.8f, 0.7f), 6, 3f);
            Destroy(gameObject);
        }

        // ---------------------------------------------------------------- queries
        /// <summary>Nearest elite or boss within <paramref name="maxDist"/> (Kiếm Tâm homing); null when there is none.</summary>
        public static Enemy NearestBig(Vector2 p, float maxDist, HashSet<Enemy> skip = null)
        {
            Enemy best = null;
            float bd = maxDist * maxDist;
            for (int i = 0; i < All.Count; i++)
            {
                var e = All[i];
                if (e.dead || !(e.elite || e.boss) || (skip != null && skip.Contains(e))) continue;
                float d = ((Vector2)e.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        public static Enemy Nearest(Vector2 p, float maxDist, HashSet<Enemy> skip = null)
        {
            Enemy best = null;
            float bd = maxDist * maxDist;
            for (int i = 0; i < All.Count; i++)
            {
                var e = All[i];
                if (e.dead || (skip != null && skip.Contains(e))) continue;
                float d = ((Vector2)e.transform.position - p).sqrMagnitude - e.radius * e.radius;
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }
    }
}
