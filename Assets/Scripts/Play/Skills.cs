using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    /// <summary>Thần Thông — one active skill per character (E / left mouse).</summary>
    public static class Skills
    {
        // Synthetic weapon defs so skill hits go through the normal damage pipeline (crit, lifesteal, statuses).
        static readonly WeaponDef KiemDef = new WeaponDef { id = "sk_kiem", name = "Kiếm Vũ", sprite = 104, tint = new Color(0.7f, 1f, 1f), type = WType.Fly,
            tags = new Tag[0], range = 7f, pierce = 3, speed = 13f, knock = 1f, look = ProjLook.Point, fx = new Color(0.5f, 1f, 1f) };
        static readonly WeaponDef HoaDef = new WeaponDef { id = "sk_hoa", name = "Đan Hỏa Bạo", sprite = 129, type = WType.Bolt,
            tags = new Tag[0], burn = 0.6f, knock = 1.8f, fx = new Color(1f, 0.5f, 0.15f) };
        static readonly WeaponDef ChanDef = new WeaponDef { id = "sk_chan", name = "Kim Cang Chấn", sprite = 117, type = WType.Swing,
            tags = new Tag[0], knock = 4f, fx = new Color(1f, 0.85f, 0.4f) };
        static readonly WeaponDef PhuDef = new WeaponDef { id = "sk_phu", name = "Định Thân Phù", sprite = 66, type = WType.Bolt,
            tags = new Tag[0], knock = 0.01f, fx = new Color(0.5f, 0.9f, 1f) };

        static readonly WeaponDef LoiDef = new WeaponDef { id = "sk_loi", name = "Thiên Lôi Dẫn", sprite = 130, type = WType.Chain,
            tags = new Tag[0], knock = 0.5f, fx = new Color(0.7f, 0.85f, 1f) };

        static WeaponInst Inst(WeaponDef d, RunState r, float pct)
        {
            d.dmg = (6f + 1.2f * r.level) * pct;
            return new WeaponInst(d, 0);
        }

        public static void Cast(Player p, RunState r)
        {
            Vector2 pos = p.Pos;
            switch (r.ch.id)
            {
                case "kiem_tu":
                {
                    var w = Inst(KiemDef, r, 1.5f);
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i * Mathf.PI * 2f / 12f;
                        Projectile.FireWeapon(w, pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
                    }
                    Fx.Ring(pos, new Color(0.5f, 1f, 1f), 0.3f, 3f, 0.4f);
                    Fx.Shake(0.2f, 0.2f);
                    Sfx.Play("swing", 0.8f, 0f, 1.6f, 0f);
                    Sfx.Play("zap", 0.4f, 0f, 1.8f, 0f);
                    break;
                }
                case "dan_tu":
                {
                    var w = Inst(HoaDef, r, 2.5f);
                    float rad = 3.5f;
                    Combat.Splash(pos, rad, w, 1f, null);
                    Fx.Ring(pos, new Color(1f, 0.5f, 0.15f), 0.3f, rad, 0.4f);
                    Fx.Glow(pos, new Color(1f, 0.45f, 0.1f, 0.8f), rad * 2.4f, 0.4f);
                    Fx.Burst(pos, new Color(1f, 0.55f, 0.15f), 40, 10f, 0.2f, 0.6f, true);
                    p.Heal(Mathf.Round(r.MaxHP * 0.25f));
                    Fx.Shake(0.35f, 0.3f);
                    Fx.Hitstop(0.05f);
                    Sfx.Play("heavy", 0.9f, 0f, 0.8f, 0f);
                    Sfx.Play("shoot", 0.6f, 0f, 0.6f, 0f);
                    break;
                }
                case "the_tu":
                {
                    var w = Inst(ChanDef, r, 2f);
                    float rad = 3.2f;
                    p.Invuln(2f);
                    Combat.Splash(pos, rad, w, 1f, null);
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
                    var w = Inst(PhuDef, r, 0.8f);
                    float rad = 5.5f;
                    var list = Enemy.Snapshot();
                    foreach (var e in list)
                    {
                        if (e.dead || Vector2.Distance(e.Pos, pos) > rad + e.radius) continue;
                        e.Freeze(2.5f);
                        Combat.Hit(e, w, e.Pos - pos);
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
                    var w = Inst(LoiDef, r, 2.2f);
                    var list = Enemy.Snapshot();
                    list.RemoveAll(e => e.dead || Vector2.Distance(e.Pos, pos) > 10f);
                    list.Sort((a, b) => Vector2.SqrMagnitude(a.Pos - pos).CompareTo(Vector2.SqrMagnitude(b.Pos - pos)));
                    for (int i = 0; i < Mathf.Min(8, list.Count); i++)
                    {
                        var e = list[i];
                        Vector2 ep = e.Pos;
                        Fx.Lightning(ep + new Vector2(Random.Range(-1f, 1f), 12f), ep, new Color(0.75f, 0.85f, 1f), 0.4f, 0.3f, 12, 0.7f);
                        Fx.Lightning(ep + new Vector2(Random.Range(-1f, 1f), 12f), ep, Color.white, 0.14f, 0.2f, 12, 0.4f);
                        Fx.Ring(ep, new Color(0.7f, 0.85f, 1f), 0.2f, 1.4f, 0.3f);
                        e.Freeze(1f);
                        Combat.Hit(e, w, ep - pos);
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
                    float cost = Mathf.Floor(r.hp * 0.15f);
                    if (r.hp - cost < 1) cost = Mathf.Max(0, r.hp - 1);
                    r.hp -= cost;
                    r.riteT = 5f;
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
}
