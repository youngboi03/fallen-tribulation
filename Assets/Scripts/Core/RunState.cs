using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    public class ShopSlot
    {
        public WeaponDef w; public ItemDef it; public int tier, price; public bool locked, sold;
    }

    public class LevelOpt { public Mod[] mods; public int tier; public SkillCardDef card; }

    /// <summary>Everything about the current run: stats, inventory, economy, shop, level-ups.</summary>
    public class RunState
    {
        public CharDef ch; public int charIndex, kiep;
        public int wave, gold, level = 1, pendingLevels, kills, bloodStacks;
        public float xp, hp, riteT;
        public int activeRoute, routeOffer;
        public bool reviveUsed;
        public StatBlock stats = new StatBlock(), levelStats = new StatBlock();
        public List<WeaponInst> weapons = new List<WeaponInst>();
        public List<ItemDef> items = new List<ItemDef>();
        public int maxWeapons = 6;
        public int[] tagCount = new int[(int)Tag.COUNT];
        public ShopSlot[] shop = new ShopSlot[4];
        public int rerolls, levelRerolls;
        public bool shopRolled;
        public LevelOpt[] levelOpts;
        public int hitsThisWave;
        public float bestHit, dmgDealt;
        public int chests, bestCombo;
        public ItemDef chestItem;
        public ItemDef[] relicChoices;   // boss treasure: pick 1 of 3
        public int relicWave;
        public bool eventPending;
        public EventDef currentEvent;
        public List<string> seenEvents = new List<string>();
        public SaveData save;
        public bool[] unlockedAtStart;
        public bool[] bossAlt = new bool[4];
        public EnemyDef Boss(int slot) => Content.BossFor(slot, bossAlt[Mathf.Clamp(slot, 0, 3)]);

        public RunState(int ci, int kiep, SaveData s)
        {
            charIndex = ci; ch = Content.Chars[ci]; this.kiep = kiep; save = s;
            gold = 30 + s.meta[4] * 12;
            for (int i = 0; i < bossAlt.Length; i++) bossAlt[i] = Random.value < 0.5f;
            unlockedAtStart = new bool[Content.Chars.Count];
            for (int i = 0; i < unlockedAtStart.Length; i++) unlockedAtStart[i] = Content.CharUnlocked(i, s);
            Recalc();
            hp = MaxHP;
        }

        public int skillStage;
        public List<string> skillCards = new List<string>();
        public float skillDmg;
        public float playTime;   // seconds of actual play (not paused, not in cutscenes)
        public float dmgEff, skillEff;   // damage capped at the target's remaining HP (no overkill): all hits / Thần Thông hits
        public Dictionary<string, float> skillDmgBy = new Dictionary<string, float>();
        public int stageUpWave = -1;
        public bool stageCallout;
        public bool guardUsed, hoMenhUsed;   // once per wave: Nguyên Anh hộ chủ / Hộ Mệnh Đan
        public int kiemKhi, linhLung;        // Kiếm Khí Hộ Thể tiers / Linh Lung Bảo Tháp stacks, both reset each wave
        public bool HasCard(string id) => skillCards.Contains(id);
        /// <summary>A run built around Thần Thông: at least one Ngộ Đạo card or Thần Lực ≥ 20 (shop bias, spec section 7).</summary>
        /// <summary>Elite chests dropped this wave; each one divides the chance of the next (Brotato-style falloff).</summary>
        public int eliteChests;
        public float EliteChestChance => (activeRoute == 2 ? 0.6f : 0.3f) / (1f + eliteChests);
        public bool SkillBuild => skillCards.Count > 0 || stats[Stat.SkillPower] >= 20f;
        public float SPAt(int w) => Content.SkillR(w) * WaveDirector.ExpectedTrashHp(w);
        public float SP => SPAt(wave);
        public float SkillCooldown => Mathf.Max(SkillNum.MinCd, ch.skillCd / (1f + Mathf.Max(0f, stats[Stat.SkillHaste]) / 100f));

        public float MaxHP => Mathf.Max(5f, Mathf.Round(stats[Stat.MaxHP]));
        public float XpMul => 1f + save.meta[5] * 0.07f;
        public float XpNeed => Content.XpNeed(level);
        public int Count(string id) { int n = 0; foreach (var i in items) if (i.id == id) n++; return n; }
        public bool Has(string id) => Count(id) > 0;
        public int TagCount(Tag t) => tagCount[(int)t];
        public static int SynTier(int count) => count >= 6 ? 3 : count >= 4 ? 2 : count >= 2 ? 1 : 0;
        public int ExtraChains => (TagCount(Tag.Loi) >= 2 ? 1 : 0) + (TagCount(Tag.Loi) >= 6 ? 2 : 0);
        public int ExtraPierce => TagCount(Tag.Phu) >= 4 ? 1 : 0;
        public float BurnMul => TagCount(Tag.Hoa) >= 4 ? 2f : 1f;
        public float PoisonMul => (TagCount(Tag.Doc) >= 2 ? 1.5f : 1f) * (TagCount(Tag.Doc) >= 6 ? 2f : 1f);
        public bool PoisonSpreads => TagCount(Tag.Doc) >= 4;

        public void Recalc()
        {
            stats.Clear();
            stats[Stat.MaxHP] = 20;
            stats.Add(ch.mods);
            for (int i = 0; i < 4; i++) stats[Content.Meta[i].stat] += save.meta[i] * Content.Meta[i].per;
            stats.Add(levelStats);
            foreach (var it in items) stats.Add(it.mods);

            System.Array.Clear(tagCount, 0, tagCount.Length);
            foreach (var w in weapons) foreach (var t in w.def.tags) tagCount[(int)t]++;
            foreach (var syn in Content.Synergies)
            {
                int tier = SynTier(TagCount(syn.tag));
                for (int k = 0; k < tier; k++) stats.Add(syn.mods[k]);
                if (tier >= 3) Ach.Unlock("syn6");
            }
            maxWeapons = 6 + Count("song_tu_bi_kip");
            foreach (var w in weapons) save.See("w:" + w.def.id);
            foreach (var it in items) save.See("i:" + it.id);
            if (hp > MaxHP) hp = MaxHP;
        }

        public int DistinctTags() { int n = 0; foreach (var c in tagCount) if (c > 0) n++; return n; }

        public float DamageMult()
        {
            float m = 1f;
            if (Has("ngu_hanh_ban") && DistinctTags() >= 5) m *= 1.5f;
            if (Has("van_kiem_quy_tong")) m *= 1f + 0.12f * TagCount(Tag.Kiem);
            if (Has("cuong_chien_y")) m *= 1f + (1f - hp / MaxHP) * 0.5f;
            if (riteT > 0) m *= 1.6f;
            if (Has("thien_thu_vo_tu")) m *= 0.85f;   // Thiên Thư Vô Tự: weapons pay for the skill bonus
            if (Has("tich_linh_ho_lo")) m *= 1f + 0.02f * (wave - 1);
            if (Has("kim_thiem")) m *= 1f + Mathf.Min(0.4f, gold / 10 * 0.01f);
            return m;
        }

        // ------------------------------------------------------------ economy
        float goldFrac;
        /// <summary>Gold per linh khí shrinks in later waves so the shop stays meaningful (XP is unaffected).</summary>
        public float GoldMul => 1f / (1f + 0.08f * (wave - 1));

        public void GainOrb(int v)
        {
            goldFrac += v * GoldMul;
            int g = Mathf.FloorToInt(goldFrac);
            goldFrac -= g;
            gold += g;
            if (gold >= 400) Ach.Unlock("rich");
            xp += v * XpMul;
            while (xp >= XpNeed)
            {
                xp -= XpNeed; level++; pendingLevels++;
                stats[Stat.MaxHP] += 1; levelStats[Stat.MaxHP] += 1; hp += 1;
                Game.I.OnLevelUp(level);
            }
        }

        int Price(float b) => Mathf.Max(1, Mathf.RoundToInt(b * (1f + (wave - 1) * 0.26f)));
        public int RerollCost => Mathf.RoundToInt(1 + wave * 0.75f + rerolls * (2 + wave / 3));
        public int LevelRerollCost => 2 + wave / 2 + levelRerolls * 2;

        public int RollTier()
        {
            float l = 1f + Mathf.Max(-50f, stats[Stat.Luck]) / 100f;
            float p3 = Mathf.Clamp01((wave - 11) * 0.02f * l);
            float p2 = Mathf.Clamp01((wave - 5) * 0.04f * l);
            float p1 = Mathf.Clamp01(((wave - 1) * 0.07f + 0.05f) * l);
            float r = Random.value;
            if (r < p3) return 3;
            if (r < p3 + p2) return 2;
            if (r < p3 + p2 + p1) return 1;
            return 0;
        }

        ShopSlot GenSlot()
        {
            int t = RollTier();
            var s = new ShopSlot { tier = t };
            if (Random.value < 0.42f)
            {
                WeaponDef w = null;
                if (weapons.Count > 0 && Random.value < 0.35f)
                {   // bias towards tags the player already owns — builds should come together
                    var owned = new List<WeaponDef>();
                    foreach (var def in Content.Weapons)
                        foreach (var mine in weapons)
                            foreach (var tg in mine.def.tags) if (def.HasTag(tg) && !owned.Contains(def)) owned.Add(def);
                    if (owned.Count > 0) w = owned[Random.Range(0, owned.Count)];
                }
                if (w == null) w = Content.Weapons[Random.Range(0, Content.Weapons.Count)];
                s.w = w; s.price = Price(w.price * Content.TierPrice[t]);
            }
            else
            {
                ItemDef pick = null;
                bool skillSlot = SkillBuild && Random.value < 0.15f;   // a Thần Thông build sees its items more often
                for (int tt = t; tt >= 0 && pick == null; tt--)
                {
                    var pool = Content.Items.FindAll(i => i.tier == tt && Count(i.id) < i.Cap && (!skillSlot || System.Array.IndexOf(Content.SkillItemIds, i.id) >= 0));
                    if (pool.Count > 0) pick = pool[Random.Range(0, pool.Count)];
                }
                if (pick == null && skillSlot)
                    for (int tt = t; tt >= 0 && pick == null; tt--)
                    {
                        var pool = Content.Items.FindAll(i => i.tier == tt && Count(i.id) < i.Cap);
                        if (pool.Count > 0) pick = pool[Random.Range(0, pool.Count)];
                    }
                if (pick == null) pick = Content.Items[0];
                s.it = pick; s.tier = pick.tier; s.price = Price(pick.price);
            }
            return s;
        }

        public void RollShop()
        {
            for (int i = 0; i < shop.Length; i++)
                if (shop[i] == null || !shop[i].locked || shop[i].sold) shop[i] = GenSlot();
                else shop[i].price = shop[i].price; // locked slots keep their price
        }

        public bool Reroll()
        {
            int c = RerollCost;
            if (gold < c) return false;
            gold -= c; rerolls++;
            RollShop();
            return true;
        }

        public WeaponInst MergeTarget(WeaponDef d, int tier)
        {
            if (tier >= 3) return null;
            foreach (var w in weapons) if (w.def == d && w.tier == tier) return w;
            return null;
        }

        /// <summary>null = can buy, otherwise the reason.</summary>
        public string CantBuy(int i)
        {
            var s = shop[i];
            if (s == null || s.sold) return "Đã bán";
            if (gold < s.price) return "Thiếu linh thạch";
            if (s.w != null && weapons.Count >= maxWeapons && MergeTarget(s.w, s.tier) == null) return "Hết ô vũ khí";
            if (s.it != null && Count(s.it.id) >= s.it.Cap) return "Đã đạt giới hạn";
            return null;
        }

        public bool Buy(int i)
        {
            if (CantBuy(i) != null) return false;
            var s = shop[i];
            gold -= s.price; s.sold = true; s.locked = false;
            if (s.w != null)
            {
                AddWeapon(s.w, s.tier);   // full slots: CantBuy guaranteed a partner, so the new copy merges away at once
            }
            else items.Add(s.it);
            Recalc();
            if (s.it != null && s.it.mods != null) foreach (var m in s.it.mods) if (m.s == Stat.MaxHP && m.v > 0) hp += m.v;
            CheckTier4();
            return true;
        }

        public WeaponInst AddWeapon(WeaponDef d, int tier)
        {
            var w = new WeaponInst(d, tier);
            weapons.Add(w);
            w.CreateView(Game.I.player ? Game.I.player.transform.position : Vector3.zero);
            Recalc();
            CheckTier4();
            AutoMerge();
            return weapons.Contains(w) ? w : weapons.Find(x => x.def == d);
        }

        /// <summary>Merges every same-id same-grade pair (cascading) until none is left; max grade stays as is.</summary>
        public void AutoMerge()
        {
            for (bool again = true; again;)
            {
                again = false;
                foreach (var w in weapons)
                    if (MergePartner(w) != null) { Merge(w); again = true; break; }
            }
        }

        public int SellValue(WeaponInst w) => Mathf.Max(1, Mathf.RoundToInt(w.def.price * Content.TierPrice[w.tier] * 0.4f * (1f + (wave - 1) * 0.09f)));

        public void Sell(WeaponInst w)
        {
            if (weapons.Count <= 1) return;
            gold += SellValue(w);
            weapons.Remove(w); w.DestroyView();
            Recalc();
        }

        public WeaponInst MergePartner(WeaponInst w)
        {
            if (w.tier >= 3) return null;
            foreach (var o in weapons) if (o != w && o.def == w.def && o.tier == w.tier) return o;
            return null;
        }

        public void Merge(WeaponInst w)
        {
            var p = MergePartner(w);
            if (p == null) return;
            weapons.Remove(p); p.DestroyView();
            w.SetTier(w.tier + 1);
            Sfx.Play("breakthrough", 0.7f);
            Recalc();
            CheckTier4();
        }

        void CheckTier4() { foreach (var w in weapons) if (w.tier >= 3) Ach.Unlock("tier4"); }

        public ItemDef RollChestItem()
        {
            int t = Mathf.Min(3, RollTier() + 1);
            for (int tt = t; tt >= 0; tt--)
            {
                var pool = Content.Items.FindAll(i => i.tier == tt && Count(i.id) < i.Cap);
                if (pool.Count > 0) return pool[Random.Range(0, pool.Count)];
            }
            return Content.Items[0];
        }

        /// <summary>Three distinct Huyền/Tiên items; from the 2nd boss on, one is guaranteed Tiên phẩm.</summary>
        public ItemDef[] RollRelics()
        {
            var res = new List<ItemDef>();
            for (int k = 0; k < 3; k++)
            {
                int minTier = k == 0 && wave >= 10 ? 3 : 2;
                var pool = Content.Items.FindAll(i => i.tier >= minTier && Count(i.id) < i.Cap && !res.Contains(i));
                if (pool.Count == 0) pool = Content.Items.FindAll(i => i.tier >= 1 && Count(i.id) < i.Cap && !res.Contains(i));
                if (pool.Count > 0) res.Add(pool[Random.Range(0, pool.Count)]);
            }
            return res.ToArray();
        }

        public void TakeRelic(int i)
        {
            var it = relicChoices[Mathf.Clamp(i, 0, relicChoices.Length - 1)];
            relicChoices = null; relicWave = wave; chests--;
            float before = MaxHP;
            items.Add(it);
            Recalc();
            if (MaxHP > before) hp += MaxHP - before;
        }

        public void TakeChest(bool sell)
        {
            var it = chestItem;
            if (sell) gold += ChestSellValue;
            chestItem = null; chests--;
            if (it == null || sell) return;
            float before = MaxHP;
            items.Add(it);
            Recalc();
            if (MaxHP > before) hp += MaxHP - before;
        }

        public int ChestSellValue => chestItem == null ? 0 : Mathf.Max(1, Mathf.RoundToInt(chestItem.price * 0.6f * (1f + (wave - 1) * 0.09f)));

        // ------------------------------------------------------------ level-ups
        public int ChoosingLevel => level - pendingLevels + 1;
        public bool IsBreakthrough => ChoosingLevel % 10 == 0;

        /// <summary>Ngộ Đạo cards not yet taken: this character's signature cards, or the shared ones whose weapon-Đạo condition holds.</summary>
        public List<SkillCardDef> CardPool(bool signature)
        {
            var res = new List<SkillCardDef>();
            foreach (var c in Content.SkillCards)
            {
                if (skillCards.Contains(c.id)) continue;
                if (signature ? c.charId != ch.id : c.charId != null) continue;
                if (c.needTag >= 0 && TagCount((Tag)c.needTag) < 2) continue;
                if (c.id == "song_thi" && ch.id == "ma_tu" && skillStage == 0) continue;   // stage I Huyết Tế has no damage to repeat
                res.Add(c);
            }
            return res;
        }

        static Stat DrawStat(List<Stat> pool)
        {
            float total = 0f;
            foreach (var s in pool) total += Content.LevelWeight(s);
            float r = Random.value * total;
            int k = pool.Count - 1;
            for (int i = 0; i < pool.Count; i++) { r -= Content.LevelWeight(pool[i]); if (r <= 0f) { k = i; break; } }
            var pick = pool[k]; pool.RemoveAt(k);
            return pick;
        }

        public void RollLevelOpts()
        {
            var opts = new List<LevelOpt>();
            var pool = new List<Stat>(Content.LevelStats);
            bool brk = IsBreakthrough;
            float l = 1f + Mathf.Max(-50f, stats[Stat.Luck]) / 100f;
            int lv = ChoosingLevel;
            if (brk)
            {   // Ngộ Đạo (spec 5.1): 1 signature card + 1 shared card, the rest Tiên stat cards; a missing signature card becomes a second shared card
                var cards = new List<SkillCardDef>();
                var sig = CardPool(true);
                if (sig.Count > 0) cards.Add(sig[Random.Range(0, sig.Count)]);
                var shared = CardPool(false);
                for (int need = cards.Count == 0 ? 2 : 1; need > 0 && shared.Count > 0; need--)
                {
                    var c = shared[Random.Range(0, shared.Count)]; shared.Remove(c); cards.Add(c);
                }
                while (opts.Count < 4 - cards.Count)
                {
                    var s1 = DrawStat(pool); var s2 = DrawStat(pool);
                    opts.Add(new LevelOpt { tier = 3, mods = new[] { new Mod(s1, Content.LevelValue(s1, 3)), new Mod(s2, Content.LevelValue(s2, 2)) } });
                }
                foreach (var c in cards) opts.Add(new LevelOpt { tier = 3, mods = new Mod[0], card = c });
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    float r = Random.value;
                    float p3 = 0.01f * lv * 0.2f * l, p2 = (0.03f + lv * 0.006f) * l, p1 = (0.18f + lv * 0.012f) * l;
                    int tier = r < p3 ? 3 : r < p3 + p2 ? 2 : r < p3 + p2 + p1 ? 1 : 0;
                    var s1 = DrawStat(pool);
                    opts.Add(new LevelOpt { tier = tier, mods = new[] { new Mod(s1, Content.LevelValue(s1, tier)) } });
                }
            }
            levelOpts = opts.ToArray();
        }

        public void ChooseLevel(int i)
        {
            var o = levelOpts[i];
            float before = MaxHP;
            levelStats.Add(o.mods);
            if (o.card != null) skillCards.Add(o.card.id);
            pendingLevels--;
            Recalc();
            if (MaxHP > before) hp += MaxHP - before;
            levelOpts = null;
        }

        public bool RerollLevel()
        {
            int c = LevelRerollCost;
            if (gold < c) return false;
            gold -= c; levelRerolls++;
            RollLevelOpts();
            return true;
        }
    }
}
