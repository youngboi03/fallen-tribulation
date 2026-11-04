using System.Collections;
using UnityEngine;

namespace VK
{
    /// <summary>Boss spell effects (prefix "vfx"): strips load, pools and fire apply statuses, scorch marks stay until the wave ends.</summary>
    public partial class SkillTest
    {
        IEnumerator T_vfx_clips()
        {
            yield return Arena(0, 1, 0);
            foreach (var n in new[] { "poison_pool", "burning_ground", "scorch", "flame_tuft", "poison_aura", "burn_aura", "meteor", "acid_splash",
                "ink_pool", "blood_pool", "blood_rain", "blood_splash", "violet_tuft", "blood_tuft", "rune_violet", "rune_gold", "rune_blood", "thunder", "gold_smite" })
            {
                var c = Res.Vfx(n);
                Check("vfx_clip_" + n, c != null && c.frames.Length > 1 && c.ms > 0f, c == null ? "null" : c.frames.Length + " frames");
            }
        }

        BossHazard Look(Enemy owner, string look, Vector2 pos, float active)
        {
            var h = Hz(owner, HShape.Circle, pos, 0.2f, active, 0.5f);
            h.dmg = 0f; h.SetLook(look);
            return h;
        }

        IEnumerator T_vfx_poison_lingers()
        {
            yield return Arena(0, 1, 0); Tank(); BossHazard.ClearAll();
            var g = Game.I; var p = g.player;
            Look(null, "poison_pool", p.Pos, 0.8f);
            yield return Sec(0.15f);
            Check("vfx_poison_not_in_telegraph", p.poisonT <= 0f, p.poisonT.ToString());
            yield return Sec(0.4f);
            g.bot.Shot("vfx_poison_pool");
            yield return Sec(0.25f);   // past the landing splash
            g.bot.Shot("vfx_poison_settled");
            Check("vfx_poison_applied", p.poisonT > Player.PoisonLinger - 0.2f, p.poisonT.ToString());
            tankHold = false;   // HoldCentre refills HP every frame
            float hp = g.run.hp;
            yield return Sec(1.5f);   // the pool is gone; Poison keeps ticking
            Check("vfx_poison_ticks_after", BossHazard.All.Count == 0 && p.poisonT > 0f && g.run.hp < hp, (hp - g.run.hp).ToString());
            p.ClearStatus();
            Check("vfx_status_cleared", p.poisonT <= 0f && p.burnT <= 0f, "");
        }

        IEnumerator T_vfx_ring_needs_hit()   // a ring burns only on a real hit, never just by standing near or in i-frames
        {
            yield return Arena(0, 1, 0); Tank(); BossHazard.ClearAll();
            var p = Game.I.player;
            Look(null, "fire_ring", (Vector2)p.Pos + new Vector2(6f, 0f), 0.5f);
            yield return Sec(0.5f);
            Check("vfx_ring_far", p.burnT <= 0f, p.burnT.ToString());
            BossHazard.ClearAll();
            yield return Sec(p.InvulnLeft + 0.05f);   // a fresh run starts with i-frames
            var h = Look(null, "fire_ring", p.Pos, 0.5f); h.dmg = 10f;
            yield return Sec(0.45f);
            Check("vfx_ring_burn", p.burnT > 0f && BossHazard.PlayerHits > 0, p.burnT.ToString());
            BossHazard.ClearAll();
        }

        IEnumerator T_vfx_god_no_status_damage()
        {
            yield return Arena(0, 1, 0); BossHazard.ClearAll();
            var g = Game.I; g.player.god = true; float hp = g.run.hp;
            g.player.ApplyStatus(0, "test"); g.player.ApplyStatus(1, "test");
            yield return Sec(1f);
            Check("vfx_god_safe", Mathf.Approximately(g.run.hp, hp), hp + " -> " + g.run.hp);
            g.player.ClearStatus(); g.player.god = false;
        }

        IEnumerator T_vfx_scorch_stays()
        {
            yield return Arena(0, 1, 0); Tank(); BossHazard.ClearAll();
            var g = Game.I; var boss = BossAt(new Vector2(0f, 9f), 0);
            int n0 = VfxFx.ScorchCount;
            Look(boss, "fire_ground", (Vector2)g.player.Pos + new Vector2(4f, 0f), 0.6f);   // out of reach: a burn aura would still be fading at the end
            yield return Sec(0.45f);
            g.bot.Shot("vfx_fire_ground");
            yield return Sec(0.6f);
            Check("vfx_scorch_left", VfxFx.ScorchCount == n0 + 1, VfxFx.ScorchCount.ToString());
            g.bot.Shot("vfx_scorch");
            yield return Sec(0.1f);    // the capture runs at frame end: clearing now would blank it
            BossHazard.ClearAll();
            Check("vfx_scorch_cleared", VfxFx.ScorchCount == 0 && VfxFx.LiveCount == 0, VfxFx.ScorchCount + "/" + VfxFx.LiveCount);
        }

        IEnumerator T_vfx_perf()   // worst case on screen: several pools and burning ground plus a fire ring; logs real frame times
        {
            yield return Arena(0, 1, 0); Tank(); BossHazard.ClearAll();
            var boss = BossAt(new Vector2(0f, 9f), 0);
            for (int i = 0; i < 6; i++) Look(boss, i % 2 == 0 ? "poison_pool" : "fire_ground", new Vector2(-6f + i * 2.4f, -3f), 3f);
            var r = Hz(boss, HShape.Annulus, Vector2.zero, 0.05f, 3f); r.radius = 7f; r.radius2 = 6f; r.SetLook("fire_ring");
            yield return Sec(0.5f);
            float sum = 0f, worst = 0f; int n = 0;
            for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime, n++) { float d = Time.unscaledDeltaTime * 1000f; sum += d; worst = Mathf.Max(worst, d); yield return null; }
            Debug.Log("[PERF] vfx_frame avg=" + (sum / Mathf.Max(1, n)).ToString("0.0") + "ms worst=" + worst.ToString("0.0") + "ms frames=" + n);
            Check("vfx_perf_ran", n > 0, n.ToString());
            BossHazard.ClearAll();
        }

        IEnumerator T_vfx_wave2()   // the looks of the other four bosses, side by side
        {
            yield return Arena(0, 1, 0); Tank(); BossHazard.ClearAll();
            var g = Game.I; var boss = BossAt(new Vector2(0f, 9f), 0); int n0 = VfxFx.ScorchCount;
            Hz(boss, HShape.Circle, new Vector2(-6f, 3f), 0.1f, 1.6f).SetLook("ink_pool");
            Hz(boss, HShape.Circle, new Vector2(-6f, -2f), 0.1f, 1.6f).SetLook("blood_pool");
            var seal = Hz(boss, HShape.Circle, new Vector2(-2f, 3f), 1.2f, 0.4f); seal.SetLook("gold_seal");
            Hz(boss, HShape.Circle, new Vector2(-2f, -2f), 1.2f, 0.4f).SetLook("thunder");
            var beam = Hz(boss, HShape.Bar, new Vector2(1f, 3f), 0.1f, 1.6f); beam.len = 7f; beam.width = 0.6f; beam.angleDeg = -20f; beam.SetLook("violet_beam");
            var cone = Hz(boss, HShape.Cone, new Vector2(2f, -3f), 1.6f, 0.2f); cone.radius = 5f; cone.arcDeg = 70f; cone.angleDeg = 0f; cone.SetLook("sonic");
            yield return Sec(1.25f);
            g.bot.Shot("vfx_wave2");
            yield return Sec(0.1f);
            Check("vfx_seal_shown", VfxFx.LiveCount > 0, VfxFx.LiveCount.ToString());
            yield return Sec(0.6f);
            Check("vfx_thunder_scorch", VfxFx.ScorchCount == n0 + 1, VfxFx.ScorchCount.ToString());
            BossHazard.ClearAll();
            Check("vfx_wave2_clear", VfxFx.ScorchCount == 0, VfxFx.ScorchCount.ToString());
        }

        IEnumerator T_vfx_blood_rain()   // Huyết Vũ is a rain front of uneven patches, not a grid of cells
        {
            yield return BossArena("b3", 5, 15); Tank(); BossHazard.ClearAll();
            var g = Game.I; g.player.god = true;
            boss.brain.ForceMove("huyet_vu");
            yield return Sec(0.9f);
            var hs = BossHazard.All.FindAll(h => h != null && h.moveId == "huyet_vu");
            float lo = 99f, hi = 0f; bool round = hs.Count > 0;
            foreach (var h in hs) { round &= h.shape == HShape.Circle; lo = Mathf.Min(lo, h.radius); hi = Mathf.Max(hi, h.radius); }
            Check("vfx_rain_patches", round && hs.Count >= 12 && hi - lo > 0.1f, hs.Count + " r " + lo + ".." + hi);
            g.bot.Shot("vfx_rain_coming");
            yield return Sec(0.75f);
            g.bot.Shot("vfx_rain_front");
            yield return Sec(0.45f);
            g.bot.Shot("vfx_rain_landed");
            yield return Sec(0.1f);
            g.player.god = false; BossHazard.ClearAll();
        }

        IEnumerator Cast(string bossId, int wave, string move, string shot, float wait)
        {
            yield return BossArena(bossId, 5, wave); Tank(); BossHazard.ClearAll();
            var g = Game.I; g.player.god = true;
            boss.brain.ForceMove(move);
            yield return Sec(wait);
            g.bot.Shot(shot);
            yield return Sec(0.1f);
            Check(shot + "_cast", boss.brain.Log.Contains(move), "");
            g.player.god = false; BossHazard.ClearAll();
        }

        IEnumerator T_vfx_web() { yield return Cast("b2b", 10, "thien_la_dia_vong", "vfx_web", 1.3f); }
        IEnumerator T_vfx_hand() { yield return Cast("b4", 20, "thien_ma_giang_the", "vfx_hand", 1.2f); }
        IEnumerator T_vfx_bats() { yield return Cast("b1b", 5, "van_buc_trieu", "vfx_bats", 0.9f); }
        IEnumerator T_vfx_flags() { yield return Cast("b2", 10, "phan_thien_chien_ky", "vfx_flags", 1.75f); }
        IEnumerator T_vfx_heaven() { yield return Cast("b4", 20, "thien_dao_tich_diet", "vfx_heaven", 5.5f); }

        IEnumerator T_vfx_shots()
        {
            yield return Arena(0, 1, 0); Tank(); BossHazard.ClearAll();
            var g = Game.I; var boss = BossAt(new Vector2(0f, 9f), 0);
            Hz(boss, HShape.Circle, new Vector2(-4f, 2f), 0.9f, 0.5f).SetLook("meteor");
            var silk = Hz(boss, HShape.Segment, new Vector2(3f, -3f), 0.2f, 1.2f); silk.a = new Vector2(3f, -3f); silk.b = new Vector2(7f, 2f); silk.width = 0.5f; silk.SetLook("silk");
            yield return Sec(0.75f);   // the meteor stays off screen for the first 0.5 s of the fall
            g.bot.Shot("vfx_meteor_fall");
            yield return Sec(0.35f);
            g.bot.Shot("vfx_meteor_hit");
            yield return Sec(0.1f);    // the capture runs at frame end: clearing now would blank it
            BossHazard.ClearAll();
        }
    }
}
