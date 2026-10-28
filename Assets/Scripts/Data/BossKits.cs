using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    public static partial class Content
    {
        public static readonly List<BossKit> Kits = new List<BossKit>
        {
            BossKits.B1(), BossKits.B1b(), BossKits.B2(), BossKits.B2b(), BossKits.B3(), BossKits.B4(),
        };

        public static BossKit Kit(string bossId) => Kits.Find(k => k.bossId == bossId);
    }

    /// <summary>The six boss move tables (spec section 6). Every balance number of a boss move lives here; field meaning per kind is documented on BossMove.</summary>
    public static class BossKits
    {
        static Color H(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        static BossMove M(string id, string name, BMove kind, BTier tier, float tele, float dmg, float cd, bool ult = false) =>
            new BossMove { id = id, name = name, kind = kind, tier = tier, tele = tele, dmg = dmg, cd = cd, ult = ult };

        public static BossKit B1()
        {
            var k = new BossKit { bossId = "b1", epithet = "Ma Nhãn Hắc Sơn", col = H("#B04DFF"), col2 = H("#B04DFF"), rest = 1.6f, summonCap = 0,
                quoteMutate = "Con mắt này đã thấy thiên đạo... và thấy cái chết của ngươi.", quoteMaThan = "Vạn nhãn khai, thiên địa tận!" };
            var m = k.moves; BossMove x;
            m.Add(x = M("ma_nhan_quet", "Ma Nhãn Quét", BMove.Sweep, BTier.Base, 0.75f, 0.8f, 5f));
            x.n = 1; x.len = 9f; x.width = 0.6f; x.angle = 90f; x.dur = 0.8f; x.speed = 45f;
            m.Add(x = M("doc_nhan_xung", "Độc Nhãn Xung", BMove.Dash, BTier.Base, 0.85f, 1.2f, 4.5f));
            x.len = 8f; x.width = 1.6f; x.speed = 5f;
            m.Add(x = M("ta_quang_phan_liet", "Tà Quang Phân Liệt", BMove.Sweep, BTier.P2, 0.7f, 0.5f, 6f));
            x.n = 3; x.len = 6f; x.width = 0.6f; x.angle = 40f; x.dur = 0.6f; x.gap = 0.2f;
            m.Add(x = M("van_nhan_thien_la", "Vạn Nhãn Thiên La", BMove.Sweep, BTier.Base, 1.2f, 0.7f, 0f, true));
            x.n = 8; x.len = 10f; x.width = 0.5f; x.angle = 120f; x.dur = 3f; x.radius = 45f;
            m.Add(x = M("nhan_cau_truy_kich", "Nhãn Cầu Truy Kích", BMove.Zones, BTier.I, 0.7f, 0.6f, 7f));
            x.n = 2; x.radius = 1.3f; x.layout = ZLayout.AroundPlayer; x.dur = 1.5f;
            m.Add(x = M("ta_nhan_phong_an", "Tà Nhãn Phong Ấn", BMove.Pillars, BTier.I, 1.0f, 0.9f, 0f, true));
            x.n = 3; x.radius = 2.2f; x.gap = 0.8f;
            m.Add(x = M("an_mu", "Ấn Mù", BMove.Zones, BTier.II, 0f, 0.2f, 0f));
            x.after = "doc_nhan_xung"; x.n = 2; x.radius = 1.1f; x.life = 4f; x.tick = 0.5f;
            m.Add(x = M("diet_the_ma_nhan", "Diệt Thế Ma Nhãn", BMove.Sweep, BTier.II, 1.5f, 1.0f, 0f, true));
            x.n = 1; x.len = 18f; x.width = 0.8f; x.angle = 340f; x.dur = 2.6f; x.radius2 = 20f;
            return k;
        }

        public static BossKit B1b()
        {
            var k = new BossKit { bossId = "b1b", epithet = "Chúa Tể Đêm Máu", col = H("#E64DB0"), col2 = H("#E64DB0"), rest = 1.3f, summonCap = 6,
                quoteMutate = "Đêm nay... máu ngươi sẽ nhuộm đỏ trăng.", quoteMaThan = "Huyết nguyệt đã lên!" };
            var m = k.moves; BossMove x;
            m.Add(x = M("am_ba_huyet_duc", "Âm Ba Huyết Dực", BMove.Cone, BTier.Base, 0.6f, 0.5f, 4.5f));
            x.radius = 5f; x.angle = 70f; x.reps = 3; x.gap = 0.5f;
            m.Add(x = M("huyet_anh_luot", "Huyết Ảnh Lướt", BMove.Dash, BTier.Base, 0.8f, 1.1f, 4.5f));
            x.n = 3; x.len = 10f; x.width = 1.4f; x.speed = 5f;
            m.Add(x = M("da_hanh", "Dạ Hành", BMove.Blink, BTier.P2, 0.6f, 0.9f, 6f));
            x.dur = 0.45f; x.radius = 3f; x.len = 3f; x.radius2 = 4f;
            m.Add(x = M("huyet_nguyet_da_yen", "Huyết Nguyệt Dạ Yến", BMove.Dash, BTier.Base, 1.0f, 1.0f, 0f, true));
            x.reps = 4; x.len = 34f; x.width = 1.6f; x.speed = 6f; x.hook = "dark";
            m.Add(x = M("bay_doi", "Bầy Dơi", BMove.Summon, BTier.I, 0.6f, 0.35f, 7f));
            x.n = 4; x.hook = "bat_small"; x.life = 5f;
            // Hook (spec: Line): bat wall spanning the arena sweeps across over dur; width = safe gap size (cells) that moves with the wall; dmg = wall hit.
            m.Add(x = M("van_buc_trieu", "Vạn Bức Triều", BMove.Hook, BTier.I, 1.2f, 0.8f, 0f, true));
            x.hook = "van_buc_trieu"; x.width = 3f; x.dur = 3f;
            m.Add(x = M("vong_am", "Vọng Âm", BMove.Dash, BTier.II, 0f, 0.6f, 0f));
            x.after = "huyet_anh_luot"; x.gap = 0.55f; x.speed = 3f;   // echo dash after gap seconds at 60% of the host dash speed 5
            m.Add(x = M("huyet_hai_cuong_ca", "Huyết Hải Cuồng Ca", BMove.Nova, BTier.II, 1.2f, 0.7f, 0f, true));
            x.reps = 5; x.radius = 1f; x.radius2 = 16f; x.gap = 0.6f; x.dur = 1.4f; x.gaps = 1; x.angle = 40f; x.speed = 90f;
            return k;
        }

        public static BossKit B2()
        {
            var k = new BossKit { bossId = "b2", epithet = "Đại Tướng Luyện Ngục", col = H("#FF7A1A"), col2 = H("#FF7A1A"), rest = 1.5f, summonCap = 0,
                quoteMutate = "Lửa trời đã nhập vào binh khí của ta!", quoteMaThan = "Luyện ngục, khai môn!" };
            var m = k.moves; BossMove x;
            m.Add(x = M("liet_diem_tam_kich", "Liệt Diễm Tam Kích", BMove.Cone, BTier.Base, 0.65f, 0.55f, 5f));
            x.radius = 4f; x.angle = 80f; x.reps = 3; x.gap = 0.45f;
            m.Add(x = M("hoa_cau_te_xa", "Hỏa Cầu Tề Xạ", BMove.Fan, BTier.Base, 0.6f, 0.5f, 4.5f));
            x.n = 5; x.angle = 40f; x.speed = 5f; x.reps = 2; x.gap = 0.4f;
            m.Add(x = M("viem_hoan", "Viêm Hoàn", BMove.Nova, BTier.P2, 0.8f, 1.0f, 6f));
            x.radius = 1.5f; x.radius2 = 9f; x.dur = 1.1f; x.gaps = 2; x.angle = 35f;
            m.Add(x = M("thien_hoa_lac", "Thiên Hỏa Lạc", BMove.Zones, BTier.Base, 1.0f, 0.9f, 0f, true));
            x.n = 6; x.radius = 1.6f; x.layout = ZLayout.Random; x.gaps = 2; x.reps = 3; x.gap = 0.7f;
            m.Add(x = M("hoa_tru", "Hỏa Trụ", BMove.Zones, BTier.I, 0.7f, 0.25f, 7f));
            x.n = 3; x.radius = 1.8f; x.layout = ZLayout.AroundPlayer; x.life = 3f; x.tick = 0.5f;
            // Hook (spec: Nova Annulus): fire closes from the arena edge to a safe circle radius around the centre and stays dur s (tick = damage interval); boss fires a Fan of n bullets (angle spread, speed) every gap s into it.
            m.Add(x = M("liet_hoa_thanh", "Liệt Hỏa Thành", BMove.Hook, BTier.I, 1.5f, 0.3f, 0f, true));
            x.hook = "liet_hoa_thanh"; x.radius = 6f; x.dur = 4f; x.tick = 0.5f; x.n = 5; x.angle = 40f; x.speed = 5f; x.gap = 1f;
            m.Add(x = M("no_hoa", "Nộ Hỏa", BMove.Zones, BTier.II, 0f, 0.15f, 0f));
            x.after = "hoa_cau_te_xa"; x.n = 8; x.radius = 1f; x.life = 3f; x.tick = 0.5f;
            // Hook (spec: Sweep from banners): n banners (breakable Enemy war_flag, hp = radius2 x boss max hp) in the corners; each fires one beam of length len, width width, rotating angle deg over dur s; a dead banner stops its beam.
            m.Add(x = M("phan_thien_chien_ky", "Phần Thiên Chiến Kỳ", BMove.Hook, BTier.II, 1.2f, 0.5f, 0f, true));
            x.hook = "phan_thien_chien_ky"; x.n = 4; x.len = 7f; x.width = 0.6f; x.angle = 360f; x.dur = 6f; x.radius2 = 0.04f;
            return k;
        }

        public static BossKit B2b()
        {
            var k = new BossKit { bossId = "b2b", epithet = "Mẫu Hoàng Vạn Độc", col = H("#66F233"), col2 = H("#66F233"), rest = 1.5f, summonCap = 6,
                quoteMutate = "Thiên kiếp... chỉ là tơ để ta dệt lưới.", quoteMaThan = "Vạn độc quy tông!" };
            var m = k.moves; BossMove x;
            m.Add(x = M("doc_ty_troi", "Độc Ty Trói", BMove.Line, BTier.Base, 0.7f, 0.6f, 5.5f));
            x.n = 3; x.layout = ZLayout.Radial; x.width = 0.5f; x.life = 2f; x.slow = 0.3f; x.dur = 1.2f;
            m.Add(x = M("noc_phun", "Nọc Phun", BMove.Zones, BTier.Base, 0.65f, 0.2f, 5f));
            x.n = 3; x.radius = 1.25f; x.layout = ZLayout.AroundPlayer; x.life = 4f; x.tick = 0.5f;
            // Hook: n cocoons (Enemy cocoon, hp = radius2 x boss max hp) beside the boss; after dur s each hatches reps spiderlings (kit.summonCap applies). tele = spawn warning.
            m.Add(x = M("ken_doc", "Kén Độc", BMove.Hook, BTier.P2, 0.6f, 0f, 8f));
            x.hook = "ken_doc"; x.n = 2; x.radius2 = 0.02f; x.dur = 3.5f; x.reps = 2;
            m.Add(x = M("thien_la_dia_vong", "Thiên La Địa Võng", BMove.Line, BTier.Base, 1.2f, 0.8f, 0f, true));
            x.n = 5; x.reps = 3; x.layout = ZLayout.Grid; x.width = 0.5f; x.life = 3f;
            m.Add(x = M("ty_dan", "Tơ Đan", BMove.Line, BTier.I, 0.6f, 0.45f, 7f));
            x.n = 1; x.layout = ZLayout.Between; x.width = 0.5f; x.life = 2.5f;
            // Hook: n cocoons on the arena edge (layout EdgeRing, hp = radius2 x boss max hp) all hatch after dur s, reps spiderlings each (kit.summonCap applies).
            m.Add(x = M("van_trung_phe", "Vạn Trùng Phệ", BMove.Hook, BTier.I, 1.0f, 0f, 0f, true));
            x.hook = "van_trung_phe"; x.n = 6; x.layout = ZLayout.EdgeRing; x.radius2 = 0.02f; x.dur = 3f; x.reps = 2;
            m.Add(x = M("phun_moi", "Phun Mồi", BMove.Summon, BTier.II, 0f, 0f, 0f));
            // Summon rider: every gaps-th (2nd) Noc Phun cast also spawns one spiderling at a puddle, at most n alive from this move.
            x.after = "noc_phun"; x.hook = "spiderling"; x.n = 3; x.gaps = 2;
            // Hook: floor floods with poison for dur s (tick = damage interval, dmg per tick) except n safe patches of radius radius drifting at speed tiles/s.
            m.Add(x = M("doc_hai", "Độc Hải", BMove.Hook, BTier.II, 1.5f, 0.3f, 0f, true));
            x.hook = "doc_hai"; x.n = 3; x.radius = 2f; x.speed = 1f; x.dur = 5f; x.tick = 0.5f;
            return k;
        }

        public static BossKit B3()
        {
            var k = new BossKit { bossId = "b3", epithet = "Ma Tôn Huyết Hải", col = H("#CC1A33"), col2 = H("#CC1A33"), rest = 1.3f, summonCap = 0,
                quoteMutate = "Máu của thiên lôi... ngọt hơn máu người.", quoteMaThan = "Huyết hải vô biên, hồi đầu vô ngạn!" };
            var m = k.moves; BossMove x;
            m.Add(x = M("huyet_luan", "Huyết Luân", BMove.Ring, BTier.Base, 0.7f, 0.55f, 4.5f));
            x.n = 12; x.speed = 4.5f; x.reps = 2; x.gap = 0.35f;
            m.Add(x = M("huyet_bo", "Huyết Bộ", BMove.Blink, BTier.Base, 0.65f, 0.9f, 5f));
            x.dur = 0.4f; x.radius = 2.6f; x.len = 2.5f; x.radius2 = 4f;
            // Hook: n clones (no damage taken, passable) each fire a Ring of gaps bullets at speed; the real boss gets a bright outline; clones vanish after life s.
            m.Add(x = M("ma_anh_huyet_y", "Ma Ảnh Huyết Y", BMove.Hook, BTier.P2, 0.7f, 0.35f, 9f));
            x.hook = "phan_than"; x.n = 2; x.life = 6f; x.gaps = 6; x.speed = 4.5f;
            m.Add(x = M("huyet_ha_dao_luu", "Huyết Hà Đảo Lưu", BMove.Spiral, BTier.Base, 1.0f, 0.45f, 0f, true));
            x.n = 2; x.reps = 3; x.speed = 90f; x.dur = 3f; x.gap = 0.12f; x.radius2 = 1.5f;
            m.Add(x = M("huyet_te_truong", "Huyết Tế Trường", BMove.Nova, BTier.I, 0.6f, 0.85f, 7f));
            x.radius = 3.8f; x.radius2 = 1f; x.dur = 1.2f;
            // Hook: n clones appear around the player at distance radius, then one by one dash through the player position (speed = boss-speed multiplier, width), gap s apart.
            m.Add(x = M("huyet_anh_van_sat", "Huyết Ảnh Vạn Sát", BMove.Hook, BTier.I, 1.0f, 0.8f, 0f, true));
            x.hook = "huyet_anh_van_sat"; x.n = 4; x.radius = 6f; x.gap = 0.5f; x.speed = 8f; x.width = 1.4f;
            m.Add(x = M("huyet_phan", "Huyết Phản", BMove.Blink, BTier.II, 0f, 0.5f, 0f));
            x.after = "huyet_bo"; x.gap = 0.5f; x.radius = 2.6f;
            m.Add(x = M("huyet_vu", "Huyết Vũ", BMove.Zones, BTier.II, 1.0f, 0.8f, 0f, true));
            x.n = 3; x.layout = ZLayout.Checker; x.reps = 4; x.gap = 0.8f;
            return k;
        }

        public static BossKit B4()
        {
            var k = new BossKit { bossId = "b4", epithet = "Chúa Tể Vạn Ma", col = H("#B366FF"), col2 = H("#FFD24D"), rest = 1.2f, summonCap = 0,
                quoteMutate = "Ngươi tưởng thiên kiếp là của riêng ngươi sao?", quoteMaThan = "Ta chính là Thiên Đạo!" };
            var m = k.moves; BossMove x;
            m.Add(x = M("thien_loi_cam_vuc", "Thiên Lôi Cấm Vực", BMove.Zones, BTier.Base, 0.75f, 0.75f, 4.5f));
            x.n = 3; x.radius = 1.35f; x.layout = ZLayout.AroundPlayer; x.reps = 2; x.gap = 0.5f;
            m.Add(x = M("hu_khong_lao", "Hư Không Lao", BMove.Dash, BTier.Base, 0.8f, 1.2f, 5f));
            x.len = 9f; x.width = 1.6f; x.speed = 5f; x.radius = 2.2f;
            m.Add(x = M("thien_ma_phap_tuong", "Thiên Ma Pháp Tướng", BMove.Pillars, BTier.P2, 0.7f, 0.7f, 5.5f));
            x.n = 4; x.radius = 1.7f; x.gap = 0.7f;
            // Hook: a giant hand (Fx.Apparition) slams n of the 4 quadrants (layout Quadrants) one after another, gap s apart; the untouched quadrant is highlighted as the safe spot.
            m.Add(x = M("thien_ma_giang_the", "Thiên Ma Giáng Thế", BMove.Hook, BTier.Base, 1.3f, 1.1f, 0f, true));
            x.hook = "thien_ma_giang_the"; x.n = 3; x.layout = ZLayout.Quadrants; x.gap = 0.8f;
            // Hook: n gates on the arena edge; during dur s each fires reps volleys, each a Fan of gaps bullets (angle spread, speed) diagonally into the arena.
            m.Add(x = M("hu_khong_mon", "Hư Không Môn", BMove.Hook, BTier.I, 0.7f, 0.42f, 7f));
            x.hook = "hu_khong_mon"; x.n = 2; x.reps = 3; x.dur = 3f; x.gaps = 3; x.angle = 40f; x.speed = 5f;
            // Hook: n ghosts of the previous bosses appear one by one gap s apart, each casting its signature move at dmg (0.7 of the original); banner shows the original move name.
            m.Add(x = M("van_ma_quy_tong", "Vạn Ma Quy Tông", BMove.Hook, BTier.I, 1.0f, 0.7f, 0f, true));
            x.hook = "van_ma_quy_tong"; x.n = 5; x.gap = 1.2f;
            m.Add(x = M("thien_kiep_cong_huong", "Thiên Kiếp Cộng Hưởng", BMove.Zones, BTier.II, 0f, 0.15f, 0f));
            x.after = "thien_ma_phap_tuong"; x.radius = 1f; x.life = 2.5f; x.tick = 0.5f;
            // Hook, two stages: (1) n tribulation Pillars of radius radius chase the player, gap s apart, tele/dmg; (2) a full-field strike except a safe circle of radius radius2 at the arena centre, tele2/dmg2.
            m.Add(x = M("thien_dao_tich_diet", "Thiên Đạo Tịch Diệt", BMove.Hook, BTier.II, 0.6f, 0.6f, 0f, true));
            x.tele2 = 1.5f; x.dmg2 = 1.5f; x.hook = "thien_dao_tich_diet"; x.n = 9; x.radius = 1.5f; x.gap = 0.35f; x.radius2 = 2.5f;
            return k;
        }
    }
}
