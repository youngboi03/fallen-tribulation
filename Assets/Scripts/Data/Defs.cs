using UnityEngine;

namespace VK
{
    public enum Tag { Kiem, The, Hoa, Loi, Phu, Doc, COUNT }
    public enum WType { Thrust, Swing, Bolt, Chain, Fly }
    public enum ProjLook { Glow, Spin, Point }
    public enum EBeh { Chase, Swarm, Charger, Shooter, Splitter, Boss, Bomber, Mimic, Blinker, Static }

    public class WeaponDef
    {
        public string id, name, desc;
        public int sprite; public Color tint = Color.white;
        public WType type; public Tag[] tags;
        public float dmg, cd, range, crit, knock = 1f, splash, burn, poison, speed = 10f, freeze;
        public int pierce, chains, price;
        public ProjLook look = ProjLook.Glow; public Color fx = Color.white;
        public bool Melee => type == WType.Thrust || type == WType.Swing;
        public bool HasTag(Tag t) { foreach (var x in tags) if (x == t) return true; return false; }
    }

    public class ItemDef
    {
        public string id, name, desc;
        public int sprite; public Color tint = Color.white;
        public int tier, price, max;   // max 0 = the tier's stack cap (Content.StackCap)
        public Mod[] mods;
        /// <summary>How many copies one run may hold.</summary>
        public int Cap => max > 0 ? max : Content.StackCap[tier];
    }

    public class EnemyDef
    {
        public string id, name;
        public int sprite; public Color tint = Color.white; public Color blood = new Color(0.8f, 0.15f, 0.2f);
        public float hp, speed, dmg, scale = 1f, weight = 1f;
        public EBeh beh; public int orbs = 1, minWave = 1;
        public bool elite;
    }

    public class SkillForm { public string name, desc, full; }   // desc: what changes from the stage before; full: the whole form (codex)

    public class CharDef
    {
        public string id, name, title, desc, startWeapon, unlockDesc, skill, skillDesc;
        public float skillCd = 10f;
        public SkillForm[] forms;      // stages II-IV
        public Color skillColor = Color.white;
        public int sprite;
        public Mod[] mods;
    }

    /// <summary>Ngộ Đạo card (spec section 5): <c>charId</c> null = shared, <c>needTag</c> = weapon Đạo with at least 2 owned weapons, -1 = none.</summary>
    public class SkillCardDef
    {
        public string id, name, desc, charId;
        public int needTag = -1;
    }

    public class MetaDef
    {
        public string name, desc; public Stat stat; public float per; public bool special;
    }

    public class AchDef
    {
        public string id, name, desc;
        public AchDef(string id, string name, string desc) { this.id = id; this.name = name; this.desc = desc; }
    }

    public class SynergyDef
    {
        public Tag tag; public string[] desc = new string[3]; public Mod[][] mods = new Mod[3][];
    }
}
