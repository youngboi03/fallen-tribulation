using System;
using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    /// <summary>Thần Thông — one active skill per character (E / left mouse). Damage scales with the wave (RunState.SP), not the weapon pipeline.</summary>
    public static class Skills
    {
        public static bool LastHitCrit;
        public static int CastCount;

        public static string FormName(CharDef c, int stage) => stage <= 0 || c.forms == null || stage > c.forms.Length ? c.skill : c.forms[stage - 1].name;

        /// <summary>One skill hit: SP · coef · (1 + Thần Lực/100 + Sát Thương/200), crit, ±8%. Bosses take half. Returns the damage dealt.</summary>
        public static float Hit(Enemy e, float coef, string src, Vector2 dir, float knock = 0.5f)
        {
            if (e == null || e.dead) return 0f;
            var run = Game.I.run;
            var st = run.stats;
            float d = run.SP * coef * (1f + st[Stat.SkillPower] / 100f + st[Stat.Damage] / 200f);
            bool crit = UnityEngine.Random.value * 100f < st[Stat.Crit];
            if (crit) d *= 1.5f + st[Stat.CritDmg] / 100f;
            d *= UnityEngine.Random.Range(0.92f, 1.08f);
            if (e.boss) d *= SkillNum.BossMul;
            LastHitCrit = crit;
            e.skillHits++;
            e.Damage(d, crit, dir, knock, null);
            if (UnityEngine.Random.value * 100f < st[Stat.Lifesteal] + (run.riteT > 0 ? 25f : 0f)) Game.I.player.Heal(1);
            run.skillDmg += d;
            run.skillDmgBy.TryGetValue(src, out var acc);
            run.skillDmgBy[src] = acc + d;
            return d;
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

        static SkillShot KiemShot(float k) => new SkillShot
        {
            src = "kiem", coef = SkillNum.KiemHit * k, speed = 13f, life = 1.6f, radius = 0.25f, pierce = SkillNum.KiemPierce,
            homing = true, homingRange = SkillNum.KiemHomeRange, sprite = Res.Tile(104), spriteAngle = Res.SpriteAngle(104),
            col = new Color(0.5f, 1f, 1f), tint = new Color(0.7f, 1f, 1f), look = ProjLook.Point
        };

        /// <summary>Cast the skill. <paramref name="echo"/> (Song Thi card) halves every coefficient and skips buffs, invulnerability and the rite.</summary>
        public static void Cast(Player p, RunState r, bool echo = false)
        {
            CastCount++;
            Vector2 pos = p.Pos;
            float k = echo ? 0.5f : 1f;
            switch (r.ch.id)
            {
                case "kiem_tu":
                {
                    for (int i = 0; i < SkillNum.KiemSwords; i++)
                    {
                        float a = i * Mathf.PI * 2f / SkillNum.KiemSwords;
                        Projectile.FireSkill(KiemShot(k), pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
                    }
                    Fx.Ring(pos, new Color(0.5f, 1f, 1f), 0.3f, 3f, 0.4f);
                    Fx.Shake(0.2f, 0.2f);
                    Sfx.Play("swing", 0.8f, 0f, 1.6f, 0f);
                    Sfx.Play("zap", 0.4f, 0f, 1.8f, 0f);
                    break;
                }
                case "dan_tu":
                {
                    float rad = SkillNum.DanR;
                    Splash(pos, rad, SkillNum.DanHit * k, "dan_nova", 1.8f, (e, d) => e.ApplyBurn(d * 0.3f * r.BurnMul));
                    Fx.Ring(pos, new Color(1f, 0.5f, 0.15f), 0.3f, rad, 0.4f);
                    Fx.Glow(pos, new Color(1f, 0.45f, 0.1f, 0.8f), rad * 2.4f, 0.4f);
                    Fx.Burst(pos, new Color(1f, 0.55f, 0.15f), 40, 10f, 0.2f, 0.6f, true);
                    p.Heal(Mathf.Round(r.MaxHP * SkillNum.DanHeal * k));
                    Fx.Shake(0.35f, 0.3f);
                    Fx.Hitstop(0.05f);
                    Sfx.Play("heavy", 0.9f, 0f, 0.8f, 0f);
                    Sfx.Play("shoot", 0.6f, 0f, 0.6f, 0f);
                    break;
                }
                case "the_tu":
                {
                    float rad = SkillNum.TheR;
                    if (!echo) p.Invuln(SkillNum.TheInvuln);
                    Splash(pos, rad, SkillNum.TheHit * k, "the_wave", 4f);
                    Fx.Ring(pos, new Color(1f, 0.85f, 0.4f), 0.3f, rad, 0.35f);
                    Fx.Ring(pos, Color.white, 0.2f, rad * 0.7f, 0.25f);
                    Fx.Burst(pos, new Color(0.8f, 0.7f, 0.55f), 30, 9f, 0.18f, 0.5f);
                    Fx.Shake(0.45f, 0.35f);
                    Fx.Hitstop(0.08f);
                    Sfx.Play("heavy", 1f, 0f, 0.6f, 0f);
                    Sfx.Play("bell", 0.5f, 0f, 1.2f, 0f);
                    break;
                }
                case "phu_tu":
                {
                    float rad = SkillNum.PhuR;
                    var list = Enemy.Snapshot();
                    foreach (var e in list)
                    {
                        if (e.dead || Vector2.Distance(e.Pos, pos) > rad + e.radius) continue;
                        e.Freeze(SkillNum.PhuFreeze);
                        Hit(e, SkillNum.PhuHit * k, "phu", e.Pos - pos, 0.01f);
                        Fx.Lightning(pos, e.Pos, new Color(0.6f, 0.9f, 1f, 0.6f), 0.05f, 0.2f, 4, 0.15f);
                    }
                    Enemy.Release(list);
                    Fx.Ring(pos, new Color(0.5f, 0.9f, 1f), 0.5f, rad, 0.5f);
                    Fx.Glow(pos, new Color(0.5f, 0.9f, 1f, 0.5f), rad * 2f, 0.5f);
                    Fx.Shake(0.2f, 0.2f);
                    Sfx.Play("pluck", 0.8f, 0f, 0.8f, 0f);
                    Sfx.Play("bell", 0.4f, 0f, 1.6f, 0f);
                    break;
                }
                case "loi_tu":
                {
                    var list = Enemy.Snapshot();
                    list.RemoveAll(e => e.dead || Vector2.Distance(e.Pos, pos) > SkillNum.LoiRange);
                    list.Sort((a, b) => Vector2.SqrMagnitude(a.Pos - pos).CompareTo(Vector2.SqrMagnitude(b.Pos - pos)));
                    for (int i = 0; i < Mathf.Min(SkillNum.LoiBolts, list.Count); i++)
                    {
                        var e = list[i];
                        Vector2 ep = e.Pos;
                        Fx.Lightning(ep + new Vector2(UnityEngine.Random.Range(-1f, 1f), 12f), ep, new Color(0.75f, 0.85f, 1f), 0.4f, 0.3f, 12, 0.7f);
                        Fx.Lightning(ep + new Vector2(UnityEngine.Random.Range(-1f, 1f), 12f), ep, Color.white, 0.14f, 0.2f, 12, 0.4f);
                        Fx.Ring(ep, new Color(0.7f, 0.85f, 1f), 0.2f, 1.4f, 0.3f);
                        e.Freeze(SkillNum.LoiStun);
                        Hit(e, SkillNum.LoiHit * k, "loi", ep - pos, 0.5f);
                    }
                    Enemy.Release(list);
                    Fx.Glow(pos, new Color(0.6f, 0.8f, 1f, 0.6f), 6f, 0.3f);
                    Fx.Shake(0.35f, 0.3f);
                    Fx.Hitstop(0.05f);
                    Sfx.Play("zap", 1f, 0f, 0.7f, 0f);
                    Sfx.Play("heavy", 0.6f, 0f, 0.9f, 0f);
                    break;
                }
                case "ma_tu":
                {
                    if (echo) break;
                    float cost = Mathf.Floor(r.hp * SkillNum.MaCost);
                    if (r.hp - cost < 1) cost = Mathf.Max(0, r.hp - 1);
                    r.hp -= cost;
                    r.riteT = SkillNum.MaRite;
                    if (cost > 0) Fx.Text(pos + Vector2.up * 0.7f, "-" + cost, new Color(1f, 0.3f, 0.3f), 1f);
                    Fx.Ring(pos, new Color(0.9f, 0.1f, 0.2f), 0.3f, 3f, 0.4f);
                    Fx.Burst(pos, new Color(0.9f, 0.1f, 0.2f), 30, 7f, 0.16f, 0.6f, true);
                    Fx.Shake(0.25f, 0.2f);
                    Sfx.Play("hurt", 0.6f, 0f, 0.7f, 0f);
                    Sfx.Play("breakthrough", 0.5f, 0f, 0.7f, 0f);
                    break;
                }
            }
        }
    }

    /// <summary>Parameters of a projectile fired by a skill; contact damage goes through Skills.Hit.</summary>
    public class SkillShot
    {
        public string src; public float coef = 1, speed = 13, life = 1.2f, radius = 0.25f; public int pierce;
        public bool homing; public float homingRange = 7; public bool preferBig;
        public string returnSrc; public float returnCoef;
        public float orbitTime, orbitCoef;
        public float blastR, blastCoef;
        public float freeze, freezeBonus;
        public Sprite sprite; public float spriteAngle = 90f; public Color col = Color.white, tint = Color.white; public ProjLook look;
        public Action<Enemy> onHit;
    }
}
