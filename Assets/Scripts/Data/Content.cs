using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    /// <summary>All balance data lives here.</summary>
    public static class SkillNum
    {
        public const float BossMul = 0.5f, MinCd = 3f;
        // stage I (spec section 4)
        public const int KiemSwords = 12, KiemPierce = 2;
        public const float KiemHit = 0.25f, KiemHomeRange = 7f;
        public const float DanR = 3.5f, DanHit = 2f, DanHeal = 0.25f;
        public const float TheR = 3.2f, TheHit = 2f, TheInvuln = 2f;
        public const float PhuR = 5.5f, PhuHit = 0.8f, PhuFreeze = 2.5f;
        public const float MaCost = 0.15f, MaRite = 5f;
        public const int LoiBolts = 8;
        public const float LoiRange = 10f, LoiHit = 1.4f, LoiStun = 1f;
        // Kiếm Tu II-IV
        public const float KiemReturnHit = 0.15f, KiemOrbitHit = 0.08f, KiemOrbitTime = 2f, KiemOrbitR = 1.4f, KiemOrbitRehit = 0.5f;
        public const int KiemSwords3 = 16;
        public const float KiemGiantHit = 1.25f, KiemGiantR = 3f, KiemGiantRange = 9f, KiemGiantDelay = 0.35f;
        // Đan Tu II-IV
        public const int DanPills = 4;
        public const float DanPillR = 1.2f, DanPillHit = 1f, DanPillRange = 9f, DanR3 = 4.2f, DanGuardMul = 0.7f, DanGuardTime = 3f, DanFollowHit = 1.2f, DanFollowR = 4.2f;
        // Thể Tu II-IV
        public const float TheR2 = 3.8f, TheStun = 1f, TheFissureLen = 6f, TheFissureW = 0.9f, TheFissureHit = 1.2f, TheFissureTime = 0.3f;
        public const float TheInvuln4 = 3f, TheAfterR = 5f, TheAfterHit = 3f;
        // Phù Tu II-IV
        public const float PhuBonus = 1.3f, PhuBonus4 = 1.5f, PhuR4 = 8f, PhuFlyHit = 0.5f, PhuFlyFreeze = 2f, PhuFlyRange = 12f, PhuBurstR = 1.2f, PhuBurstHit = 0.8f;
        public const int PhuFlyers = 6;
        // Ma Tu II-IV
        public const float MaNovaR = 3f, MaNovaHit = 1.8f, MaNovaR4 = 4.5f, MaNovaHit4 = 3f, MaGuardTime = 1.5f, MaGuardMul = 0.6f;
        public const float MaExtend = 0.2f, MaExtendMax = 3f, MaLifesteal = 25f, MaLifesteal4 = 40f;
        // Lôi Tu II-IV
        public const int LoiChainHops = 2, LoiBolts4 = 12;
        public const float LoiChainR = 3.8f, LoiChainHit = 0.5f, LoiChainDecay = 0.85f;
        public const float LoiFieldR = 2.5f, LoiFieldTime = 4f, LoiFieldTick = 0.4f, LoiFieldHit = 0.5f, LoiFieldStun = 0.3f;
        public const float LoiBigHit = 3f, LoiBigDelay = 0.4f, LoiBigStun = 1.5f, LoiBigRange = 12f;
    }

    public static class Content
    {
        /// <summary>Skill power ratio vs. the average trash HP: 1.5 at wave 1 falling to 0.15 at wave 20 (balance pass 2026-10-27, docs/balance).</summary>
        public static float SkillR(int w) => 1.5f - 1.35f * (Mathf.Clamp(w, 1, 20) - 1) / 19f;

        public static readonly float[] TierMul = { 1f, 1.7f, 2.8f, 4.5f };
        public static readonly float[] TierCd = { 1f, 0.92f, 0.85f, 0.78f };
        public static readonly int[] TierPrice = { 1, 2, 4, 7 };
        /// <summary>Copies of one item a run may hold, by tier (Phàm/Linh/Huyền/Tiên); an item's own max overrides it.</summary>
        public static readonly int[] StackCap = { 6, 4, 3, 2 };
        public static readonly string[] TierName = { "Phàm", "Linh", "Huyền", "Tiên" };
        public static readonly Color[] TierColor = {
            new Color(0.92f, 0.92f, 0.92f), new Color(0.36f, 0.75f, 1f), new Color(0.78f, 0.49f, 1f), new Color(1f, 0.66f, 0.2f) };
        public static string TierHex(int t) => "#" + ColorUtility.ToHtmlStringRGB(TierColor[Mathf.Clamp(t, 0, 3)]);

        public static readonly string[] TagName = { "Kiếm", "Thể", "Hỏa", "Lôi", "Phù", "Độc" };
        public static readonly Color[] TagColor = {
            new Color(0.75f, 0.88f, 1f), new Color(0.95f, 0.72f, 0.45f), new Color(1f, 0.45f, 0.25f),
            new Color(0.55f, 0.7f, 1f), new Color(1f, 0.9f, 0.45f), new Color(0.55f, 0.95f, 0.4f) };
        public static string TagHex(Tag t) => "#" + ColorUtility.ToHtmlStringRGB(TagColor[(int)t]);

        public static readonly string[] RealmName = { "Luyện Khí", "Trúc Cơ", "Kim Đan", "Nguyên Anh", "Hóa Thần", "Luyện Hư" };
        /// <summary>Swap weapon/treasure icons for the PixelLab redraws (Resources/PixelLab/Icons) where they exist.</summary>
        public static void ApplyIcons()
        {
            for (int i = 0; i < Weapons.Count; i++)
                if (Res.RegisterPixelLab(400 + i, "Icons/w_" + Weapons[i].id, Weapons[i].sprite)) { Weapons[i].sprite = 400 + i; Weapons[i].tint = Color.white; }
            for (int i = 0; i < Items.Count; i++)
                if (Res.RegisterPixelLab(500 + i, "Icons/i_" + Items[i].id, Items[i].sprite)) { Items[i].sprite = 500 + i; Items[i].tint = Color.white; }
        }

        /// <summary>Uy Áp Cảnh Giới (spec section 6): reward added to levelStats at the Trúc Cơ (10) and Kim Đan (20) breakthroughs; Nguyên Anh (30) gives a once-per-wave guard instead.</summary>
        public static readonly Mod[][] UyAp =
        {
            new[] { new Mod(Stat.MaxHP, 10f), new Mod(Stat.Armor, 1f) },
            new[] { new Mod(Stat.Damage, 10f), new Mod(Stat.SkillPower, 10f) },
        };

        // ---------------------------------------------------------------- Ngộ Đạo cards (spec section 5.2-5.3)
        static SkillCardDef Card(string id, string name, string charId, string desc, int needTag = -1) =>
            new SkillCardDef { id = id, name = name, charId = charId, desc = desc, needTag = needTag };

        public static readonly List<SkillCardDef> SkillCards = new List<SkillCardDef>
        {
            Card("bach_kiem", "Bách Kiếm Triều", "kiem_tu", "+8 kiếm; mọi kiếm ×0.7 sát thương."),
            Card("kiem_tam", "Kiếm Tâm Thông Minh", "kiem_tu", "Kiếm ưu tiên tinh anh và boss; mục tiêu bị nhiều kiếm chém thì mỗi kiếm sau +15% (tối đa +150%)."),
            Card("kiem_khi", "Kiếm Khí Hộ Thể", "kiem_tu", "Mỗi kiếm của chiêu trúng quái tích 1 tầng (tối đa 12, xoá đầu đợt); đủ 12 tầng thì chặn hẳn đòn kế tiếp."),
            Card("cuu_chuyen", "Cửu Chuyển Hoàn Đan", "dan_tu", "Vụ nổ chính hồi thêm 2% máu tối đa cho mỗi quái trúng (tối đa +20%)."),
            Card("bao_viem", "Bạo Viêm Đan", "dan_tu", "Bán kính nổ +0.15 cho mỗi quái trong vùng lúc thi triển (tối đa +1.5); đòn chiêu +50% lên quái đang cháy."),
            Card("ho_menh", "Hộ Mệnh Đan", "dan_tu", "Máu xuống dưới 30% mà chiêu đã sẵn sàng thì tự thi triển (1 lần mỗi đợt)."),
            Card("bat_dong", "Bất Động Minh Vương", "the_tu", "Trong lúc bất tử do chiêu, quái chạm người chịu ×0.5 sát thương và bị hất lùi."),
            Card("thiet_bo_sam", "Thiết Bố Sam", "the_tu", "Thi triển xong +5 Hộ Giáp trong 5 giây."),
            Card("son_bang", "Sơn Băng Địa Hãm", "the_tu", "Trước sóng chấn 0.25 giây, hút quái trong 5 ô về phía người."),
            Card("han_bang", "Hàn Băng Phù", "phu_tu", "Đóng băng của chiêu lâu thêm 1.5 giây."),
            Card("ho_than", "Hộ Thân Phù", "phu_tu", "Thi triển xong có 3 lá bùa quay quanh người 8 giây, mỗi lá chặn 1 viên đạn địch."),
            Card("truy_hon", "Truy Hồn Phù", "phu_tu", "Quái đang bị chiêu đóng băng mà chết thì bắn 1 lá bùa đuổi quái gần nhất, ×0.6, đóng băng 1 giây."),
            Card("huyet_hai", "Huyết Hải Vô Biên", "ma_tu", "Hiến tế kéo dài 8 giây nhưng mất 25% máu hiện tại."),
            Card("thi_huyet", "Thị Huyết", "ma_tu", "Trong lúc hiến tế, mỗi mạng diệt hồi 1% máu tối đa."),
            Card("huyet_anh", "Huyết Ảnh Phân Thân", "ma_tu", "Trong lúc hiến tế có bóng máu đi theo, mỗi giây lướt một đường 5 ô về phía quái gần nhất (×0.8)."),
            Card("loi_dinh", "Lôi Đình Vạn Quân", "loi_tu", "Bớt 3 tia sét; mỗi tia ×2 sát thương và gây choáng 2 giây."),
            Card("loi_linh", "Lôi Linh Hộ Thể", "loi_tu", "Thi triển xong 4 giây có vòng điện 1.6 ô quanh người, mỗi 0.3 giây giật quái bên trong (×0.3)."),
            Card("tich_loi", "Tích Lôi", "loi_tu", "Mỗi đòn bạo kích của vũ khí giảm 0.3 giây hồi chiêu (tối đa 4 giây mỗi vòng hồi)."),
            Card("song_thi", "Song Thi", null, "0.6 giây sau, chiêu tự lặp lại phần sát thương và hồi máu ở 50%."),
            Card("tu_khi", "Tụ Khí", null, "Mỗi mạng diệt giảm 0.15 giây hồi chiêu (tối đa 3 giây mỗi vòng hồi)."),
            Card("du_ba", "Dư Ba", null, "Thi triển xong, vũ khí +35% Tốc Đánh trong 4 giây."),
            Card("than_nhan", "Thần Nhãn", null, "Đòn chiêu +15% Bạo Kích, +50% Sát Thương Bạo Kích."),
            Card("hoa_an", "Hỏa Đạo Ấn", null, "Cần 2 vũ khí Đạo Hỏa. Đòn chiêu thiêu đốt (30% đòn mỗi giây).", (int)Tag.Hoa),
            Card("loi_an", "Lôi Đạo Ấn", null, "Cần 2 vũ khí Đạo Lôi. Mỗi đòn chiêu nảy sang 1 quái trong 3.5 ô với 50% đòn.", (int)Tag.Loi),
            Card("doc_an", "Độc Đạo Ấn", null, "Cần 2 vũ khí Đạo Độc. Đòn chiêu gây độc; quái chết vì độc này lây cho tối đa 3 quái.", (int)Tag.Doc),
            Card("phu_an", "Phù Đạo Ấn", null, "Cần 2 vũ khí Đạo Phù. Đòn chiêu có 25% đóng băng 1 giây.", (int)Tag.Phu),
        };
        public static SkillCardDef SkillCard(string id) => SkillCards.Find(c => c.id == id);

        public static readonly string[] SkillItemIds = { "ngung_than_huong", "thanh_tam_phu", "than_thong_ngoc_gian", "tinh_toa_bo_doan", "linh_lung_bao_thap", "thien_thu_vo_tu" };

        public static string Realm(int level)
        {
            int r = Mathf.Min(level / 10, RealmName.Length - 1);
            int layer = r == 0 ? level : level - r * 10 + 1;
            return Loc.Lang == Loc.ZH ? Loc.Tr(RealmName[r]) + " " + layer + "层" : RealmName[r] + " tầng " + layer;   // Chinese puts the layer number first
        }
        public static string RealmOnly(int level) => RealmName[Mathf.Min(level / 10, RealmName.Length - 1)];

        public static float XpNeed(int level) => Mathf.Round(Mathf.Pow(level + 3, 2) * 1.25f + level * 2);

        // ---------------------------------------------------------------- weapons
        public static readonly List<WeaponDef> Weapons = new List<WeaponDef>
        {
            new WeaponDef { id="thiet_kiem", name="Thiết Kiếm", sprite=104, type=WType.Thrust, tags=new[]{Tag.Kiem},
                dmg=9, cd=0.85f, range=2.7f, pierce=1, knock=1f, price=14, desc="Đâm thẳng, xuyên 2 kẻ địch." },
            new WeaponDef { id="doan_dao", name="Đoản Đao", sprite=103, type=WType.Thrust, tags=new[]{Tag.Kiem},
                dmg=5, cd=0.42f, range=2.1f, crit=15, knock=0.5f, price=12, desc="Cực nhanh. +15% bạo kích riêng." },
            new WeaponDef { id="truong_thuong", name="Trường Thương", sprite=131, type=WType.Thrust, tags=new[]{Tag.Kiem, Tag.The},
                dmg=11, cd=1.1f, range=3.8f, pierce=99, knock=1.4f, price=18, desc="Tầm rất xa, xuyên mọi kẻ địch trên đường." },
            new WeaponDef { id="cu_kiem", name="Cự Kiếm", sprite=106, type=WType.Swing, tags=new[]{Tag.Kiem, Tag.The},
                dmg=20, cd=1.45f, range=2.5f, splash=1.55f, knock=1.6f, price=24, desc="Bổ xuống tạo sóng chấn vùng rộng." },
            new WeaponDef { id="thiet_chuy", name="Thiết Chùy", sprite=117, type=WType.Swing, tags=new[]{Tag.The},
                dmg=16, cd=1.25f, range=2.3f, splash=1.35f, knock=2.4f, price=18, desc="Đập vùng, đẩy lùi cực mạnh." },
            new WeaponDef { id="song_phu", name="Song Phủ", sprite=118, type=WType.Swing, tags=new[]{Tag.The},
                dmg=12, cd=0.95f, range=2.1f, splash=1.15f, crit=5, knock=1.2f, price=20, desc="Chém vùng nhanh." },
            new WeaponDef { id="hoa_truong", name="Hỏa Trượng", sprite=129, type=WType.Bolt, tags=new[]{Tag.Hoa},
                dmg=8, cd=0.95f, range=7f, splash=1.1f, burn=0.6f, speed=9f, price=18, look=ProjLook.Glow,
                fx=new Color(1f,0.5f,0.15f), desc="Cầu lửa nổ vùng và thiêu đốt." },
            new WeaponDef { id="loi_truong", name="Lôi Trượng", sprite=130, type=WType.Chain, tags=new[]{Tag.Loi},
                dmg=7, cd=1.0f, range=6.5f, chains=3, price=20, fx=new Color(0.6f,0.8f,1f), desc="Sét nảy qua 4 kẻ địch." },
            new WeaponDef { id="phu_luc", name="Phù Lục", sprite=Res.TalismanId, type=WType.Bolt, tags=new[]{Tag.Phu},
                dmg=6, cd=0.55f, range=7.5f, pierce=1, speed=12f, price=14, look=ProjLook.Spin,
                fx=new Color(1f,0.9f,0.5f), desc="Bắn bùa nhanh, xuyên 2 kẻ địch." },
            new WeaponDef { id="phi_kiem", name="Phi Kiếm", sprite=104, tint=new Color(0.6f,1f,1f), type=WType.Fly, tags=new[]{Tag.Kiem, Tag.Phu},
                dmg=10, cd=1.1f, range=8f, pierce=3, speed=11f, price=22, look=ProjLook.Point,
                fx=new Color(0.5f,1f,1f), desc="Kiếm bay tự tìm địch, xuyên 4 kẻ." },
            new WeaponDef { id="doc_dao", name="Độc Đao", sprite=105, tint=new Color(0.7f,1f,0.6f), type=WType.Thrust, tags=new[]{Tag.Doc, Tag.Kiem},
                dmg=7, cd=0.7f, range=2.5f, poison=1.0f, price=15, desc="Đâm gây trúng độc." },
            new WeaponDef { id="doc_binh", name="Độc Bình", sprite=114, type=WType.Bolt, tags=new[]{Tag.Doc},
                dmg=5, cd=1.2f, range=6f, splash=1.6f, poison=1.3f, speed=7f, price=17, look=ProjLook.Spin,
                fx=new Color(0.45f,1f,0.35f), desc="Ném bình độc, vỡ ra vùng độc." },
            new WeaponDef { id="loi_phu", name="Lôi Phù", sprite=Res.TalismanId, tint=new Color(0.7f,0.85f,1f), type=WType.Chain, tags=new[]{Tag.Loi, Tag.Phu},
                dmg=5, cd=0.75f, range=6f, chains=2, price=16, fx=new Color(0.7f,0.85f,1f), desc="Bùa sét nhanh, nảy 3 kẻ." },
            new WeaponDef { id="hoa_phu", name="Hỏa Phù", sprite=Res.TalismanId, tint=new Color(1f,0.7f,0.6f), type=WType.Bolt, tags=new[]{Tag.Hoa, Tag.Phu},
                dmg=6, cd=0.8f, range=7f, splash=0.8f, burn=0.4f, speed=10f, price=16, look=ProjLook.Glow,
                fx=new Color(1f,0.35f,0.2f), desc="Bùa lửa nổ nhỏ, thiêu đốt." },
            new WeaponDef { id="bang_phach_cham", name="Băng Phách Châm", sprite=103, tint=new Color(0.6f,0.9f,1f), type=WType.Bolt, tags=new[]{Tag.Phu},
                dmg=4, cd=0.38f, range=6.5f, speed=15f, freeze=0.2f, price=16, look=ProjLook.Point,
                fx=new Color(0.6f,0.9f,1f), desc="Kim băng cực nhanh, 20% đóng băng 1.2s." },
            new WeaponDef { id="moc_kiem", name="Mộc Kiếm", sprite=107, type=WType.Thrust, tags=new[]{Tag.Kiem},
                dmg=4, cd=0.32f, range=2.0f, knock=0.4f, price=10, desc="Kiếm gỗ rẻ, đâm liên tục." },
            new WeaponDef { id="khai_son_phu", name="Khai Sơn Phủ", sprite=119, tint=new Color(1f,0.9f,0.8f), type=WType.Swing, tags=new[]{Tag.The},
                dmg=26, cd=1.8f, range=2.4f, splash=1.9f, knock=3f, price=28, desc="Rìu bổ núi: chậm, vùng cực rộng." },
            new WeaponDef { id="han_bang_binh", name="Hàn Băng Bình", sprite=113, tint=new Color(0.7f,0.95f,1f), type=WType.Bolt, tags=new[]{Tag.Phu, Tag.Doc},
                dmg=5, cd=1.15f, range=6f, splash=1.5f, freeze=0.5f, speed=7f, price=18, look=ProjLook.Spin,
                fx=new Color(0.6f,0.95f,1f), desc="Bình băng vỡ thành vùng, 50% đóng băng." },
            new WeaponDef { id="huyet_nguyet_dao", name="Huyết Nguyệt Đao", sprite=105, tint=new Color(1f,0.55f,0.55f), type=WType.Thrust, tags=new[]{Tag.Kiem},
                dmg=7, cd=0.65f, range=2.4f, crit=25, price=20, desc="Đao máu: +25% bạo kích riêng." },
            new WeaponDef { id="loi_chuy", name="Lôi Chùy", sprite=117, tint=new Color(0.65f,0.8f,1f), type=WType.Swing, tags=new[]{Tag.The, Tag.Loi},
                dmg=14, cd=1.3f, range=2.3f, splash=1.2f, chains=2, knock=1.6f, price=22, fx=new Color(0.6f,0.8f,1f),
                desc="Đập vùng rồi phóng sét nảy 3 kẻ." },
            new WeaponDef { id="hoa_long_tien", name="Hỏa Long Tiễn", sprite=131, tint=new Color(1f,0.6f,0.35f), type=WType.Bolt, tags=new[]{Tag.Hoa},
                dmg=12, cd=1.25f, range=8.5f, pierce=2, burn=0.5f, speed=15f, price=20, look=ProjLook.Point,
                fx=new Color(1f,0.5f,0.2f), desc="Tên lửa xuyên 3 kẻ, thiêu đốt." },
            new WeaponDef { id="viem_duong_chuy", name="Viêm Dương Chùy", sprite=119, tint=new Color(1f,0.55f,0.4f), type=WType.Swing, tags=new[]{Tag.Hoa, Tag.The},
                dmg=13, cd=1.15f, range=2.3f, splash=1.3f, burn=0.7f, knock=1.5f, price=22, fx=new Color(1f,0.5f,0.2f),
                desc="Búa lửa đập vùng, thiêu mọi kẻ trúng đòn." },
            new WeaponDef { id="loi_an", name="Lôi Ấn", sprite=101, tint=new Color(0.6f,0.8f,1f), type=WType.Chain, tags=new[]{Tag.Loi},
                dmg=11, cd=1.4f, range=7f, chains=5, price=24, fx=new Color(0.7f,0.85f,1f), desc="Ấn sét nảy qua 6 kẻ địch." },
            new WeaponDef { id="loi_hoa_chau", name="Lôi Hỏa Châu", sprite=102, tint=new Color(0.85f,0.65f,1f), type=WType.Bolt, tags=new[]{Tag.Hoa, Tag.Loi},
                dmg=9, cd=1.0f, range=7f, splash=1.2f, burn=0.4f, speed=9f, price=22, look=ProjLook.Glow,
                fx=new Color(0.8f,0.5f,1f), desc="Châu lôi hỏa nổ vùng, thiêu đốt." },
        };

        // ---------------------------------------------------------------- items
        static Mod M(Stat s, float v) => new Mod(s, v);

        public static readonly List<ItemDef> Items = new List<ItemDef>
        {
            // Phàm
            new ItemDef { id="hoi_khi_dan", name="Hồi Khí Đan", sprite=115, tier=0, price=13, mods=new[]{M(Stat.MaxHP,6)} },
            new ItemDef { id="hoi_xuan_dan", name="Hồi Xuân Đan", sprite=126, tier=0, price=13, mods=new[]{M(Stat.Regen,2)} },
            new ItemDef { id="cuong_bao_dan", name="Cuồng Bạo Đan", sprite=127, tier=0, price=15, mods=new[]{M(Stat.Damage,6)} },
            new ItemDef { id="phong_hanh_phu", name="Phong Hành Phù", sprite=Res.TalismanId, tint=new Color(0.75f,1f,0.8f), tier=0, price=12, mods=new[]{M(Stat.Speed,6)} },
            new ItemDef { id="thiet_giap", name="Thiết Giáp", sprite=101, tier=0, price=15, mods=new[]{M(Stat.Armor,2)} },
            new ItemDef { id="tu_linh_ban", name="Tụ Linh Bàn", sprite=56, tier=0, price=12, mods=new[]{M(Stat.Pickup,30), M(Stat.Harvest,3)} },
            new ItemDef { id="kiem_pho", name="Kiếm Phổ", sprite=66, tint=new Color(0.7f,0.85f,1f), tier=0, price=14, mods=new[]{M(Stat.Melee,9)} },
            new ItemDef { id="phap_quyet", name="Pháp Quyết", sprite=66, tint=new Color(0.85f,0.6f,1f), tier=0, price=14, mods=new[]{M(Stat.Spell,9)} },
            new ItemDef { id="ung_nhan", name="Ưng Nhãn", sprite=102, tint=new Color(1f,0.9f,0.6f), tier=0, price=15, mods=new[]{M(Stat.Crit,4)} },
            new ItemDef { id="tho_van_phu", name="Thổ Vận Phù", sprite=64, tier=0, price=12, mods=new[]{M(Stat.Luck,10)} },
            new ItemDef { id="khinh_kiem_quyet", name="Khinh Kiếm Quyết", sprite=125, tier=0, price=15, mods=new[]{M(Stat.AtkSpeed,6)} },
            // Linh
            new ItemDef { id="huyet_long_huyet", name="Huyết Long Huyết", sprite=115, tint=new Color(1f,0.5f,0.5f), tier=1, price=30, max=4,
                mods=new[]{M(Stat.MaxHP,12), M(Stat.Regen,2), M(Stat.Speed,-3)} },
            new ItemDef { id="kim_cang_than", name="Kim Cang Thân", sprite=102, tier=1, price=32, max=3, mods=new[]{M(Stat.Armor,5), M(Stat.Speed,-4)} },
            new ItemDef { id="thiet_quyen", name="Thiết Quyền", sprite=74, tier=1, price=30, mods=new[]{M(Stat.Melee,16), M(Stat.AtkSpeed,-3)} },
            new ItemDef { id="bat_quai", name="Bát Quái Kính", sprite=56, tint=new Color(1f,0.85f,0.4f), tier=1, price=36, max=1,
                mods=new[]{M(Stat.Crit,3)}, desc="Mỗi đòn thứ 8 của mỗi vũ khí chắc chắn bạo kích." },
            new ItemDef { id="ma_huyet_chau", name="Ma Huyết Châu", sprite=127, tint=new Color(0.9f,0.3f,0.5f), tier=1, price=30, max=3,
                mods=new[]{M(Stat.Lifesteal,6), M(Stat.Regen,-2)} },
            new ItemDef { id="tu_bao_bon", name="Tụ Bảo Bồn", sprite=89, tier=1, price=35, max=1,
                desc="Cuối mỗi đợt nhận lãi 10% linh thạch đang có (tối đa 20)." },
            new ItemDef { id="linh_nhan_chau", name="Linh Nhãn Châu", sprite=102, tint=new Color(0.6f,1f,1f), tier=1, price=30,
                mods=new[]{M(Stat.Range,15), M(Stat.Crit,3)} },
            new ItemDef { id="phong_loi_hai", name="Phong Lôi Hài", sprite=128, tint=new Color(0.7f,1f,0.8f), tier=1, price=32, max=1,
                mods=new[]{M(Stat.Speed,10), M(Stat.Dodge,5)}, desc="Hồi chiêu lướt -35%." },
            new ItemDef { id="the_hon_phu", name="Thế Hồn Phù", sprite=65, tier=1, price=28, max=3, mods=new[]{M(Stat.Dodge,8), M(Stat.MaxHP,-3)} },
            // Huyền
            new ItemDef { id="loi_linh_chau", name="Lôi Linh Châu", sprite=130, tier=2, price=55, max=1,
                desc="Bạo kích gọi sét đánh 2 kẻ gần đó (60% sát thương)." },
            new ItemDef { id="hoa_long_chau", name="Hỏa Long Châu", sprite=129, tier=2, price=55, max=1,
                desc="Kẻ địch chết phát nổ, gây 50% sát thương đòn kết liễu xung quanh." },
            new ItemDef { id="song_tu_bi_kip", name="Song Tu Bí Kíp", sprite=66, tint=new Color(1f,0.8f,0.3f), tier=2, price=60, max=2,
                desc="+1 ô vũ khí." },
            new ItemDef { id="huyet_kiem_tam", name="Huyết Kiếm Tâm", sprite=107, tier=2, price=50, max=1,
                desc="Mỗi 25 kẻ địch bị giết: +1% Sát Thương vĩnh viễn trong lượt này." },
            new ItemDef { id="cuong_chien_y", name="Cuồng Chiến Ý", sprite=110, tier=2, price=50, max=1,
                desc="Mỗi 2% máu đã mất: +1% sát thương." },
            new ItemDef { id="tham_lam_ho_lo", name="Tham Lam Hồ Lô", sprite=82, tier=2, price=45, max=2,
                mods=new[]{M(Stat.Harvest,15), M(Stat.Luck,15), M(Stat.MaxHP,-6)} },
            // Joker (cộng hưởng hệ thống)
            new ItemDef { id="tich_linh_ho_lo", name="Tích Linh Hồ Lô", sprite=82, tint=new Color(1f,0.85f,0.4f), tier=1, price=35, max=1,
                desc="+2% sát thương cho mỗi đợt đã vượt qua." },
            new ItemDef { id="sat_luc_an", name="Sát Lục Ấn", sprite=64, tint=new Color(1f,0.45f,0.45f), tier=1, price=32, max=1,
                desc="Khi Liên Sát ≥ 30: +25% Tốc Đánh." },
            new ItemDef { id="kim_thiem", name="Kim Thiềm", sprite=108, tint=new Color(1f,0.85f,0.3f), tier=2, price=50, max=1,
                desc="+1% sát thương cho mỗi 10 linh thạch đang giữ (tối đa +40%)." },
            new ItemDef { id="phan_than_phu", name="Phân Thân Phù", sprite=Res.TalismanId, tint=new Color(1f,0.6f,0.9f), tier=2, price=55, max=1,
                desc="Vũ khí bắn đạn/phi kiếm: 30% bắn thêm 1 viên." },
            new ItemDef { id="pha_quan_kich", name="Phá Quân Kích", sprite=118, tint=new Color(1f,0.5f,0.4f), tier=2, price=50, max=1,
                desc="+40% sát thương lên kẻ địch đang bị thiêu đốt, trúng độc hoặc đóng băng." },
            new ItemDef { id="thon_thien", name="Thôn Thiên Ma Công", sprite=111, tint=new Color(0.8f,0.5f,1f), tier=3, price=85, max=1,
                desc="Hạ tinh anh/boss: +3 Sinh Lực vĩnh viễn (trong lượt)." },
            // Tiên
            new ItemDef { id="bat_tu_phu", name="Bất Tử Phù", sprite=65, tint=new Color(1f,0.85f,0.4f), tier=3, price=80, max=1,
                desc="Khi chết: hồi sinh với 50% máu (1 lần)." },
            new ItemDef { id="ngu_hanh_ban", name="Ngũ Hành Bàn", sprite=90, tier=3, price=75, max=1,
                desc="Nếu sở hữu từ 5 Đạo khác nhau: sát thương ×1.5." },
            new ItemDef { id="van_kiem_quy_tong", name="Vạn Kiếm Quy Tông", sprite=106, tint=new Color(1f,0.85f,0.4f), tier=3, price=80, max=1,
                desc="+12% sát thương cho mỗi vũ khí Đạo Kiếm đang có." },
            new ItemDef { id="thien_dao_chi_tam", name="Thiên Đạo Chi Tâm", sprite=29, tier=3, price=100, max=1,
                mods=new[]{M(Stat.Damage,20), M(Stat.AtkSpeed,10), M(Stat.Crit,8), M(Stat.MaxHP,15)} },
            // Thần Thông items (spec section 7); icons are placeholders until the PixelLab redraw (tools/icons.json)
            new ItemDef { id="ngung_than_huong", name="Ngưng Thần Hương", sprite=126, tint=new Color(0.85f,0.7f,1f), tier=0, price=13, mods=new[]{M(Stat.SkillPower,15)} },
            new ItemDef { id="thanh_tam_phu", name="Thanh Tâm Phù", sprite=Res.TalismanId, tint=new Color(0.7f,0.9f,1f), tier=0, price=12, mods=new[]{M(Stat.SkillHaste,5)} },
            new ItemDef { id="than_thong_ngoc_gian", name="Thần Thông Ngọc Giản", sprite=66, tint=new Color(0.6f,1f,0.75f), tier=1, price=30, mods=new[]{M(Stat.SkillPower,30), M(Stat.AtkSpeed,-5)} },
            new ItemDef { id="tinh_toa_bo_doan", name="Tĩnh Tọa Bồ Đoàn", sprite=56, tint=new Color(1f,0.7f,0.4f), tier=1, price=32, max=1, mods=new[]{M(Stat.SkillHaste,12)},
                desc="Mỗi lần thi triển Thần Thông hồi 3% máu tối đa." },
            new ItemDef { id="linh_lung_bao_thap", name="Linh Lung Bảo Tháp", sprite=90, tint=new Color(1f,0.9f,0.5f), tier=2, price=52, max=1,
                desc="Mỗi lần thi triển Thần Thông +4% Thần Lực đến hết đợt (tối đa +40%)." },
            new ItemDef { id="thien_thu_vo_tu", name="Thiên Thư Vô Tự", sprite=66, tint=new Color(1f,0.95f,0.8f), tier=3, price=85, max=1,
                desc="Sát thương Thần Thông ×1.5; sát thương vũ khí ×0.85." },
        };

        // ---------------------------------------------------------------- routes (Lộ Tuyến)
        public static readonly string[] RouteName = { "Đường Bằng", "Ma Triều", "Tinh Anh Tụ Tập", "Huyết Nguyệt" };
        public static readonly string[] RouteDesc = { "Bình thường",
            "+40% quái · mỗi mạng 40% rơi thêm linh khí", "Tinh anh ×4 (mỗi con rơi Bảo Rương)", "Quái nhanh +25% · cuối đợt nhận 1 Bảo Rương" };

        // ---------------------------------------------------------------- enemies
        public static readonly List<EnemyDef> Enemies = new List<EnemyDef>
        {
            new EnemyDef { id="huyet_thu", name="Huyết Thử", sprite=Res.Px.HuyetThu, hp=4, speed=2.5f, dmg=2, beh=EBeh.Chase, orbs=1, minWave=1, weight=10 },
            new EnemyDef { id="ma_buc", name="Ma Bức", sprite=Res.Px.MaBuc, hp=3, speed=3.5f, dmg=1, beh=EBeh.Swarm, orbs=1, minWave=2, weight=7 },
            new EnemyDef { id="luc_doc_trung", name="Lục Độc Trùng", sprite=Res.Px.DocTrung, hp=10, speed=1.8f, dmg=2, beh=EBeh.Splitter, orbs=2, minWave=3, weight=5,
                blood=new Color(0.3f,0.9f,0.5f) },
            new EnemyDef { id="doc_chu", name="Độc Chu", sprite=Res.Px.DocChu, hp=8, speed=2.2f, dmg=3, beh=EBeh.Charger, orbs=2, minWave=4, weight=5 },
            new EnemyDef { id="oan_hon", name="Oan Hồn", sprite=Res.Px.OanHon, hp=9, speed=2.0f, dmg=2, beh=EBeh.Shooter, orbs=2, minWave=5, weight=4,
                blood=new Color(0.7f,0.8f,1f) },
            new EnemyDef { id="thiet_boi_thu", name="Thiết Bối Thử", sprite=Res.Px.ThietBoiThu, hp=20, speed=2.3f, dmg=3, beh=EBeh.Chase, orbs=2, minWave=7, weight=5 },
            new EnemyDef { id="ta_tu", name="Tà Tu", sprite=Res.Px.TaTu, hp=16, speed=1.8f, dmg=3, beh=EBeh.Shooter, orbs=3, minWave=9, weight=3 },
            new EnemyDef { id="doc_nhan_ma", name="Độc Nhãn Ma", sprite=Res.Px.DocNhanMa, hp=65, speed=1.5f, dmg=5, scale=1.45f, beh=EBeh.Chase, orbs=8, minWave=8, weight=1.0f, elite=true },
            new EnemyDef { id="bao_hon", name="Bạo Hồn", sprite=Res.Px.BaoHon, hp=12, speed=2.9f, dmg=4, beh=EBeh.Bomber, orbs=2, minWave=6, weight=3, blood=new Color(1f,0.55f,0.2f) },
            new EnemyDef { id="bao_ruong_yeu", name="Bảo Rương Yêu", sprite=Res.Px.Mimic, hp=45, speed=2.4f, dmg=4, scale=1.15f, beh=EBeh.Mimic, orbs=14, minWave=6, weight=0.3f,
                blood=new Color(0.9f,0.7f,0.3f) },
            new EnemyDef { id="anh_sat", name="Ảnh Sát", sprite=Res.Px.AnhSat, hp=30, speed=2.2f, dmg=5, beh=EBeh.Blinker, orbs=4, minWave=12, weight=2, blood=new Color(0.6f,0.4f,1f) },
            new EnemyDef { id="hoa_ma", name="Hỏa Ma", sprite=Res.Px.HoaMa, hp=40, speed=2.6f, dmg=4, scale=1.2f, beh=EBeh.Charger, orbs=5, minWave=11, weight=2,
                blood=new Color(1f,0.5f,0.2f) },
            new EnemyDef { id="ma_tuong", name="Ma Tướng", sprite=Res.Px.MaTuong, hp=130, speed=1.7f, dmg=6, scale=1.5f, beh=EBeh.Chase, orbs=12, minWave=14, weight=0.8f, elite=true },
        };

        public static readonly EnemyDef MiniSlime = new EnemyDef { id="mini", name="Tiểu Độc Trùng", sprite=Res.Px.DocTrung, hp=3, speed=2.6f, dmg=1, scale=0.65f,
            beh=EBeh.Chase, orbs=1, blood=new Color(0.3f,0.9f,0.5f) };

        public static readonly EnemyDef[] Bosses =
        {
            new EnemyDef { id="b1", name="Độc Nhãn Ma Vương", sprite=Res.Px.Boss1, hp=1400, speed=1.7f, dmg=7, scale=3.2f, beh=EBeh.Boss, orbs=40 },
            new EnemyDef { id="b2", name="Hỏa Ma Tướng Quân", sprite=Res.Px.Boss2, hp=10000, speed=1.9f, dmg=12, scale=3.2f, beh=EBeh.Boss, orbs=60,
                blood=new Color(1f,0.5f,0.2f) },
            new EnemyDef { id="b3", name="Huyết Y Ma Tôn", sprite=Res.Px.Boss3, hp=45000, speed=2.0f, dmg=20, scale=3.2f, beh=EBeh.Boss, orbs=80 },
            new EnemyDef { id="b4", name="Thiên Ma Chi Chủ", sprite=Res.Px.Boss4, hp=140000, speed=2.1f, dmg=30, scale=3.6f, beh=EBeh.Boss, orbs=0, blood=new Color(0.7f,0.4f,1f) },
            new EnemyDef { id="b1b", name="Huyết Bức Vương", sprite=Res.Px.Boss1b, hp=1300, speed=2.4f, dmg=7, scale=3.0f, beh=EBeh.Boss, orbs=40 },
            new EnemyDef { id="b2b", name="Vạn Độc Chu Hậu", sprite=Res.Px.Boss2b, hp=10000, speed=2.0f, dmg=12, scale=3.2f, beh=EBeh.Boss, orbs=60, blood=new Color(0.4f,0.95f,0.3f) },
        };
        /// <summary>Boss for a 5-wave slot (0-3); slots 0 and 1 have an alternate boss picked per run.</summary>
        public static EnemyDef BossFor(int slot, bool alt) => alt && slot < 2 ? Bosses[4 + slot] : Bosses[slot];

        // ---------------------------------------------------------------- characters
        public static readonly List<CharDef> Chars = new List<CharDef>
        {
            new CharDef { id="kiem_tu", name="Kiếm Tu", title="Lý Thanh Phong", sprite=Res.Px.KiemTu, startWeapon="thiet_kiem",
                desc="Kiếm khách cân bằng, sở trường bạo kích.", unlockDesc="Mặc định",
                skill="Kiếm Vũ", skillDesc="12 phi kiếm toả ra, tự tìm địch", skillCd=9f,
                mods=new[]{ new Mod(Stat.Melee,15), new Mod(Stat.Crit,5) }, skillColor=new Color(0.502f, 1.000f, 1.000f), forms=new[]{ new SkillForm { name="Hồi Phong Kiếm", desc="Kiếm hết đà quay về người, chém thêm lần nữa" }, new SkillForm { name="Kiếm Trận Hộ Thân", desc="16 kiếm; kiếm về xoay quanh người 2 giây, chém quái chạm vào" }, new SkillForm { name="Thiên Kiếm Giáng Thế", desc="Thêm một cự kiếm rơi xuống chỗ quái đông nhất, nổ vùng lớn" } } },
            new CharDef { id="dan_tu", name="Đan Tu", title="Bạch Hạc Lão Nhân", sprite=Res.Px.DanTu, startWeapon="hoa_truong",
                desc="Luyện đan sư, pháp thuật mạnh, hồi máu nhanh.", unlockDesc="Đạt tới đợt 5",
                skill="Đan Hỏa Bạo", skillDesc="Nổ lửa quanh thân, hồi 25% máu", skillCd=9f,
                mods=new[]{ new Mod(Stat.Spell,20), new Mod(Stat.Regen,5), new Mod(Stat.MaxHP,5), new Mod(Stat.Melee,-15) }, skillColor=new Color(1.000f, 0.502f, 0.149f), forms=new[]{ new SkillForm { name="Liệt Hỏa Đan", desc="Thêm 4 viên đan lửa bay tới quái ngoài vùng nổ" }, new SkillForm { name="Đan Khí Hộ Thể", desc="Vùng nổ rộng hơn, xoá đạn địch, giảm 30% sát thương nhận vào 3 giây" }, new SkillForm { name="Lò Đỉnh Pháp Tướng", desc="Nổ thêm 2 đợt liên tiếp quanh người" } } },
            new CharDef { id="the_tu", name="Thể Tu", title="Thiết Hán Vô Danh", sprite=Res.Px.TheTu, startWeapon="thiet_chuy",
                desc="Luyện thể, da dày thịt béo, đánh chậm mà nặng.", unlockDesc="Tổng cộng diệt 1500 kẻ địch",
                skill="Kim Cang Chấn", skillDesc="Bất tử 2s + sóng chấn đẩy lùi", skillCd=12f,
                mods=new[]{ new Mod(Stat.MaxHP,15), new Mod(Stat.Armor,3), new Mod(Stat.Melee,10), new Mod(Stat.AtkSpeed,-10), new Mod(Stat.Speed,-5) }, skillColor=new Color(1.000f, 0.851f, 0.400f), forms=new[]{ new SkillForm { name="Chấn Hồn", desc="Sóng chấn rộng hơn, choáng địch 1 giây" }, new SkillForm { name="Địa Liệt", desc="4 đường nứt lan ra từ người, choáng địch" }, new SkillForm { name="Kim Thân Pháp Tướng", desc="Bất tử 3 giây, hết giờ thì dư chấn nổ lớn" } } },
            new CharDef { id="phu_tu", name="Phù Tu", title="Tô Vân Nhi", sprite=Res.Px.PhuTu, startWeapon="phu_luc",
                desc="Phù sư, tầm xa, cơ duyên tốt.", unlockDesc="Đạt cảnh giới Kim Đan",
                skill="Định Thân Phù", skillDesc="Đóng băng địch quanh thân 2.5s, chúng nhận thêm 30% ST", skillCd=12f,
                mods=new[]{ new Mod(Stat.Luck,20), new Mod(Stat.Range,20), new Mod(Stat.Spell,10), new Mod(Stat.MaxHP,-4) }, skillColor=new Color(0.502f, 0.902f, 1.000f), forms=new[]{ new SkillForm { name="Phi Phù", desc="Thêm 6 lá bùa bay tới quái ngoài vùng, đóng băng" }, new SkillForm { name="Bạo Phù", desc="Quái bị đóng băng hết băng sẽ nổ lên quái xung quanh" }, new SkillForm { name="Thiên La Địa Võng", desc="Vùng đóng băng 8 ô, quái chịu thêm 50% sát thương" } } },
            new CharDef { id="ma_tu", name="Ma Tu", title="Huyết Ảnh", sprite=Res.Px.MaTu, startWeapon="huyet_nguyet_dao",
                desc="Tà đạo: sát thương khủng, hút máu, nhưng mỏng manh.", unlockDesc="Phi thăng (thắng) 1 lần",
                skill="Huyết Tế", skillDesc="Hiến 15% máu: 5s +60% ST, +25% hút máu", skillCd=14f,
                mods=new[]{ new Mod(Stat.Damage,40), new Mod(Stat.Lifesteal,10), new Mod(Stat.MaxHP,-3), new Mod(Stat.Regen,-3), new Mod(Stat.Armor,-1) }, skillColor=new Color(0.902f, 0.102f, 0.200f), forms=new[]{ new SkillForm { name="Huyết Bạo", desc="Khi thi triển, vòng máu nổ quanh thân" }, new SkillForm { name="Huyết Thuẫn", desc="1.5 giây đầu giảm 40% sát thương; diệt quái kéo dài hiến tế" }, new SkillForm { name="Huyết Ma Pháp Tướng", desc="Vòng nổ lớn hơn, Hút Máu hiến tế +40%" } } },
            new CharDef { id="loi_tu", name="Lôi Tu", title="Lôi Vân Tử", sprite=Res.Px.LoiTu, startWeapon="loi_truong",
                desc="Lôi pháp: đánh cực nhanh, sét lan khắp đàn quái.", unlockDesc="Đạt tới đợt 10",
                skill="Thiên Lôi Dẫn", skillDesc="Gọi 8 tia sét lên kẻ địch gần nhất, làm choáng 1s", skillCd=11f,
                mods=new[]{ new Mod(Stat.AtkSpeed,20), new Mod(Stat.Spell,10), new Mod(Stat.Speed,5), new Mod(Stat.MaxHP,3) }, skillColor=new Color(0.702f, 0.851f, 1.000f), forms=new[]{ new SkillForm { name="Lôi Liên", desc="Mỗi tia sét nảy sang 2 quái gần đó" }, new SkillForm { name="Lôi Vực", desc="Vùng sấm đi theo người 4 giây, liên tục giật quái bên trong" }, new SkillForm { name="Lôi Thần Pháp Tướng", desc="12 tia sét và một cột sét lớn đánh quái cứng nhất" } } },
        };

        public static bool CharUnlocked(int i, SaveData s)
        {
            switch (i)
            {
                case 0: return true;
                case 1: return s.bestWave >= 5;
                case 2: return s.totalKills >= 1500;
                case 3: return s.bestLevel >= 20;
                case 4: return s.wins >= 1;
                case 5: return s.bestWave >= 10;
            }
            return false;
        }

        // ---------------------------------------------------------------- synergies
        public static readonly SynergyDef[] Synergies =
        {
            new SynergyDef { tag=Tag.Kiem, desc=new[]{"+5% Bạo Kích","+25% ST Bạo Kích","+20% Tốc Đánh"},
                mods=new[]{ new[]{M(Stat.Crit,5)}, new[]{M(Stat.CritDmg,25)}, new[]{M(Stat.AtkSpeed,20)} } },
            new SynergyDef { tag=Tag.The, desc=new[]{"+8 Sinh Lực","+3 Hộ Giáp, +15% Cận Chiến","+25 Sinh Lực"},
                mods=new[]{ new[]{M(Stat.MaxHP,8)}, new[]{M(Stat.Armor,3), M(Stat.Melee,15)}, new[]{M(Stat.MaxHP,25)} } },
            new SynergyDef { tag=Tag.Hoa, desc=new[]{"+10% Pháp Thuật","Thiêu đốt ×2","+35% Pháp Thuật"},
                mods=new[]{ new[]{M(Stat.Spell,10)}, new Mod[0], new[]{M(Stat.Spell,35)} } },
            new SynergyDef { tag=Tag.Loi, desc=new[]{"Sét nảy +1","+15% Tốc Đánh","Sét nảy +2, +10% Bạo Kích"},
                mods=new[]{ new Mod[0], new[]{M(Stat.AtkSpeed,15)}, new[]{M(Stat.Crit,10)} } },
            new SynergyDef { tag=Tag.Phu, desc=new[]{"+15% Tầm Đánh","Đạn xuyên +1, +15 Cơ Duyên","+30% Pháp Thuật"},
                mods=new[]{ new[]{M(Stat.Range,15)}, new[]{M(Stat.Luck,15)}, new[]{M(Stat.Spell,30)} } },
            new SynergyDef { tag=Tag.Doc, desc=new[]{"Độc +50%","Độc lây khi kẻ địch chết","Độc ×2"},
                mods=new[]{ new Mod[0], new Mod[0], new Mod[0] } },
        };
        public static readonly int[] SynergyAt = { 2, 4, 6 };

        // ---------------------------------------------------------------- level-up pool
        public static readonly Stat[] LevelStats = {
            Stat.MaxHP, Stat.Regen, Stat.Lifesteal, Stat.Damage, Stat.Melee, Stat.Spell, Stat.AtkSpeed, Stat.Crit,
            Stat.CritDmg, Stat.Range, Stat.Armor, Stat.Dodge, Stat.Speed, Stat.Luck, Stat.Harvest, Stat.SkillPower, Stat.SkillHaste };
        /// <summary>Thần Lực and Tĩnh Tâm come up half as often as the other level-up stats (spec 3.3).</summary>
        public static float LevelWeight(Stat s) => s == Stat.SkillPower || s == Stat.SkillHaste ? 0.5f : 1f;
        public static float LevelValue(Stat s, int tier)
        {
            switch (s)
            {
                case Stat.MaxHP: return new[] { 3, 6, 9, 13 }[tier];
                case Stat.Regen: return new[] { 2, 3, 4, 6 }[tier];
                case Stat.Lifesteal: return new[] { 1, 2, 3, 5 }[tier];
                case Stat.Damage: return new[] { 5, 8, 12, 17 }[tier];
                case Stat.Melee: return new[] { 6, 10, 15, 22 }[tier];
                case Stat.Spell: return new[] { 6, 10, 15, 22 }[tier];
                case Stat.AtkSpeed: return new[] { 5, 9, 13, 18 }[tier];
                case Stat.Crit: return new[] { 3, 5, 7, 10 }[tier];
                case Stat.CritDmg: return new[] { 10, 16, 25, 35 }[tier];
                case Stat.Range: return new[] { 8, 12, 18, 26 }[tier];
                case Stat.Armor: return new[] { 1, 2, 3, 4 }[tier];
                case Stat.Dodge: return new[] { 3, 5, 7, 9 }[tier];
                case Stat.Speed: return new[] { 3, 6, 9, 12 }[tier];
                case Stat.Luck: return new[] { 5, 10, 15, 20 }[tier];
                case Stat.Harvest: return new[] { 5, 8, 12, 16 }[tier];
                case Stat.SkillPower: return new[] { 15, 23, 34, 48 }[tier];
                case Stat.SkillHaste: return new[] { 4, 6, 9, 12 }[tier];
            }
            return 1;
        }

        // ---------------------------------------------------------------- meta
        public static readonly MetaDef[] Meta =
        {
            new MetaDef { name="Căn Cốt", desc="+4 Sinh Lực mỗi cấp", stat=Stat.MaxHP, per=4 },
            new MetaDef { name="Kiếm Ý", desc="+3% Sát Thương mỗi cấp", stat=Stat.Damage, per=3 },
            new MetaDef { name="Phúc Duyên", desc="+6 Cơ Duyên mỗi cấp", stat=Stat.Luck, per=6 },
            new MetaDef { name="Khinh Công", desc="+3% Thân Pháp mỗi cấp", stat=Stat.Speed, per=3 },
            new MetaDef { name="Tài Lộc", desc="+12 Linh Thạch khởi đầu mỗi cấp", special=true },
            new MetaDef { name="Ngộ Tính", desc="+7% kinh nghiệm mỗi cấp", special=true },
        };
        public const int MetaMax = 5;
        public static int MetaCost(int lvl) => 15 + lvl * 15;

        // ---------------------------------------------------------------- achievements
        public static readonly AchDef[] Achs =
        {
            new AchDef("wave1", "Sơ Nhập Giang Hồ", "Vượt qua đợt 1"),
            new AchDef("boss1", "Trảm Ma", "Hạ gục boss đầu tiên"),
            new AchDef("wave10", "Bán Bộ Thành Danh", "Vượt qua đợt 10"),
            new AchDef("win", "Phi Thăng", "Vượt qua đợt 20"),
            new AchDef("lv10", "Trúc Cơ", "Đạt Trúc Cơ trong một lượt"),
            new AchDef("lv20", "Kim Đan", "Đạt Kim Đan trong một lượt"),
            new AchDef("lv30", "Nguyên Anh", "Đạt Nguyên Anh trong một lượt"),
            new AchDef("k1000", "Sát Thần I", "Tổng cộng diệt 1.000 kẻ địch"),
            new AchDef("k10000", "Sát Thần II", "Tổng cộng diệt 10.000 kẻ địch"),
            new AchDef("tier4", "Tiên Khí Xuất Thế", "Sở hữu vũ khí phẩm Tiên"),
            new AchDef("syn6", "Đại Đạo Viên Mãn", "Kích hoạt mốc 6 của một Đạo"),
            new AchDef("rich", "Phú Khả Địch Quốc", "Giữ 400 linh thạch cùng lúc"),
            new AchDef("bighit", "Nhất Kích Tất Sát", "Gây 500 sát thương trong một đòn"),
            new AchDef("combo100", "Huyết Chiến Bách Liên", "Đạt Liên Sát x100"),
            new AchDef("nohit", "Thân Như Quỷ Mị", "Vượt qua một đợt (từ đợt 5) không trúng đòn nào"),
            new AchDef("allchars", "Vạn Pháp Quy Nhất", "Mở khóa tất cả nhân vật"),
            new AchDef("kiep3", "Độ Kiếp", "Phi thăng ở Kiếp 3"),
            new AchDef("kiep5", "Thiên Kiếp Bất Diệt", "Phi thăng ở Kiếp 5"),
            new AchDef("codex", "Bách Khoa Toàn Thư", "Hoàn thành Đồ Giám"),
            new AchDef("allwin", "Tông Sư", "Phi thăng với cả 6 nhân vật"),
        };

        public const int MaxKiep = 5;
        public static readonly string[] KiepDesc = {
            "Kiếp 0 — Phàm nhân lộ: thử thách chuẩn.",
            "Kiếp 1 — Quái +30% máu, +25% sát thương (tăng dần theo đợt).",
            "Kiếp 2 — Thêm: quái tinh anh xuất hiện nhiều hơn.",
            "Kiếp 3 — Thêm: quái xuất hiện dày hơn 20%.",
            "Kiếp 4 — Thêm: Thiên Kiếp giáng xuống mọi đợt.",
            "Kiếp 5 — Thêm: boss thêm 15% máu. Chỉ dành cho bậc tông sư.",
        };

        public static WeaponDef W(string id) => Weapons.Find(w => w.id == id);
        public static ItemDef It(string id) => Items.Find(w => w.id == id);
    }
}
