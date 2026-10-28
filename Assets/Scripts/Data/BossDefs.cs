using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VK
{
    public enum BMove { Dash, Fan, Ring, Spiral, Sweep, Cone, Zones, Pillars, Nova, Line, Blink, Summon, Hook }
    public enum BTier { Base, P2, I, II }
    public enum ZLayout { AroundPlayer, Random, Grid, Checker, Quadrants, EdgeRing, Radial, Between }

    /// <summary>
    /// One row of a boss move table (spec section 6). Every number is data; BossMoves/BossHooks read them.
    /// Field meaning per kind (unused fields stay 0):
    ///   Dash   : n traces (real + decoys), reps dashes, len, width, speed (ALWAYS a multiplier of the boss speed, riders included: Vong Am = 3.0 = 60% of the host dash 5.0), radius (end blast), hook "dark" = screen tint
    ///   Fan    : n bullets, angle (spread deg), speed, reps volleys, gap (s between volleys)
    ///   Ring   : n bullets, speed, reps volleys (each offset half a step), gap
    ///   Spiral : n arms, reps shots per arm, speed (deg/s), dur, gap (fire interval), radius2 (s at which rotation reverses, 0 = never)
    ///   Sweep  : n beams, len, width, angle (arc swept, deg), dur, speed (start offset from the player, deg), radius (angular spacing between beams), radius2 (safe gap in deg for a long arc), gap (s between beams)
    ///   Cone   : radius, angle, reps, gap
    ///   Zones  : n, radius, layout, life (0 = explode after tele), tick (damage interval), reps (waves), gap, gaps (how many of n sit around the player, rest random), dur (follow time before lock)
    ///   Pillars: n, radius, gap
    ///   Nova   : radius (r0), radius2 (r1; r0 &gt; r1 = collapsing), dur, gaps (safe gaps), angle (gap width deg), reps (rings), gap, speed (gap rotation per ring, deg)
    ///   Line   : n, layout, width, life, slow (fraction), dur (slow time), reps (rows of a grid), len
    ///   Blink  : dur (vanish time), radius (slash), len / radius2 (min / max landing distance from the player)
    ///   Summon : n, hook = minion def id, life (minion lifetime), kit.summonCap is the alive cap
    ///   Hook   : BossHooks entry named by `hook`; the numbers are described per row in BossKits.cs
    ///            Rows that use Hook where the spec names a primitive: van_buc_trieu (Line), liet_hoa_thanh (Nova Annulus), phan_thien_chien_ky (Sweep from banners).
    ///   gaps   : Nova = number of safe gaps; Zones = how many of n sit around the player (rest random); Spiral/Ring unused;
    ///            Summon rider (phun_moi) = every `gaps`-th cast of the host move spawns; Hook = per row (ma_anh_huyet_y bullets per clone, hu_khong_mon bullets per fan)
    /// Rider rows (after != null) amend the host move and are never picked; their n is a cap on live objects, gap a delay.
    /// dmg is a multiplier of the boss damage D; tick rows apply it every `tick` seconds.
    /// </summary>
    public class BossMove
    {
        public string id, name;
        public BMove kind;
        public BTier tier;
        public bool ult;
        public float tele, dmg, cd;
        /// <summary>Second stage of a two-stage move (tele2/dmg2; 0 = unused). Only thien_dao_tich_diet: stage 1 = tele/dmg (pillars), stage 2 = tele2/dmg2 (full-field strike).</summary>
        public float tele2, dmg2;
        public int n = 1, reps = 1, gaps;
        public float len, width, speed, angle, radius, radius2, dur, gap, life, tick, slow;
        public ZLayout layout;
        public string hook;
        public string after;

        /// <summary>Shallow copy (hooks reuse rows of other kits, e.g. the Vạn Ma Quy Tông ghosts).</summary>
        public BossMove Clone() => (BossMove)MemberwiseClone();
    }

    public class BossKit
    {
        public string bossId, epithet, quoteMutate, quoteMaThan;
        public Color col, col2;
        public float rest;
        public int summonCap;
        public List<BossMove> moves = new List<BossMove>();

        public IEnumerable<BossMove> Tier(BTier t) => moves.Where(m => m.tier == t);
        public BossMove Ult(BTier t) => moves.FirstOrDefault(m => m.tier == t && m.ult);
        public BossMove Move(string id) => moves.FirstOrDefault(m => m.id == id);
    }

    /// <summary>Kiếp balance numbers (spec section 7). Stats come from Hp/Dmg/RampCap; the per-kiep boss factors never touch telegraph time (TeleFloor is the minimum).</summary>
    public static class KiepNum
    {
        public const float Hp = 0.20f, Dmg = 0.16f, RampCap = 1.3f;
        /// <summary>Boss cooldown and rest factor, indexed by kiep 0-5.</summary>
        public static readonly float[] BossCd = { 1f, 1f, 1f, 0.92f, 0.92f, 0.85f };
        /// <summary>Boss bullet speed factor, indexed by kiep 0-5.</summary>
        public static readonly float[] BossShot = { 1f, 1f, 1f, 1.06f, 1.06f, 1.10f };
        /// <summary>Shortest telegraph of a damaging boss move.</summary>
        public static float TeleFloor(int kiep) => kiep >= 3 ? 0.5f : 0.6f;
        /// <summary>Kiếp bonus grows over the run so early waves stay fair.</summary>
        public static float Ramp(int w) => Mathf.Min(RampCap, 0.2f + w / 12f);
        public static float HpMul(int k, int w) => 1f + Hp * k * Ramp(w);
        public static float DmgMul(int k, int w) => 1f + Dmg * k * Ramp(w);
    }
}
