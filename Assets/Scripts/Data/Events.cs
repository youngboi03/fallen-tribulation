using System;
using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    /// <summary>Kỳ Ngộ — Slay-the-Spire style choice events between some waves.</summary>
    public class EventOption
    {
        public string label, desc;
        public Func<RunState, bool> can = r => true;
        public Func<RunState, string> apply;
    }

    public class EventDef
    {
        public string id, title, text;
        public int sprite;
        public EventOption[] options;
    }

    public static class Events
    {
        public static readonly int[] Waves = { 3, 7, 12, 17 };

        static string Perm(RunState r, params Mod[] mods)
        {
            float before = r.MaxHP;
            r.levelStats.Add(mods);
            r.Recalc();
            if (r.MaxHP > before) r.hp += r.MaxHP - before;
            return StatInfo.Fmt(mods).Replace("\n", ", ");
        }

        static Mod M(Stat s, float v) => new Mod(s, v);
        static EventOption Leave(string label = "Rời đi") => new EventOption { label = label, desc = "Không có gì xảy ra.", apply = r => "Ngươi lặng lẽ rời đi." };

        public static string GiveItem(RunState r, int tier)
        {
            ItemDef pick = null;
            for (int t = tier; t >= 0 && pick == null; t--)
            {
                var pool = Content.Items.FindAll(i => i.tier == t && r.Count(i.id) < i.Cap);
                if (pool.Count > 0) pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            }
            if (pick == null) { r.gold += 30; return "Không còn bảo vật — nhận 30 linh thạch."; }
            r.items.Add(pick);
            float before = r.MaxHP;
            r.Recalc();
            if (r.MaxHP > before) r.hp += r.MaxHP - before;
            return "Nhận được <color=" + Content.TierHex(pick.tier) + ">" + pick.name + "</color>!";
        }

        public static readonly List<EventDef> All = new List<EventDef>
        {
            new EventDef { id="ma_dao", title="Ma Đạo Truyền Thừa", sprite=Res.Px.TaTu,
                text="Trong hang tối, một bộ hài cốt ôm cuốn ma công. Huyết khí cuồn cuộn mời gọi.",
                options=new[]{
                    new EventOption{ label="Tu luyện ma công", desc="+20% Sát Thương, -6 Sinh Lực", apply=r=>Perm(r, M(Stat.Damage,20), M(Stat.MaxHP,-6)) },
                    new EventOption{ label="Thiêu hủy ma công", desc="+10 Cơ Duyên", apply=r=>Perm(r, M(Stat.Luck,10)) },
                    Leave() } },
            new EventDef { id="linh_tuyen", title="Linh Tuyền", sprite=116,
                text="Một dòng suối linh khí trong vắt chảy giữa khe đá.",
                options=new[]{
                    new EventOption{ label="Tắm suối", desc="+8 Sinh Lực", apply=r=>Perm(r, M(Stat.MaxHP,8)) },
                    new EventOption{ label="Uống nước suối", desc="+4 Hồi Phục", apply=r=>Perm(r, M(Stat.Regen,4)) },
                    new EventOption{ label="Đóng bình mang theo", desc="+25 linh thạch", apply=r=>{ r.gold+=25; return "+25 linh thạch"; } } } },
            new EventDef { id="do_thach", title="Đổ Thạch", sprite=56,
                text="Lão thương nhân bày những khối nguyên thạch: \"Mở ra là gì, tùy cơ duyên của đạo hữu.\"",
                options=new[]{
                    new EventOption{ label="Mua khối lớn (35)", desc="55%: pháp bảo Huyền phẩm. Còn lại: đá vụn.", can=r=>r.gold>=35,
                        apply=r=>{ r.gold-=35; return UnityEngine.Random.value < 0.55f + r.stats[Stat.Luck]/400f ? GiveItem(r,2) : "Chỉ là đá vụn... Đạo tâm ngươi rung động."; } },
                    new EventOption{ label="Mua khối nhỏ (15)", desc="75%: pháp bảo Linh phẩm.", can=r=>r.gold>=15,
                        apply=r=>{ r.gold-=15; return UnityEngine.Random.value < 0.75f + r.stats[Stat.Luck]/400f ? GiveItem(r,1) : "Đá vụn. Lần sau may mắn hơn."; } },
                    Leave("Không đánh bạc") } },
            new EventDef { id="kiem_trung", title="Kiếm Trủng", sprite=106,
                text="Vạn thanh kiếm cắm trên núi hoang, kiếm ý ngút trời.",
                options=new[]{
                    new EventOption{ label="Rút một thanh kiếm", desc="Nhận vũ khí Kiếm phẩm Linh (cần ô trống)", can=r=>r.weapons.Count<r.maxWeapons,
                        apply=r=>{ var pool=Content.Weapons.FindAll(w=>w.HasTag(Tag.Kiem)); var w0=pool[UnityEngine.Random.Range(0,pool.Count)]; r.AddWeapon(w0,1); return "Nhận <color=#5BC0FF>"+w0.name+" (Linh)</color>!"; } },
                    new EventOption{ label="Lĩnh ngộ kiếm ý", desc="+12% Cận Chiến, +4% Bạo Kích", apply=r=>Perm(r, M(Stat.Melee,12), M(Stat.Crit,4)) },
                    Leave() } },
            new EventDef { id="tam_ma", title="Tâm Ma Kiếp", sprite=Res.Px.AnhSat,
                text="Bóng hình của chính ngươi hiện ra, cười lạnh: \"Ngươi dám đối mặt với ta?\"",
                options=new[]{
                    new EventOption{ label="Chém tâm ma", desc="+2 lần lên cấp, -4 Sinh Lực", apply=r=>{ for (int k=0;k<2;k++){ r.level++; r.pendingLevels++; Game.I.OnLevelUp(r.level); } Perm(r, M(Stat.MaxHP,-4)); return "Tâm cảnh thăng hoa: +2 cấp!"; } },
                    new EventOption{ label="Tĩnh tọa", desc="+2 Hộ Giáp, +5% Né Tránh", apply=r=>Perm(r, M(Stat.Armor,2), M(Stat.Dodge,5)) } } },
            new EventDef { id="tien_nhan", title="Tiên Nhân Chỉ Lộ", sprite=Res.Px.DanTu,
                text="Một lão tiên ông cưỡi hạc ghé xuống: \"Tiểu tử có căn cốt, lão phu chỉ cho một đường.\"",
                options=new[]{
                    new EventOption{ label="Thân pháp", desc="+15% Tốc Đánh", apply=r=>Perm(r, M(Stat.AtkSpeed,15)) },
                    new EventOption{ label="Nhãn lực", desc="+8% Bạo Kích, +20% ST Bạo Kích", apply=r=>Perm(r, M(Stat.Crit,8), M(Stat.CritDmg,20)) },
                    new EventOption{ label="Thần thức", desc="+20% Tầm Đánh, +30% Thu Linh", apply=r=>Perm(r, M(Stat.Range,20), M(Stat.Pickup,30)) } } },
            new EventDef { id="linh_thu", title="Linh Thú Trứng", sprite=82,
                text="Một quả trứng ấm áp nằm trong tổ chim bỏ hoang, linh khí dao động.",
                options=new[]{
                    new EventOption{ label="Ấp trứng (30)", desc="+10 Thu Hoạch, +15 Cơ Duyên", can=r=>r.gold>=30, apply=r=>{ r.gold-=30; return Perm(r, M(Stat.Harvest,10), M(Stat.Luck,15)); } },
                    new EventOption{ label="Ăn trứng", desc="+5 Sinh Lực, +2 Hồi Phục", apply=r=>Perm(r, M(Stat.MaxHP,5), M(Stat.Regen,2)) },
                    Leave() } },
            new EventDef { id="huyet_te", title="Huyết Tế Đàn", sprite=29,
                text="Tế đàn cổ đòi hỏi máu tươi. Đổi lại, nó ban phát sức mạnh.",
                options=new[]{
                    new EventOption{ label="Hiến máu", desc="-5 Sinh Lực, nhận pháp bảo Huyền phẩm", apply=r=>{ Perm(r, M(Stat.MaxHP,-5)); return GiveItem(r,2); } },
                    new EventOption{ label="Hiến linh thạch (40)", desc="Nhận pháp bảo Huyền phẩm", can=r=>r.gold>=40, apply=r=>{ r.gold-=40; return GiveItem(r,2); } },
                    Leave() } },
            new EventDef { id="luyen_dan", title="Lò Luyện Đan", sprite=74,
                text="Một lò đan còn ấm, bên trong vương lại vài viên đan dược chưa thành hình.",
                options=new[]{
                    new EventOption{ label="Nuốt đan dở", desc="50%: +10% Sát Thương. 50%: -3 Sinh Lực", apply=r=> UnityEngine.Random.value<0.5f ? Perm(r, M(Stat.Damage,10)) : Perm(r, M(Stat.MaxHP,-3)) },
                    new EventOption{ label="Luyện tiếp (20)", desc="+6% Sát Thương, +3 Sinh Lực", can=r=>r.gold>=20, apply=r=>{ r.gold-=20; return Perm(r, M(Stat.Damage,6), M(Stat.MaxHP,3)); } },
                    Leave() } },
            new EventDef { id="co_mo", title="Cổ Mộ", sprite=65,
                text="Một ngôi mộ cổ của tiên nhân. Cấm chế đã yếu, nhưng vẫn còn sát khí.",
                options=new[]{
                    new EventOption{ label="Mở quan tài", desc="50%: pháp bảo Tiên phẩm. 50%: -8 Sinh Lực",
                        apply=r=> UnityEngine.Random.value < 0.5f + r.stats[Stat.Luck]/500f ? GiveItem(r,3) : Perm(r, M(Stat.MaxHP,-8)) },
                    new EventOption{ label="Bái lạy rồi đi", desc="+8 Cơ Duyên", apply=r=>Perm(r, M(Stat.Luck,8)) } } },
            new EventDef { id="luan_dao", title="Luận Đạo", sprite=Res.Px.TheTu,
                text="Một lão tăng ngồi dưới gốc bồ đề, mỉm cười mời ngươi luận đạo.",
                options=new[]{
                    new EventOption{ label="Luận đạo ba ngày", desc="+1 cấp", apply=r=>GainLevel(r,1) },
                    new EventOption{ label="Xin chút lộ phí", desc="+30 linh thạch", apply=r=>{ r.gold+=30; return "+30 linh thạch"; } } } },
            new EventDef { id="thien_tai", title="Thiên Tài Địa Bảo", sprite=126,
                text="Một gốc linh thảo ngàn năm toả hương giữa vách núi.",
                options=new[]{
                    new EventOption{ label="Luyện hoá", desc="+12 Sinh Lực, -5% Thân Pháp", apply=r=>Perm(r, M(Stat.MaxHP,12), M(Stat.Speed,-5)) },
                    new EventOption{ label="Đem bán", desc="+45 linh thạch", apply=r=>{ r.gold+=45; return "+45 linh thạch"; } },
                    new EventOption{ label="Trồng lại", desc="+3 Hồi Phục, +5 Thu Hoạch", apply=r=>Perm(r, M(Stat.Regen,3), M(Stat.Harvest,5)) } } },
            new EventDef { id="linh_mach", title="Linh Mạch", sprite=56,
                text="Dưới chân núi, một mạch linh thạch lộ thiên toả ánh lam nhạt.",
                options=new[]{
                    new EventOption{ label="Hấp thu linh khí", desc="+1 cấp", apply=r=>GainLevel(r,1) },
                    new EventOption{ label="Khai thác", desc="+40 linh thạch", apply=r=>{ r.gold+=40; return "+40 linh thạch"; } } } },
            new EventDef { id="phuc_kich", title="Ma Tu Phục Kích", sprite=Res.Px.TaTu,
                text="Ba tà tu chặn đường: \"Để lại bảo vật, tha cho ngươi một mạng!\"",
                options=new[]{
                    new EventOption{ label="Nghênh chiến", desc="-6 Sinh Lực, đoạt 2 pháp bảo Linh phẩm",
                        apply=r=>{ Perm(r, M(Stat.MaxHP,-6)); return GiveItem(r,1) + " " + GiveItem(r,1); } },
                    new EventOption{ label="Nộp tiền mãi lộ (20)", desc="Mất 20 linh thạch", can=r=>r.gold>=20, apply=r=>{ r.gold-=20; return "Thoát thân an toàn."; } },
                    new EventOption{ label="Bỏ chạy", desc="-3 Sinh Lực", apply=r=>Perm(r, M(Stat.MaxHP,-3)) } } },
            new EventDef { id="than_binh_cac", title="Thần Binh Các", sprite=74,
                text="Lão thợ rèn mù gõ búa: \"Đưa ta một món binh khí, ta trả lại ngươi thần binh.\"",
                options=new[]{
                    new EventOption{ label="Rèn lại (30)", desc="Nâng 1 vũ khí ngẫu nhiên lên một phẩm", can=r=>r.gold>=30 && r.weapons.Exists(w=>w.tier<3),
                        apply=r=>{ var c=r.weapons.FindAll(w=>w.tier<3); var w0=c[UnityEngine.Random.Range(0,c.Count)]; r.gold-=30; w0.SetTier(w0.tier+1); r.Recalc(); r.AutoMerge();
                                   return w0.def.name+" lên phẩm <color="+Content.TierHex(w0.tier)+">"+Content.TierName[w0.tier]+"</color>!"; } },
                    new EventOption{ label="Học nghề", desc="+10% Tầm Đánh, +5% Bạo Kích", apply=r=>Perm(r, M(Stat.Range,10), M(Stat.Crit,5)) },
                    Leave() } },
            new EventDef { id="dao_lu", title="Song Tu Đạo Lữ", sprite=Res.Px.PhuTu,
                text="Một nữ tu mời ngươi cùng luận kiếm dưới trăng.",
                options=new[]{
                    new EventOption{ label="Luận kiếm", desc="+5% Né Tránh, +6% Tốc Đánh", apply=r=>Perm(r, M(Stat.Dodge,5), M(Stat.AtkSpeed,6)) },
                    new EventOption{ label="Cùng tu luyện", desc="+10 Sinh Lực", apply=r=>Perm(r, M(Stat.MaxHP,10)) } } },
        };

        static string GainLevel(RunState r, int n)
        {
            for (int k = 0; k < n; k++) { r.level++; r.pendingLevels++; Game.I.OnLevelUp(r.level); }
            return "+" + n + " cấp!";
        }

        public static EventDef Pick(RunState r)
        {
            var pool = All.FindAll(e => !r.seenEvents.Contains(e.id));
            if (pool.Count == 0) pool = All;
            var ev = pool[UnityEngine.Random.Range(0, pool.Count)];
            r.seenEvents.Add(ev.id);
            return ev;
        }
    }
}
