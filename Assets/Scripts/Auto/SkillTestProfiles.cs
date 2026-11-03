using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VK
{
    /// <summary>Profiles, the erase-progress dialog and the boss portrait upscale (prefix "prof"), plus the old-look intro card shot.</summary>
    public partial class SkillTest
    {
        static Button BtnByLabel(string label, bool onlyUsable = false)
        {
            Button hit = null;
            foreach (var b in Object.FindObjectsOfType<Button>())
                if (b.isActiveAndEnabled && (!onlyUsable || b.interactable)) { var t = b.GetComponentInChildren<Text>(); if (t != null && t.text == label) hit = b; }
            return hit;
        }

        static int UsableCount(string label) { int n = 0; foreach (var b in Object.FindObjectsOfType<Button>()) if (b.isActiveAndEnabled && b.interactable && b.GetComponentInChildren<Text>()?.text == label) n++; return n; }

        static void Click(string label)
        {
            var b = BtnByLabel(label, true);
            if (b != null) b.onClick.Invoke(); else Debug.Log("[AUTOTEST] no usable button " + label);
        }

        static string SelLabel(PadNav nav) => nav.Selected != null ? nav.Selected.GetComponentInChildren<Text>().text : "none";

        IEnumerator T_prof_a_flow()
        {
            yield return Arena(0, 5, 1); var g = Game.I; var ui = g.ui; g.state = GState.Menu;   // PadNav only works on menu screens
            string realSave = PlayerPrefs.GetString("vk_save_v1", "#none"), realIdx = PlayerPrefs.GetString("vk_profiles", "#none");
            int lang0 = g.save.lang; ui.ApplyLanguage(Loc.VI); yield return null;
            bool inKeys = SaveData.Key == "vk_save_autotest" && Profiles.Base == "vk_save_autotest" && Profiles.IndexKey == "vk_profiles_autotest";
            for (int i = 0; i < Profiles.Max; i++) inKeys &= Profiles.SlotKey(i).StartsWith("vk_save_autotest") && Profiles.SlotKey(i) != "vk_save_v1";
            Check("prof_keys_autotest", inKeys && Profiles.Active == 0 && Profiles.Used(0) && !Profiles.Used(1), SaveData.Key + " / " + Profiles.IndexKey);

            g.save.daoHanh = 77; g.save.runs = 4; g.save.shake = 0.5f; g.save.sfxVol = 0.3f; g.save.Save();
            ui.ShowProfiles(); yield return null;
            Check("prof_screen", ui.screen == UIScreen.Profiles && BtnByLabel("Tạo hồ sơ") != null && BtnByLabel("Đổi tên") != null, "");
            // create profile 2 through the UI
            Click("Tạo hồ sơ"); yield return null;
            Check("prof_name_entry", ui.profMode == 1 && ui.NameField != null && ui.NameField.characterLimit == 16, "");
            var nav = g.GetComponent<PadNav>(); nav.Hide(); nav.Navigate(Vector2.right); yield return null;
            Check("prof_name_default_focus", SelLabel(nav) == "OK", SelLabel(nav));
            Click("Ngẫu nhiên"); string rnd = ui.NameField.text;
            Check("prof_random_name", rnd.Length > 0 && rnd.Length <= 16, rnd);
            ui.NameField.text = "   Tiểu Minh   "; ui.NameOk(); yield return null;
            Check("prof_created_switched", Profiles.Active == 1 && Profiles.Name(1) == "Tiểu Minh" && SaveData.Key == Profiles.SlotKey(1) && ui.screen == UIScreen.Menu, Profiles.Active + " " + Profiles.Name(1));
            Check("prof_new_fresh", g.save.daoHanh == 0 && g.save.runs == 0, g.save.daoHanh + "");
            Check("prof_new_settings", Mathf.Approximately(g.save.shake, 0.5f) && Mathf.Approximately(g.save.sfxVol, 0.3f) && g.save.lang == Loc.VI, g.save.shake + " " + g.save.sfxVol);
            g.save.daoHanh = 5; g.save.Save();
            ui.ShowProfiles(); yield return null;
            Check("prof_active_locked", UsableCount("Xoá") == 1 && UsableCount("Chọn") == 1, UsableCount("Xoá") + "/" + UsableCount("Chọn"));   // the active card has neither Chọn nor Xoá while another profile exists
            // back to profile 1: progress intact
            Click("Chọn"); yield return null;
            Check("prof_switch_back", Profiles.Active == 0 && g.save.daoHanh == 77 && g.save.runs == 4 && SaveData.Key == Profiles.SlotKey(0), g.save.daoHanh + "");
            Check("prof_p2_kept", SaveData.Load(Profiles.SlotKey(1)).daoHanh == 5, "");
            // rename
            ui.ShowProfiles(); yield return null; Click("Đổi tên"); yield return null;
            Check("prof_rename_prefill", ui.NameField != null && ui.NameField.text == Profiles.Name(0), "");
            ui.NameField.text = "Đạo Sĩ"; ui.NameOk(); yield return null;
            Check("prof_rename", Profiles.Name(0) == "Đạo Sĩ" && ui.screen == UIScreen.Profiles && g.save.daoHanh == 77, Profiles.Name(0));
            Check("prof_clean", Profiles.Clean("   ", 1) == "Đạo Hữu 2" && Profiles.Clean(new string('x', 30), 0).Length == 16, "");
            // delete profile 2 (not the active one): the active card's Xoá is greyed out, so the only usable Xoá is slot 2's
            Click("Xoá"); yield return null;
            Check("prof_delete_confirm", ui.profMode == 2 && Object.FindObjectsOfType<PadDefault>().Length == 1 && Object.FindObjectOfType<PadDefault>().GetComponentInChildren<Text>().text == "Huỷ", "");
            nav.Hide(); nav.Navigate(Vector2.right); yield return null;
            Check("prof_delete_default_cancel", SelLabel(nav) == "Huỷ", SelLabel(nav));
            ui.ProfileBack(); yield return null;   // B
            Check("prof_delete_cancel", ui.profMode == 0 && Profiles.Used(1) && ui.screen == UIScreen.Profiles, "");
            Check("prof_delete_active_refused", !Profiles.Delete(0) && Profiles.Used(0), "");
            Click("Xoá"); yield return null; Click("Xoá"); yield return null;
            Check("prof_delete_slot", !Profiles.Used(1) && !PlayerPrefs.HasKey(Profiles.SlotKey(1)) && Profiles.Used(0) && g.save.daoHanh == 77, "");
            ui.ProfileBack(); yield return null;
            Check("prof_back_menu", ui.screen == UIScreen.Menu, ui.screen + "");

            // erase all progress
            g.save.sfxVol = 0.2f; g.save.winSize = 2; g.save.Save();
            ui.HideHint(); ui.ShowSettings(false); yield return new WaitForSecondsRealtime(0.4f); g.bot.Shot("prof_settings"); yield return new WaitForSecondsRealtime(0.3f);   // the capture happens at the end of the frame
            Click("Xoá toàn bộ tiến trình"); yield return null;
            nav.Hide(); nav.Navigate(Vector2.right); yield return null;
            Check("reset_confirm_up", ui.ResetConfirmUp && BtnByLabel("Xoá") != null, "");
            Check("reset_default_cancel", Object.FindObjectsOfType<PadDefault>().Length == 1 && SelLabel(nav) == "Huỷ", SelLabel(nav));
            yield return new WaitForSecondsRealtime(0.2f); g.bot.Shot("prof_reset_confirm"); yield return new WaitForSecondsRealtime(0.3f);
            ui.SettingsBack(); yield return null;   // B / Android back
            Check("reset_back_cancels", !ui.ResetConfirmUp && ui.screen == UIScreen.Settings && g.save.daoHanh == 77, "");
            Click("Xoá toàn bộ tiến trình"); yield return null; Click("Xoá"); yield return null;
            var re = SaveData.Load();
            Check("reset_progress_zeroed", g.save.daoHanh == 0 && g.save.runs == 0 && g.save.wins == 0 && g.save.ach.Count == 0 && g.save.codex.Count == 0 && re.daoHanh == 0 && re.runs == 0, g.save.daoHanh + "");
            Check("reset_keeps_settings_name", Mathf.Approximately(g.save.sfxVol, 0.2f) && g.save.winSize == 2 && Mathf.Approximately(g.save.shake, 0.5f) && Profiles.Name(0) == "Đạo Sĩ" && g.save.lang == Loc.VI, "");
            Check("reset_to_menu", ui.screen == UIScreen.Menu && g.state == GState.Menu, ui.screen + "");
            // the only profile: Xoá = reset
            g.save.daoHanh = 9; g.save.Save(); ui.ShowProfiles(); yield return null;
            Check("prof_only_can_reset", Profiles.UsedCount == 1 && BtnByLabel("Xoá", true) != null, "");
            Click("Xoá"); yield return null; Click("Xoá"); yield return null;
            Check("prof_only_reset", g.save.daoHanh == 0 && Profiles.Used(0) && Profiles.Name(0) == "Đạo Sĩ", "");

            Profiles.Rename(0, "Đạo Hữu");
            bool untouched = PlayerPrefs.GetString("vk_save_v1", "#none") == realSave && PlayerPrefs.GetString("vk_profiles", "#none") == realIdx;
            Check("prof_real_keys_untouched", untouched && SaveData.Key.StartsWith("vk_save_autotest"), "");
            ui.ApplyLanguage(lang0); g.save.shake = 1f; g.save.sfxVol = 0.8f; g.save.winSize = 1; g.save.Save(); ui.ShowMenu();
            yield return null;
        }

        IEnumerator T_prof_b_portrait()
        {
            var src = Resources.Load<Sprite>("PixelLab/Portraits/b1"); var big = Res.BossPortrait("b1");
            Check("prof_portrait_8x", src != null && big != null && big.texture.width == src.texture.width * 8 && big.texture.height == src.texture.height * 8, big ? big.texture.width + " vs " + src.texture.width : "null");
            Check("prof_portrait_filter", big.texture.filterMode == FilterMode.Bilinear && big.texture.mipmapCount == 1 && big.texture.wrapMode == TextureWrapMode.Clamp, big.texture.filterMode + "");
            Check("prof_portrait_cached", ReferenceEquals(big, Res.BossPortrait("b1")), "");
            Check("prof_portrait_shape", Mathf.Abs(big.rect.width / big.rect.height - src.rect.width / src.rect.height) < 0.001f && Mathf.Abs(big.pivot.x / big.rect.width - src.pivot.x / src.rect.width) < 0.001f, "");
            var tex = big.texture; int bad = 0;
            for (int i = 0; i < 40; i++)
            {   // nearest-neighbour: the interior of every 8x8 block is one colour
                int bx = (i * 37) % (tex.width / 8), by = (i * 53) % (tex.height / 8);
                var a = tex.GetPixel(bx * 8 + 2, by * 8 + 2); var b = tex.GetPixel(bx * 8 + 5, by * 8 + 5);
                if (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a) > 0.01f) bad++;
            }
            Check("prof_portrait_blocks", bad == 0, bad + " mixed blocks");
            yield return null;
        }

        IEnumerator T_introshot_old()   // the card with the raw ~80 px portrait (how it looked before the 8x copy), for comparison
        {
            yield return BossArena("b1", 0, 5); var b = boss.brain; var c = Game.I.cinema;
            c.testMode = true;
            var card = new BossIntroCard(Game.I.ui.Root, b.e.def, b.kit, VK.Arena.RegionName[0], Resources.Load<Sprite>("PixelLab/Portraits/b1"));
            for (float t = 0f; t < 1.3f; t += Time.unscaledDeltaTime) { card.Apply(t); yield return null; }
            Game.I.bot.Shot("boss_intro_old"); yield return new WaitForSecondsRealtime(0.4f);
            card.Finish(); c.testMode = false;
        }

        IEnumerator T_prof_c_shots()
        {
            yield return Arena(0, 5, 1); var g = Game.I; var ui = g.ui;
            ui.ApplyLanguage(Loc.VI); ui.HideHint();
            g.save.daoHanh = 1234; g.save.runs = 17; g.save.bestWave = 24; g.save.wins = 3; g.save.Save();
            var p2 = Profiles.Create(1, "Tiểu Minh", g.save); g.save = Profiles.Switch(0, p2);   // profile 1 stays live, profile 2 exists
            ui.ShowProfiles(); yield return new WaitForSecondsRealtime(0.5f); g.bot.Shot("prof_cards2"); yield return new WaitForSecondsRealtime(0.3f);
            Profiles.Delete(1); ui.ShowProfiles(); yield return new WaitForSecondsRealtime(0.3f);
            Click("Tạo hồ sơ"); yield return new WaitForSecondsRealtime(0.4f); ui.NameField.text = "Vân Hạc"; yield return null; g.bot.Shot("prof_name_entry"); yield return new WaitForSecondsRealtime(0.3f);
            ui.ShowMenu(); yield return new WaitForSecondsRealtime(0.4f); g.bot.Shot("prof_menu"); yield return new WaitForSecondsRealtime(0.3f);
            g.save.daoHanh = 0; g.save.runs = 0; g.save.bestWave = 0; g.save.wins = 0; g.save.Save();
        }
    }
}
