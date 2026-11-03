using UnityEngine;

namespace VK
{
    public class Player : MonoBehaviour
    {
        public float radius = 0.36f;
        public Vector2 Vel => vel;
        public Vector2 Pos => transform.position;
        public bool IsDashing => dashT > 0;
        public float DashReady => Mathf.Clamp01(1f - dashCd / Mathf.Max(0.01f, DashCooldown));
        public bool god;
        public float SkillReady => Mathf.Clamp01(1f - skillCd / Mathf.Max(0.01f, Game.I.run != null ? Game.I.run.SkillCooldown : 1f));
        float skillCd, goldT;
        public float InvulnLeft => Mathf.Max(0f, iframe);
        public float SkillCdLeft => Mathf.Max(0f, skillCd);
        public void ReduceSkillCd(float s) { skillCd -= s; }
        public void SetSkillReady() { skillCd = 0f; }
        public bool TryCast()
        {
            var run = Game.I.run;
            if (run == null || skillCd > 0f) return false;
            skillCd = run.SkillCooldown;
            Skills.Cast(this, run);
            return true;
        }
        public void Shove(Vector2 v) { vel += v; }
        public void Invuln(float t) { iframe = Mathf.Max(iframe, t); goldT = t; }

        Transform body;
        SpriteRenderer sr, shadow, aura;
        SpriteAnimator anim;
        string charId;
        MaterialPropertyBlock mpb;
        Vector2 vel, lastMove = Vector2.right, dashDir;
        public float slowT, slowMul = 1f;   // BossHazard slow: movement multiplier while slowT > 0
        float dashT, dashCd, iframe, flash, animT, ghostT, dead;
        bool seq;   // EndSeq owns the sprite (death / ascension pose)
        static readonly Color AuraCol = new Color(0.4f, 0.9f, 1f, 0.18f);
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int FlashColId = Shader.PropertyToID("_FlashColor");

        public static Player Create()
        {
            var go = new GameObject("Player");
            var p = go.AddComponent<Player>();
            p.Build();
            return p;
        }

        void Build()
        {
            mpb = new MaterialPropertyBlock();
            var sh = new GameObject("shadow");
            sh.transform.SetParent(transform, false);
            sh.transform.localPosition = new Vector3(0, -0.42f, 0);
            sh.transform.localScale = new Vector3(0.75f, 0.25f, 1);
            shadow = sh.AddComponent<SpriteRenderer>();
            shadow.sprite = Res.Circle; shadow.color = new Color(0, 0, 0, 0.35f); shadow.sortingOrder = -120;

            var au = new GameObject("aura");
            au.transform.SetParent(transform, false);
            au.transform.localScale = Vector3.one * 2.2f;
            aura = au.AddComponent<SpriteRenderer>();
            aura.sprite = Res.Glow; aura.sharedMaterial = Res.AddMat; aura.sortingOrder = -110;
            aura.color = AuraCol;

            body = new GameObject("body").transform;
            body.SetParent(transform, false);
            sr = body.gameObject.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = Res.FlashMat;
            anim = body.gameObject.AddComponent<SpriteAnimator>();
        }

        public void Setup(CharDef c)
        {
            skillCd = 3f;
            sr.sprite = Res.Tile(c.sprite);
            transform.position = Vector3.zero;
            dead = 0; ClearPose();
            charId = c.id;
            anim.Base = sr.sprite;
            anim.Stop();
        }

        public void ResetForWave()
        {
            transform.position = Vector3.zero;
            vel = Vector2.zero; dashT = 0; dashCd = 0; iframe = 1f; dead = 0; goldT = 0; slowT = 0f; slowMul = 1f;
            skillCd = Mathf.Min(skillCd, 3f);
            ClearPose();
        }

        void ClearPose()
        {
            seq = false;
            body.localRotation = Quaternion.identity; body.localPosition = Vector3.zero;
            sr.color = Color.white; aura.color = AuraCol; shadow.color = new Color(0, 0, 0, 0.35f);
        }

        /// <summary>End-of-run pose, k 0..1: defeat topples, dims and dissolves; ascension glows, lifts and fades upward.</summary>
        public void EndPose(float k, bool win)
        {
            seq = true; anim.Stop();
            body.localPosition = win ? new Vector3(0, k * k * 3.5f, 0) : Vector3.zero;
            body.localRotation = win ? Quaternion.identity : Quaternion.Euler(0, 0, Mathf.Min(90f, k * 400f));
            var c = win ? Color.Lerp(Color.white, new Color(1f, 0.92f, 0.55f), Mathf.Min(1f, k * 2f)) : Color.Lerp(Color.white, new Color(0.45f, 0.45f, 0.55f), Mathf.Min(1f, k * 3f));
            c.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1f, k));
            sr.color = c;
            shadow.color = new Color(0, 0, 0, 0.35f * c.a);
            aura.color = new Color(AuraCol.r, AuraCol.g, AuraCol.b, AuraCol.a * c.a);
        }

        public float DashCooldown => 1.2f * (Game.I.run != null && Game.I.run.Has("phong_loi_hai") ? 0.65f : 1f);

        Vector2 ReadMove()
        {
            if (Game.I.bot != null) return Game.I.bot.Move(this);
            return GameInput.Move;
        }

        bool ReadSkill()
        {
            if (Game.I.bot != null) return Game.I.bot.WantSkill(this);
            return GameInput.Skill;
        }

        bool ReadDash()
        {
            if (Game.I.bot != null) return Game.I.bot.WantDash(this);
            return GameInput.Dash;
        }

        void Update()
        {
            var g = Game.I;
            if (g.state == GState.Over && dead > 0)
            {   // death animation
                dead += Time.deltaTime;
                if (seq) return;
                anim.Stop();
                body.localRotation = Quaternion.Euler(0, 0, Mathf.Min(90f, dead * 300f));
                sr.color = Color.Lerp(Color.white, new Color(0.5f, 0.2f, 0.2f, 0.7f), dead);
                return;
            }
            if (!g.Playing) return;
            float dt = Game.Dt;
            var run = g.run;

            Vector2 input = ReadMove();
            bool dashPressed = ReadDash();
            float spd = 5f * Mathf.Max(0.35f, 1f + run.stats[Stat.Speed] / 100f);
            if (slowT > 0f) slowT -= dt;
            dashCd -= dt; iframe -= dt; skillCd -= dt; goldT -= dt;
            if (run.riteT > 0) run.riteT -= dt;
            if (Game.T > 4f && run.wave == 1) g.ui.Hint("dash", GameInput.K("Nhấn <color=#FFD266>SPACE</color>", "Chạm nút <color=#FFD266>Lướt</color>", "Nhấn <color=#FFD266>(A)</color>") + " để lướt: bất tử trong lúc lướt, xuyên qua quái và đạn.");
            if (skillCd <= 0 && run.wave >= 1 && Game.T > 12f) g.ui.Hint("skill", "Thần Thông đã sẵn sàng! " + GameInput.K("Nhấn <color=#FFD266>E</color> (hoặc chuột trái)", "Chạm nút <color=#FFD266>Thần Thông</color>", "Nhấn <color=#FFD266>(X)</color>") + ": " + Skills.FormName(run.ch, run.skillStage) + " — " + run.ch.skillDesc + ".");
            if (skillCd <= 0 && ReadSkill()) TryCast();

            if (dashT > 0)
            {
                dashT -= dt;
                vel = dashDir * spd * 3.4f;
                ghostT -= dt;
                if (ghostT <= 0)
                {
                    ghostT = 0.025f;
                    Fx.Particle(Pos, Vector2.zero, new Color(0.5f, 0.95f, 1f, 0.55f), new Color(0.3f, 0.6f, 1f, 0f), 1f, 1f, 0.22f, sr.sprite, true, 0, 9);
                }
            }
            else
            {
                if (dashPressed && dashCd <= 0)
                {
                    dashDir = input.sqrMagnitude > 0.01f ? input.normalized : lastMove;
                    dashT = 0.16f; dashCd = DashCooldown;
                    iframe = Mathf.Max(iframe, 0.28f);
                    Sfx.Play("dash", 0.6f, 0.1f, 1.3f);
                    Fx.Burst(Pos + Vector2.down * 0.35f, new Color(0.8f, 0.7f, 0.6f, 0.7f), 6, 3f, 0.12f, 0.35f);
                }
                Vector2 target = input * spd * (slowT > 0f ? slowMul : 1f);
                vel = Vector2.MoveTowards(vel, target, dt * spd * 12f);
            }
            if (input.sqrMagnitude > 0.01f) lastMove = input.normalized;

            var pos = Arena.Clamp(Pos + vel * dt, radius);
            transform.position = pos;

            float regen = run.stats[Stat.Regen];
            if (regen > 0 && run.hp < run.MaxHP) run.hp = Mathf.Min(run.MaxHP, run.hp + regen * 0.12f * dt);

            // animation: hop while walking, breathe while idle
            bool moving = vel.sqrMagnitude > 0.25f;
            const float Size = 1.3f;   // the hero reads a bit larger than ordinary monsters
            // the walk strips are not good enough yet (SpriteAnimator stays for later): heroes hop like the monsters do
            if (moving)
            {
                animT += dt * 13f;
                float hop = Mathf.Abs(Mathf.Sin(animT)) * 0.1f;
                body.localPosition = new Vector3(0, hop, 0);
                body.localScale = new Vector3((1f + hop * 0.6f) * Size, (1f - hop * 0.6f) * Size, 1f);
            }
            else
            {
                anim.Stop();
                animT += dt * 3f;
                float breathe = 1f + Mathf.Sin(animT) * 0.03f;
                body.localPosition = Vector3.zero;
                body.localScale = new Vector3(Size, breathe * Size, 1f);
            }
            if (Mathf.Abs(vel.x) > 0.1f) sr.flipX = vel.x < 0;
            sr.sortingOrder = Mathf.RoundToInt(-pos.y * 5f);

            flash = Mathf.MoveTowards(flash, 0, dt * 6f);
            sr.GetPropertyBlock(mpb);
            if (goldT > 0) { mpb.SetFloat(FlashId, 0.35f + Mathf.Sin(Time.time * 12f) * 0.15f); mpb.SetColor(FlashColId, new Color(1f, 0.85f, 0.3f)); }
            else { mpb.SetFloat(FlashId, flash); mpb.SetColor(FlashColId, new Color(1f, 0.3f, 0.3f)); }
            sr.SetPropertyBlock(mpb);
            var c = sr.color; c.a = iframe > 0 && dashT <= 0 && goldT <= 0 && Mathf.Repeat(Time.time * 16f, 1f) < 0.5f ? 0.45f : 1f; sr.color = c;

            float hpk = run.hp / run.MaxHP;
            aura.color = run.riteT > 0 ? new Color(1f, 0.1f, 0.15f, 0.35f + Mathf.Sin(Time.time * 10f) * 0.1f) : goldT > 0 ? new Color(1f, 0.8f, 0.3f, 0.35f) : hpk < 0.3f ? new Color(1f, 0.25f, 0.25f, 0.18f + Mathf.Sin(Time.time * 8f) * 0.08f) : RealmAura(run.level);
            if (run.kiemKhi >= 12 && run.HasCard("kiem_khi")) aura.color = new Color(0.5f, 1f, 1f, 0.5f);   // Kiếm Khí full: the block is armed
            aura.transform.localScale = Vector3.one * (2.1f + Mathf.Sin(Time.time * 2f) * 0.1f) * (1f + 0.15f * Mathf.Min(3, run.level / 10));
            if (run.level >= 20 && g.state == GState.Playing)
            {   // Kim Đan and above: motes of light circle the hero
                moteT -= Time.deltaTime;
                if (moteT <= 0f)
                {
                    moteT = run.level >= 30 ? 0.08f : 0.14f;
                    float a = Random.value * 6.2832f; Color mc = RealmAura(run.level); mc.a = 0.9f;
                    Fx.Particle(Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.1f, new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * 1.2f, mc, new Color(mc.r, mc.g, mc.b, 0f), 0.09f, 0.02f, 0.7f, Res.Glow, true, 0f);
                }
            }

            // weapons orbit the player
            ringDeg = Mathf.Repeat(ringDeg + 52f * dt, 360f);   // one turn ≈ 6.9 s, frozen with the game (dt = Game.Dt)
            int n = run.weapons.Count;
            for (int i = 0; i < n; i++) run.weapons[i].Tick(RingHome(i, n), dt);
        }

        /// <summary>Degrees the weapon ring has turned; slots sit evenly on a circle around the hero.</summary>
        public float ringDeg;
        public Vector2 RingHome(int i, int n)
        {
            float ang = (90f + i * 360f / n + ringDeg) * Mathf.Deg2Rad;
            return Pos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (n == 1 ? 0.8f : 1.0f);
        }

        /// <summary>True only when the player was actually damaged (not for god/iframe/dash/dodge/block).</summary>
        public bool Hurt(float dmg, Vector2 from)
        {
            var g = Game.I;
            if (god || iframe > 0 || dashT > 0 || g.state != GState.Playing) return false;
            var run = g.run;
            if (Random.value * 100f < Mathf.Min(60f, run.stats[Stat.Dodge]))
            {
                Fx.Text(Pos + Vector2.up * 0.6f, "Né!", new Color(0.6f, 1f, 1f), 0.9f);
                iframe = 0.3f;
                Sfx.Play("tick", 0.6f);
                return false;
            }
            dmg = Skills.ModifyIncoming(dmg);
            if (dmg <= 0f)
            {   // Kiếm Khí Hộ Thể: blocked outright
                iframe = 0.4f;
                Fx.Text(Pos + Vector2.up * 0.6f, "Chặn!", new Color(0.5f, 1f, 1f), 1.1f);
                Fx.Ring(Pos, new Color(0.5f, 1f, 1f), 0.4f, 2.4f, 0.4f);
                Sfx.Play("bell", 0.6f, 0f, 1.4f, 0f);
                return false;
            }
            float armor = run.stats[Stat.Armor] + Skills.TempArmor;
            float mul = armor >= 0 ? 1f / (1f + armor / 15f) : 2f - 1f / (1f - armor / 15f);
            float d = Mathf.Max(1f, Mathf.Round(dmg * mul));
            run.hp -= d;
            run.hitsThisWave++;
            iframe = 0.5f; flash = 1f;
            Vector2 away = (Pos - from).normalized;
            vel += away * 7f;
            Fx.Text(Pos + Vector2.up * 0.6f, "-" + d, new Color(1f, 0.3f, 0.3f), 1.2f);
            Fx.Burst(Pos, new Color(0.9f, 0.15f, 0.2f), 10, 5f);
            Fx.Shake(0.28f, 0.25f);
            Fx.Hitstop(0.07f);
            Sfx.Play("hurt", 0.8f);
            g.ui.HurtFlash();
            if (run.hp <= 0 && run.level >= 30 && !run.guardUsed)
            {   // Nguyên Anh hộ chủ: the first lethal hit of a wave leaves 1 HP (before Bất Tử Phù)
                run.guardUsed = true; run.hp = 1f; iframe = Mathf.Max(iframe, 1f);
                Fx.Ring(Pos, new Color(0.85f, 0.7f, 1f), 0.3f, 5f, 0.7f);
                Fx.Text(Pos + Vector2.up * 1.1f, "Nguyên Anh hộ chủ!", new Color(0.85f, 0.7f, 1f), 1.2f, 1.4f);
                Sfx.Play("breakthrough", 0.6f, 0f);
            }
            if (run.hp <= 0) { run.hp = 0; g.OnPlayerDeath(); return true; }
            if (run.HasCard("ho_menh") && !run.hoMenhUsed && run.hp < run.MaxHP * 0.3f && skillCd <= 0f)
            {   // Hộ Mệnh Đan: below 30% HP with the skill ready, it casts itself (once per wave)
                run.hoMenhUsed = true;
                Fx.Text(Pos + Vector2.up * 1.1f, "Hộ Mệnh!", new Color(1f, 0.6f, 0.3f), 1.2f, 1.2f);
                TryCast();
            }
            return true;
        }

        float moteT;

        /// <summary>Aura color by realm: Luyện Khí cyan, Trúc Cơ jade, Kim Đan gold, Nguyên Anh white-violet (spec section 6).</summary>
        static Color RealmAura(int level)
        {
            switch (Mathf.Min(3, level / 10))
            {
                case 1: return new Color(0.35f, 1f, 0.65f, 0.27f);
                case 2: return new Color(1f, 0.85f, 0.35f, 0.33f);
                case 3: return new Color(0.85f, 0.7f, 1f, 0.4f);
                default: return new Color(0.4f, 0.9f, 1f, 0.15f);
            }
        }

        float lastHealText;
        public void Heal(float v)
        {
            var run = Game.I.run;
            if (run.hp >= run.MaxHP) return;
            run.hp = Mathf.Min(run.MaxHP, run.hp + v);
            if (Time.time - lastHealText > 0.25f)
            {
                lastHealText = Time.time;
                Fx.Text(Pos + Vector2.up * 0.7f, "+" + StatInfo.Num(v), new Color(0.4f, 1f, 0.5f), 0.75f);
            }
        }

        public void Revive()
        {
            iframe = 2.5f;
            Fx.Ring(Pos, new Color(1f, 0.85f, 0.4f), 0.3f, 7f, 0.9f);
            Fx.Burst(Pos, new Color(1f, 0.85f, 0.4f), 50, 10f, 0.2f, 1f, true);
            Sfx.Play("breakthrough", 1f, 0f);
            foreach (var e in new System.Collections.Generic.List<Enemy>(Enemy.All))
                if (e && !e.boss && Vector2.Distance(e.Pos, Pos) < 5f) e.Damage(99999, false, e.Pos - Pos, 3f, null);
        }

        public void Die()
        {
            dead = 0.001f;
            Fx.Burst(Pos, new Color(0.9f, 0.15f, 0.2f), 30, 7f, 0.18f, 0.8f);
            Fx.Hitstop(0.2f);
            foreach (var w in Game.I.run.weapons) w.Drop();
        }
    }
}
