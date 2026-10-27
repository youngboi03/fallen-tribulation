using System;
using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    /// <summary>Thần Thông — one active skill per character (E / left mouse). Damage scales with the wave (RunState.SP), not the weapon pipeline.</summary>
    public static class Skills
    {
        public static bool LastHitCrit;
        public static int CastCount, LastStrikes;
        public static Enemy LastBigTarget;
        static float riteExt, tuKhiAcc;

        public static string FormName(CharDef c, int stage) => stage <= 0 || c.forms == null || stage > c.forms.Length ? c.skill : c.forms[stage - 1].name;

        /// <summary>Lifesteal bonus while the Huyết Tế rite runs.</summary>
        public static float RiteLifesteal => Game.I.run != null && Game.I.run.skillStage >= 3 ? SkillNum.MaLifesteal4 : SkillNum.MaLifesteal;

        /// <summary>Thiết Bố Sam: +5 Armor for 5 s after a cast (added in Player.Hurt).</summary>
        public static float TempArmor => SkillRuntime.I != null && SkillRuntime.I.armorT > 0f ? 5f : 0f;

        /// <summary>Damage the player takes passes through here before armor (skill damage-reduction buffs).</summary>
        public static float ModifyIncoming(float dmg)
        {
            var run = Game.I.run;
            if (run != null && run.kiemKhi >= 12 && run.HasCard("kiem_khi")) { run.kiemKhi = 0; return 0f; }   // Kiếm Khí Hộ Thể: the hit is blocked outright
            var rt = SkillRuntime.I;
            return rt != null && rt.drT > 0f ? dmg * rt.drMul : dmg;
        }

        /// <summary>One skill hit: SP · coef · (1 + Thần Lực/100 + Sát Thương/200), crit, ±8%. Bosses take half. Returns the damage dealt.</summary>
        public static float Hit(Enemy e, float coef, string src, Vector2 dir, float knock = 0.5f)
        {
            if (e == null || e.dead) return 0f;
            var run = Game.I.run;
            var st = run.stats;
            float d = run.SP * coef * (1f + st[Stat.SkillPower] / 100f + st[Stat.Damage] / 200f);
            bool eye = run.HasCard("than_nhan");   // Thần Nhãn: +15% Bạo Kích, +50% ST Bạo Kích on skill hits
            bool crit = UnityEngine.Random.value * 100f < st[Stat.Crit] + (eye ? 15f : 0f);
            if (crit) d *= 1.5f + (st[Stat.CritDmg] + (eye ? 50f : 0f)) / 100f;
            d *= UnityEngine.Random.Range(0.92f, 1.08f);
            if (e.boss) d *= SkillNum.BossMul;
            if (e.Burning && run.HasCard("bao_viem")) d *= 1.5f;   // Bạo Viêm Đan
            LastHitCrit = crit;
            e.skillHits++;
            Vector2 ep = e.Pos;
            e.Damage(d, crit, dir, knock, null);
            Seals(run, e, ep, d, crit, coef);
            if (run.HasCard("kiem_khi") && (src == "kiem" || src == "kiem_return" || src == "kiem_orbit") && run.kiemKhi < 12)
            {   // Kiếm Khí Hộ Thể: each sword hit stores a tier; the 12th arms a full block
                run.kiemKhi++;
                if (run.kiemKhi == 12)
                {
                    Fx.Ring(Game.I.player.Pos, new Color(0.5f, 1f, 1f), 0.4f, 1.8f, 0.5f);
                    Sfx.Play("bell", 0.5f, 0f, 1.5f, 0f);
                }
            }
            if (UnityEngine.Random.value * 100f < st[Stat.Lifesteal] + (run.riteT > 0 ? RiteLifesteal : 0f)) Game.I.player.Heal(1);
            run.skillDmg += d;
            run.skillDmgBy.TryGetValue(src, out var acc);
            run.skillDmgBy[src] = acc + d;
            return d;
        }

        static bool sealBounce;
        static float sealSfxT;

        /// <summary>Thần Nhãn mark and the four Đạo Ấn riders on a skill hit (spec 5.3). The Lôi bounce never bounces again.</summary>
        static void Seals(RunState run, Enemy e, Vector2 ep, float d, bool crit, float coef)
        {
            bool sfx = Time.time - sealSfxT > 0.08f;
            if (crit && run.HasCard("than_nhan"))
            {
                Color gold = new Color(1f, 0.85f, 0.3f);
                Fx.Ring(ep, gold, 0.1f, 0.55f, 0.25f); Fx.Burst(ep, gold, 3, 3f, 0.1f, 0.25f, true);
            }
            if (!e.dead)
            {
                if (run.HasCard("hoa_an"))
                {
                    e.ApplyBurn(d * 0.3f * run.BurnMul);
                    Fx.Burst(ep, new Color(1f, 0.5f, 0.15f), 3, 2.5f, 0.12f, 0.3f, true);
                    if (sfx) { Sfx.Play("heavy", 0.12f, 0.1f, 1.7f); sealSfxT = Time.time; }
                }
                if (run.HasCard("doc_an"))
                {
                    e.ApplyPoison(d * 0.33f * run.PoisonMul, true);
                    Fx.Burst(ep, new Color(0.5f, 1f, 0.4f), 3, 1.5f, 0.12f, 0.45f, true);
                    if (sfx) { Sfx.Play("pluck", 0.12f, 0.1f, 0.7f); sealSfxT = Time.time; }
                }
                if (run.HasCard("phu_an") && UnityEngine.Random.value < 0.25f)
                {
                    e.Freeze(1f);
                    Fx.Burst(ep, new Color(0.7f, 0.95f, 1f), 4, 3.5f, 0.1f, 0.3f, true);
                    if (sfx) { Sfx.Play("sk_chime", 0.12f, 0.1f, 1.6f); sealSfxT = Time.time; }
                }
            }
            if (run.HasCard("loi_an") && !sealBounce)
            {
                Enemy tgt = null; float best = 3.5f * 3.5f;
                var list = Enemy.Snapshot();
                foreach (var o in list)
                {
                    if (o == null || o.dead || o == e) continue;
                    float sq = (o.Pos - ep).sqrMagnitude;
                    if (sq < best) { best = sq; tgt = o; }
                }
                Enemy.Release(list);
                if (tgt != null)
                {
                    Fx.Lightning(ep, tgt.Pos, new Color(0.7f, 0.85f, 1f), 0.08f, 0.15f, 5, 0.25f);
                    if (sfx) { Sfx.Play("tick", 0.15f, 0.1f, 1.8f); sealSfxT = Time.time; }
                    sealBounce = true; bool lc = LastHitCrit;
                    Hit(tgt, coef * 0.5f, "loi_an", tgt.Pos - ep, 0.3f);
                    LastHitCrit = lc; sealBounce = false;
                }
            }
        }

        public static int Splash(Vector2 p, float r, float coef, string src, float knock, Action<Enemy, float> onHit = null)
        {
            int n = 0;
            var list = Enemy.Snapshot();
            foreach (var e in list)
            {
                if (e.dead || Vector2.Distance(e.Pos, p) >= r + e.radius) continue;
                float d = Hit(e, coef, src, e.Pos - p, knock);
                onHit?.Invoke(e, d);
                n++;
            }
            Enemy.Release(list);
            return n;
        }

        /// <summary>Bạo Phù: a skill-frozen enemy whose ice runs out explodes on its neighbours (a frozen enemy that dies does not).</summary>
        public static void OnSkillFreezeEnd(Enemy e)
        {
            var run = Game.I.run;
            if (run == null || run.skillStage < 2 || e == null || e.dead) return;
            Vector2 p = e.Pos;
            Fx.Ring(p, new Color(0.5f, 0.9f, 1f), 0.2f, SkillNum.PhuBurstR, 0.3f);
            Fx.Burst(p, new Color(0.7f, 0.95f, 1f), 10, 5f, 0.12f, 0.35f, true);
            Sfx.Play("heavy", 0.2f, 0.2f, 1.6f);
            Splash(p, SkillNum.PhuBurstR, SkillNum.PhuBurstHit, "phu_burst", 1f);
        }

        /// <summary>Per-wave resets for the Thần Thông state (called from Game.BeginWave).</summary>
        public static void OnWaveStart(RunState r)
        {
            r.guardUsed = false; r.hoMenhUsed = false; r.kiemKhi = 0; r.linhLung = 0;
        }

        /// <summary>Called by Enemy.Die before the enemy goes away.</summary>
        public static void OnKill(Enemy e)
        {
            var run = Game.I.run;
            if (run == null) return;
            if (run.HasCard("tu_khi") && tuKhiAcc < 3f && Game.I.player != null)
            {   // Tụ Khí: each kill shaves 0.15 s off the cooldown, 3 s per cooldown cycle
                float g = Mathf.Min(0.15f, 3f - tuKhiAcc); tuKhiAcc += g;
                Game.I.player.ReduceSkillCd(g);
                Vector2 from = e.Pos, to = Game.I.player.Pos;
                Fx.Particle(from, (to - from).normalized * 7f, new Color(0.6f, 1f, 0.9f, 0.9f), new Color(0.6f, 1f, 0.9f, 0f), 0.1f, 0.03f, 0.35f, Res.Glow, true, 0f);
            }
            if (run.skillStage >= 2 && run.ch.id == "ma_tu" && run.riteT > 0f && riteExt < SkillNum.MaExtendMax)
            {
                run.riteT += SkillNum.MaExtend; riteExt += SkillNum.MaExtend;
            }
        }

        static SkillShot KiemShot(RunState r, float k)
        {
            var s = new SkillShot
            {
                src = "kiem", coef = SkillNum.KiemHit * k, speed = 13f, life = 1.6f, radius = 0.25f, pierce = SkillNum.KiemPierce,
                homing = true, homingRange = SkillNum.KiemHomeRange, sprite = Res.Tile(104), spriteAngle = Res.SpriteAngle(104),
                col = new Color(0.5f, 1f, 1f), tint = new Color(0.7f, 1f, 1f), look = ProjLook.Point
            };
            if (r.skillStage >= 1) { s.returnSrc = "kiem_return"; s.returnCoef = SkillNum.KiemReturnHit * k; }
            if (r.skillStage >= 2) { s.orbitTime = SkillNum.KiemOrbitTime; s.orbitCoef = SkillNum.KiemOrbitHit * k; }
            return s;
        }

        static List<Enemy> Nearest(Vector2 pos, int n, float minD, float maxD)
        {
            var list = Enemy.Snapshot();
            list.RemoveAll(e => e.dead || Vector2.Distance(e.Pos, pos) < minD || Vector2.Distance(e.Pos, pos) > maxD);
            list.Sort((a, b) => Vector2.SqrMagnitude(a.Pos - pos).CompareTo(Vector2.SqrMagnitude(b.Pos - pos)));
            var res = list.Count > n ? list.GetRange(0, n) : new List<Enemy>(list);
            Enemy.Release(list);
            return res;
        }

        /// <summary>Cast the skill. <paramref name="echo"/> (Song Thi card) halves every coefficient and skips buffs, invulnerability and the rite.</summary>
        public static void Cast(Player p, RunState r, bool echo = false)
        {
            CastCount++;
            Vector2 pos = p.Pos;
            float k = echo ? 0.5f : 1f;
            int st = r.skillStage;
            var rt = SkillRuntime.I;
            switch (r.ch.id)
            {
                case "kiem_tu": CastKiem(p, r, pos, k, st, rt); break;
                case "dan_tu": CastDan(p, r, pos, k, st, rt, echo); break;
                case "the_tu": CastThe(p, r, pos, k, st, rt, echo); break;
                case "phu_tu": CastPhu(p, r, pos, k, st); break;
                case "loi_tu": CastLoi(p, r, pos, k, st, rt); break;
                case "ma_tu": CastMa(p, r, pos, k, st, rt, echo); break;
            }
            if (echo) return;
            SkillFx.Ladder(st, pos, r.ch.skillColor);
            tuKhiAcc = 0f;   // a new cooldown cycle starts
            Color c = r.ch.skillColor;
            if (rt != null && r.HasCard("du_ba"))
            {   // Dư Ba: weapons glow in the skill color and attack 35% faster for 4 s (WeaponInst.Cooldown / Tick)
                rt.duBaT = 4f; Fx.Ring(pos, c, 0.4f, 1.8f, 0.4f);
            }
            if (rt != null && r.HasCard("song_thi"))
            {   // Song Thi: a faded shade of the hero repeats the skill at 50% after 0.6 s
                Fx.Apparition(Res.Tile(r.ch.sprite), pos, Color.Lerp(c, Color.white, 0.4f), 5f, 0.6f);
                rt.After(0.6f, () =>
                {
                    if (p == null || Game.I.run != r || !Game.I.Playing) return;
                    Fx.Apparition(Res.Tile(r.ch.sprite), p.Pos, Color.Lerp(c, Color.white, 0.7f), 5f, 0.5f);
                    Cast(p, r, true);
                });
            }
        }

        // ---------------------------------------------------------------- Kiếm Tu
        static void CastKiem(Player p, RunState r, Vector2 pos, float k, int st, SkillRuntime rt)
        {
            bool hundred = r.HasCard("bach_kiem"), clever = r.HasCard("kiem_tam");
            int n = (st >= 2 ? SkillNum.KiemSwords3 : SkillNum.KiemSwords) + (hundred ? 8 : 0);   // Bách Kiếm Triều: +8 swords at ×0.7
            var cut = clever ? new Dictionary<Enemy, int>() : null;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var shot = KiemShot(r, hundred ? k * 0.7f : k);
                if (clever) { shot.preferBig = true; shot.stackHits = cut; }   // Kiếm Tâm Thông Minh
                Projectile.FireSkill(shot, pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
            }
            Fx.Ring(pos, new Color(0.5f, 1f, 1f), 0.3f, hundred ? 4.5f : 3f, 0.4f);
            Fx.Shake(0.2f, 0.2f);
            Sfx.Play("swing", 0.8f, 0f, 1.6f, 0f);
            Sfx.Play("zap", 0.4f, 0f, 1.8f, 0f);
            if (st >= 3 && rt != null)
            {
                // the giant sword falls where the most enemies stand together
                Enemy best = null; int bestN = -1;
                var cand = Nearest(pos, 40, 0f, SkillNum.KiemGiantRange);
                foreach (var e in cand)
                {
                    int c = 0;
                    foreach (var o in cand) if (Vector2.Distance(o.Pos, e.Pos) < 2.5f) c++;
                    if (c > bestN) { bestN = c; best = e; }
                }
                if (best != null)
                {
                    Vector2 tp = best.Pos;
                    Fx.Ring(tp, new Color(1f, 0.4f, 0.3f), SkillNum.KiemGiantR, SkillNum.KiemGiantR * 0.6f, SkillNum.KiemGiantDelay);
                    float kk = k;
                    rt.After(SkillNum.KiemGiantDelay, () =>
                    {
                        Splash(tp, SkillNum.KiemGiantR, SkillNum.KiemGiantHit * kk, "kiem_giant", 7f);
                        Fx.Pillar(tp, new Color(0.7f, 1f, 1f), 10f, 0.4f);
                        Fx.Ring(tp, new Color(0.5f, 1f, 1f), 0.4f, SkillNum.KiemGiantR, 0.4f);
                        Fx.Burst(tp, new Color(0.7f, 1f, 1f), 30, 9f, 0.16f, 0.5f, true);
                        Fx.Shake(0.5f, 0.35f);
                        Sfx.Play("heavy", 1f, 0f, 0.7f, 0f);
                    });
                }
            }
        }

        // ---------------------------------------------------------------- Đan Tu
        static void CastDan(Player p, RunState r, Vector2 pos, float k, int st, SkillRuntime rt, bool echo)
        {
            float rad = st >= 2 ? SkillNum.DanR3 : SkillNum.DanR;
            if (r.HasCard("bao_viem")) rad += Mathf.Min(1.5f, 0.15f * Nearest(pos, 200, 0f, rad).Count);   // Bạo Viêm Đan: the blast grows with the crowd
            Action<Enemy, float> burn = (e, d) => e.ApplyBurn(d * 0.3f * r.BurnMul);
            int caught = Splash(pos, rad, SkillNum.DanHit * k, "dan_nova", 1.8f, burn);
            float extraHeal = r.HasCard("cuu_chuyen") ? Mathf.Min(0.2f, 0.02f * caught) : 0f;   // Cửu Chuyển Hoàn Đan
            if (extraHeal > 0f) Fx.Burst(pos, new Color(0.5f, 1f, 0.6f), 14, 5f, 0.14f, 0.5f, true);
            Fx.Ring(pos, new Color(1f, 0.5f, 0.15f), 0.3f, rad, 0.4f);
            Fx.Glow(pos, new Color(1f, 0.45f, 0.1f, 0.8f), rad * 2.4f, 0.4f);
            Fx.Burst(pos, new Color(1f, 0.55f, 0.15f), 40, 10f, 0.2f, 0.6f, true);
            p.Heal(Mathf.Round(r.MaxHP * (SkillNum.DanHeal + extraHeal) * k));
            Fx.Shake(0.35f, 0.3f);
            Fx.Hitstop(0.05f);
            Sfx.Play("heavy", 0.9f, 0f, 0.8f, 0f);
            Sfx.Play("shoot", 0.6f, 0f, 0.6f, 0f);
            if (st >= 1)
            {
                foreach (var e in Nearest(pos, SkillNum.DanPills, rad, SkillNum.DanPillRange))
                {
                    var s = new SkillShot
                    {
                        src = "dan_pill", coef = 0f, speed = 12f, life = 1.5f, radius = 0.3f, homing = true, homingRange = 12f,
                        blastR = SkillNum.DanPillR, blastCoef = SkillNum.DanPillHit * k, col = new Color(1f, 0.5f, 0.15f), tint = new Color(1f, 0.7f, 0.3f), look = ProjLook.Glow,
                        onHit = en => en.ApplyBurn(r.SP * 0.3f * r.BurnMul)
                    };
                    Projectile.FireSkill(s, pos, e.Pos - pos);
                }
            }
            if (st >= 2 && !echo && rt != null)
            {
                Projectile.ClearHostileWithin(pos, rad);
                rt.Guard(SkillNum.DanGuardTime, SkillNum.DanGuardMul);
            }
            if (st >= 3 && rt != null)
            {
                for (int i = 1; i <= 2; i++)
                    rt.After(0.6f * i, () =>
                    {
                        Vector2 c = Game.I.player.Pos;
                        Splash(c, SkillNum.DanFollowR, SkillNum.DanFollowHit * k, "dan_followup", 1.5f, burn);
                        Fx.Ring(c, new Color(1f, 0.55f, 0.2f), 0.3f, SkillNum.DanFollowR, 0.4f);
                        Fx.Glow(c, new Color(1f, 0.45f, 0.1f, 0.7f), SkillNum.DanFollowR * 2.2f, 0.4f);
                        Fx.Burst(c, new Color(1f, 0.55f, 0.15f), 24, 9f, 0.16f, 0.5f, true);
                        Fx.Shake(0.3f, 0.25f);
                        Sfx.Play("heavy", 0.6f, 0f, 0.9f, 0f);
                    });
            }
        }

        // ---------------------------------------------------------------- Thể Tu
        static void CastThe(Player p, RunState r, Vector2 pos, float k, int st, SkillRuntime rt, bool echo)
        {
            float rad = st >= 1 ? SkillNum.TheR2 : SkillNum.TheR;
            float inv = st >= 3 ? SkillNum.TheInvuln4 : SkillNum.TheInvuln;
            if (!echo) p.Invuln(inv);
            Action<Enemy, float> stun = null;
            if (st >= 1) stun = (e, d) => { if (!e.dead) e.Freeze(SkillNum.TheStun); };
            Action wave = () =>
            {
                Splash(pos, rad, SkillNum.TheHit * k, "the_wave", 4f, stun);
                Fx.Ring(pos, new Color(1f, 0.85f, 0.4f), 0.3f, rad, 0.35f);
                Fx.Ring(pos, Color.white, 0.2f, rad * 0.7f, 0.25f);
                Fx.Burst(pos, new Color(0.8f, 0.7f, 0.55f), 30, 9f, 0.18f, 0.5f);
                Fx.Shake(0.45f, 0.35f);
                Fx.Hitstop(0.08f);
                Sfx.Play("heavy", 1f, 0f, 0.6f, 0f);
                Sfx.Play("bell", 0.5f, 0f, 1.2f, 0f);
                if (st >= 2 && rt != null) Fissures(pos, k, rt);
            };
            if (r.HasCard("son_bang") && rt != null)
            {   // Sơn Băng Địa Hãm: for 0.25 s before the wave, everything within 5 tiles is dragged in
                Fx.Ring(pos, new Color(0.8f, 0.65f, 0.4f), 5f, 0.6f, 0.25f);
                rt.During(0.25f, 0.025f, () =>
                {
                    var list = Enemy.Snapshot();
                    foreach (var e in list)
                    {
                        if (e.dead || e.boss) continue;
                        Vector2 rel = pos - e.Pos; float dist = rel.magnitude;
                        if (dist > 5.2f || dist < 1f) continue;
                        e.transform.position = e.Pos + rel / dist * Mathf.Min(14f * 0.025f, dist - 1f);
                    }
                    Enemy.Release(list);
                }, wave);
            }
            else wave();
            if (!echo && rt != null)
            {
                if (r.HasCard("thiet_bo_sam"))
                {   // Thiết Bố Sam
                    rt.armorT = 5f; Fx.Ring(pos, new Color(1f, 0.85f, 0.4f), 1.6f, 0.7f, 0.4f);
                }
                if (r.HasCard("bat_dong"))
                {   // Bất Động Minh Vương: while invulnerable, enemies that touch the hero are struck and thrown back
                    var last = new Dictionary<Enemy, float>(); float clock = 0f;
                    rt.During(inv, 0.1f, () =>
                    {
                        clock += 0.1f;
                        var pp = Game.I.player.Pos;
                        var list = Enemy.Snapshot();
                        foreach (var e in list)
                        {
                            if (e.dead || Vector2.Distance(e.Pos, pp) > p.radius + e.radius + 0.15f) continue;
                            if (last.TryGetValue(e, out float t) && clock - t < 0.5f) continue;
                            last[e] = clock;
                            Hit(e, SkillNum.TheHit * 0.5f * k, "the_thorns", e.Pos - pp, 6f);
                            Fx.Burst(e.Pos, new Color(1f, 0.85f, 0.4f), 5, 4f, 0.12f, 0.3f, true);
                        }
                        Enemy.Release(list);
                    });
                }
            }
            if (st >= 3 && !echo && rt != null)
            {
                rt.After(inv, () =>
                {
                    Vector2 c = Game.I.player.Pos;
                    Splash(c, SkillNum.TheAfterR, SkillNum.TheAfterHit * k, "the_after", 9f);
                    Fx.Ring(c, new Color(1f, 0.85f, 0.4f), 0.4f, SkillNum.TheAfterR, 0.5f);
                    Fx.Ring(c, Color.white, 0.3f, SkillNum.TheAfterR * 0.7f, 0.35f);
                    Fx.Burst(c, new Color(1f, 0.85f, 0.4f), 50, 12f, 0.2f, 0.6f, true);
                    Fx.Shake(0.7f, 0.45f);
                    Fx.Hitstop(0.1f);
                    Sfx.Play("heavy", 1f, 0f, 0.5f, 0f);
                });
            }
        }

        /// <summary>Four cracks across the ground, the first aimed at the nearest enemy; they spread outward for 0.3 s.</summary>
        static void Fissures(Vector2 pos, float k, SkillRuntime rt)
        {
            var near = Nearest(pos, 1, 0f, 30f);
            float a0 = near.Count > 0 ? Mathf.Atan2(near[0].Pos.y - pos.y, near[0].Pos.x - pos.x) : 0f;
            var dirs = new Vector2[4];
            for (int i = 0; i < 4; i++) { float a = a0 + i * Mathf.PI / 2f; dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
            var hit = new HashSet<Enemy>();
            float t = 0f;
            const float tick = 0.05f;
            rt.During(SkillNum.TheFissureTime, tick, () =>
            {
                t += tick;
                float reach = SkillNum.TheFissureLen * Mathf.Clamp01(t / SkillNum.TheFissureTime);
                var list = Enemy.Snapshot();
                foreach (var e in list)
                {
                    if (e.dead || hit.Contains(e)) continue;
                    Vector2 rel = e.Pos - pos;
                    for (int i = 0; i < 4; i++)
                    {
                        float along = Vector2.Dot(rel, dirs[i]);
                        float perp = Mathf.Abs(rel.x * dirs[i].y - rel.y * dirs[i].x);
                        if (along < 0f || along > reach + e.radius || perp > SkillNum.TheFissureW * 0.5f + e.radius) continue;
                        hit.Add(e);
                        Hit(e, SkillNum.TheFissureHit * k, "the_fissure", dirs[i], 2f);
                        if (!e.dead) e.Freeze(SkillNum.TheStun);
                        break;
                    }
                }
                Enemy.Release(list);
                for (int i = 0; i < 4; i++)
                {
                    Fx.Streak(pos + dirs[i] * reach * 0.5f, dirs[i], reach, new Color(0.9f, 0.75f, 0.35f, 0.8f), SkillNum.TheFissureW * 0.4f, 0.25f);
                    Fx.Burst(pos + dirs[i] * reach, new Color(0.8f, 0.65f, 0.4f), 3, 5f, 0.12f, 0.35f);
                }
            });
            Fx.Shake(0.4f, 0.3f);
        }

        // ---------------------------------------------------------------- Phù Tu
        static void CastPhu(Player p, RunState r, Vector2 pos, float k, int st)
        {
            float rad = st >= 3 ? SkillNum.PhuR4 : SkillNum.PhuR;
            float bonus = st >= 3 ? SkillNum.PhuBonus4 : SkillNum.PhuBonus;
            var list = Enemy.Snapshot();
            foreach (var e in list)
            {
                if (e.dead || Vector2.Distance(e.Pos, pos) > rad + e.radius) continue;
                e.SkillFreeze(SkillNum.PhuFreeze, bonus);
                Hit(e, SkillNum.PhuHit * k, "phu", e.Pos - pos, 0.01f);
                Fx.Lightning(pos, e.Pos, new Color(0.6f, 0.9f, 1f, 0.6f), 0.05f, 0.2f, 4, 0.15f);
            }
            Enemy.Release(list);
            Fx.Ring(pos, new Color(0.5f, 0.9f, 1f), 0.5f, rad, 0.5f);
            Fx.Glow(pos, new Color(0.5f, 0.9f, 1f, 0.5f), rad * 2f, 0.5f);
            Fx.Shake(0.2f, 0.2f);
            Sfx.Play("pluck", 0.8f, 0f, 0.8f, 0f);
            Sfx.Play("bell", 0.4f, 0f, 1.6f, 0f);
            if (st >= 1)
                foreach (var e in Nearest(pos, SkillNum.PhuFlyers, rad, SkillNum.PhuFlyRange))
                {
                    var s = new SkillShot
                    {
                        src = "phu_flyer", coef = SkillNum.PhuFlyHit * k, speed = 14f, life = 1.5f, radius = 0.3f, homing = true, homingRange = 12f,
                        freeze = SkillNum.PhuFlyFreeze, freezeBonus = bonus, sprite = Res.Tile(66), spriteAngle = 90f,
                        col = new Color(0.5f, 0.9f, 1f), tint = Color.white, look = ProjLook.Point
                    };
                    Projectile.FireSkill(s, pos, e.Pos - pos);
                }
        }

        // ---------------------------------------------------------------- Lôi Tu
        static void CastLoi(Player p, RunState r, Vector2 pos, float k, int st, SkillRuntime rt)
        {
            int bolts = st >= 3 ? SkillNum.LoiBolts4 : SkillNum.LoiBolts;
            var list = Nearest(pos, bolts, 0f, SkillNum.LoiRange);
            LastStrikes = list.Count;
            foreach (var e in list)
            {
                Vector2 ep = e.Pos;
                Fx.Lightning(ep + new Vector2(UnityEngine.Random.Range(-1f, 1f), 12f), ep, new Color(0.75f, 0.85f, 1f), 0.4f, 0.3f, 12, 0.7f);
                Fx.Lightning(ep + new Vector2(UnityEngine.Random.Range(-1f, 1f), 12f), ep, Color.white, 0.14f, 0.2f, 12, 0.4f);
                Fx.Ring(ep, new Color(0.7f, 0.85f, 1f), 0.2f, 1.4f, 0.3f);
                e.Freeze(SkillNum.LoiStun);
                Hit(e, SkillNum.LoiHit * k, "loi", ep - pos, 0.5f);
                if (st >= 1) Chain(ep, e, k);
            }
            Fx.Glow(pos, new Color(0.6f, 0.8f, 1f, 0.6f), 6f, 0.3f);
            Fx.Shake(0.35f, 0.3f);
            Fx.Hitstop(0.05f);
            Sfx.Play("zap", 1f, 0f, 0.7f, 0f);
            Sfx.Play("heavy", 0.6f, 0f, 0.9f, 0f);
            if (st >= 2 && rt != null)
            {
                rt.During(SkillNum.LoiFieldTime, SkillNum.LoiFieldTick, () =>
                {
                    Vector2 c = Game.I.player.Pos;
                    Fx.Ring(c, new Color(0.7f, 0.85f, 1f, 0.5f), SkillNum.LoiFieldR * 1.6f, SkillNum.LoiFieldR * 2f, 0.4f);
                    var inside = Nearest(c, 40, 0f, SkillNum.LoiFieldR);
                    if (inside.Count == 0) return;
                    var e = inside[UnityEngine.Random.Range(0, inside.Count)];
                    Fx.Lightning(c, e.Pos, new Color(0.8f, 0.9f, 1f), 0.1f, 0.15f, 6, 0.4f);
                    Hit(e, SkillNum.LoiFieldHit * k, "loi_field", e.Pos - c, 0.3f);
                    if (!e.dead) e.Freeze(SkillNum.LoiFieldStun);
                    Sfx.Play("zap", 0.25f, 0.2f, 1.3f);
                });
            }
            if (st >= 3 && rt != null)
                rt.After(SkillNum.LoiBigDelay, () =>
                {
                    Vector2 c = Game.I.player.Pos;
                    Enemy big = null;
                    foreach (var e in Nearest(c, 200, 0f, SkillNum.LoiBigRange)) if (big == null || e.maxHp > big.maxHp) big = e;
                    if (big == null) return;
                    LastBigTarget = big;
                    Vector2 bp = big.Pos;
                    Fx.Pillar(bp, new Color(0.75f, 0.85f, 1f), 14f, 0.5f);
                    Fx.Lightning(bp + Vector2.up * 14f, bp, Color.white, 0.6f, 0.4f, 14, 0.9f);
                    Fx.Ring(bp, new Color(0.8f, 0.9f, 1f), 0.4f, 3f, 0.4f);
                    Fx.Shake(0.55f, 0.4f);
                    Hit(big, SkillNum.LoiBigHit * k, "loi_big", Vector2.zero, 0.5f);
                    if (!big.dead) big.Freeze(SkillNum.LoiBigStun);
                    Sfx.Play("zap", 1f, 0f, 0.5f, 0f);
                    Sfx.Play("heavy", 0.9f, 0f, 0.6f, 0f);
                });
        }

        /// <summary>Lôi Liên: the bolt jumps on to nearby enemies, losing power each step.</summary>
        static void Chain(Vector2 from, Enemy first, float k)
        {
            var skip = new HashSet<Enemy> { first };
            float mul = 1f; Vector2 a = from;
            for (int i = 0; i < SkillNum.LoiChainHops; i++)
            {
                var n = Enemy.Nearest(a, SkillNum.LoiChainR, skip);
                if (n == null) break;
                skip.Add(n);
                mul *= SkillNum.LoiChainDecay;
                Fx.Lightning(a, n.Pos, new Color(0.7f, 0.85f, 1f), 0.12f, 0.2f, 6, 0.4f);
                Hit(n, SkillNum.LoiChainHit * mul * k, "loi_chain", n.Pos - a, 0.3f);
                a = n.Pos;
            }
        }

        // ---------------------------------------------------------------- Ma Tu
        static void CastMa(Player p, RunState r, Vector2 pos, float k, int st, SkillRuntime rt, bool echo)
        {
            if (echo) return;
            float cost = Mathf.Floor(r.hp * SkillNum.MaCost);
            if (r.hp - cost < 1) cost = Mathf.Max(0, r.hp - 1);
            r.hp -= cost;
            r.riteT = SkillNum.MaRite;
            riteExt = 0f;
            if (cost > 0) Fx.Text(pos + Vector2.up * 0.7f, "-" + cost, new Color(1f, 0.3f, 0.3f), 1f);
            Fx.Ring(pos, new Color(0.9f, 0.1f, 0.2f), 0.3f, 3f, 0.4f);
            Fx.Burst(pos, new Color(0.9f, 0.1f, 0.2f), 30, 7f, 0.16f, 0.6f, true);
            Fx.Shake(0.25f, 0.2f);
            Sfx.Play("hurt", 0.6f, 0f, 0.7f, 0f);
            Sfx.Play("breakthrough", 0.5f, 0f, 0.7f, 0f);
            if (st >= 1)
            {
                float rad = st >= 3 ? SkillNum.MaNovaR4 : SkillNum.MaNovaR;
                float hit = st >= 3 ? SkillNum.MaNovaHit4 : SkillNum.MaNovaHit;
                Splash(pos, rad, hit, "ma_nova", 3f);
                Fx.Ring(pos, new Color(0.9f, 0.1f, 0.2f), 0.4f, rad, 0.4f);
                Fx.Glow(pos, new Color(0.9f, 0.1f, 0.2f, 0.6f), rad * 2f, 0.4f);
                Fx.Burst(pos, new Color(0.9f, 0.1f, 0.2f), 30, 9f, 0.18f, 0.5f, true);
            }
            if (st >= 2 && rt != null) rt.Guard(SkillNum.MaGuardTime, SkillNum.MaGuardMul);
        }
    }

    /// <summary>Parameters of a projectile fired by a skill; contact damage goes through Skills.Hit.</summary>
    public class SkillShot
    {
        public string src; public float coef = 1, speed = 13, life = 1.2f, radius = 0.25f; public int pierce;
        public bool homing; public float homingRange = 7; public bool preferBig;
        public Dictionary<Enemy, int> stackHits;   // Kiếm Tâm: hits already landed on each target by this cast
        public string returnSrc; public float returnCoef;
        public float orbitTime, orbitCoef;
        public float blastR, blastCoef;
        public float freeze, freezeBonus;
        public Sprite sprite; public float spriteAngle = 90f; public Color col = Color.white, tint = Color.white; public ProjLook look;
        public Action<Enemy> onHit;
    }
}
