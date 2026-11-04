using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VK
{
    /// <summary>Profiles, the erase-progress dialog and the boss portrait upscale (prefix "prof"), the old-look intro card shot, and the codex Thần Thông tab (prefix "cdx").</summary>
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

        // ---------------------------------------------------------------- Đồ Giám: tab Thần Thông, charBestStage (prefix "cdx")
        IEnumerator T_cdx_a_save()
        {
            yield return Arena(2, 5, 0); var g = Game.I;
            string realSave = PlayerPrefs.GetString("vk_save_v1", "#none"), realIdx = PlayerPrefs.GetString("vk_profiles", "#none");
            Check("cdx_keys_autotest", SaveData.Key.StartsWith("vk_save_autotest"), SaveData.Key);

            // a save written before the field existed: a win implies the bosses of waves 5, 10, 15; other progress stays
            var old = new SaveData(); old.charWinKiep[1] = 2; old.daoHanh = 42; old.codex.Add("w:x");
            string json = System.Text.RegularExpressions.Regex.Replace(JsonUtility.ToJson(old), "\"charBestStage\":\\[[^\\]]*\\],", "");
            const string k = "vk_save_autotest_cdx";
            Platform.Services.SaveString(k, json);
            var d = SaveData.Load(k);
            Check("cdx_old_save", !json.Contains("charBestStage") && d.charBestStage.Length == Content.Chars.Count && d.charBestStage[0] == 0 && d.charBestStage[1] == 3 && d.daoHanh == 42 && d.Seen("w:x") && d.charWinKiep[1] == 2, string.Join(",", d.charBestStage));
            Platform.Services.SaveString(k, json.Replace("\"daoHanh\":42,", "\"daoHanh\":42,\"charBestStage\":[2],"));
            d = SaveData.Load(k);
            Check("cdx_short_array", d.charBestStage.Length == Content.Chars.Count && d.charBestStage[0] == 2 && d.charBestStage[1] == 3, string.Join(",", d.charBestStage));
            Platform.Services.DeleteKey(k);
            d.NoteStage(0, 1); Check("cdx_note_keeps_max", d.charBestStage[0] == 2, d.charBestStage[0] + "");
            d.NoteStage(0, 3); Check("cdx_note_raises", d.charBestStage[0] == 3, d.charBestStage[0] + "");

            // profiles: each keeps its own, a reset clears it
            g.save.charBestStage[1] = 2; g.save.Save();
            var p2 = Profiles.Create(1, "Cdx", g.save);
            Check("cdx_profile_fresh", p2.charBestStage[1] == 0, "");
            g.save = Profiles.Switch(0, p2);
            Check("cdx_profile_kept", g.save.charBestStage[1] == 2, "");
            Profiles.Delete(1);
            g.save = Profiles.ResetProgress(g.save);
            Check("cdx_reset_clears", g.save.charBestStage[1] == 0 && SaveData.Load().charBestStage[1] == 0, "");

            // a boss kill records the stage at once, for the character played, and never lowers it
            var b = BossAt(new Vector2(0, 4), 0);
            b.Damage(b.hp + 1f, false, Vector2.zero, 0f, null); yield return new WaitForSeconds(0.2f);
            Check("cdx_boss_records", g.save.charBestStage[2] == 1 && g.save.charBestStage[0] == 0 && SaveData.Load().charBestStage[2] == 1, g.save.charBestStage[2] + "");
            g.save.charBestStage[2] = 3;
            yield return Arena(2, 5, 0); b = BossAt(new Vector2(0, 4), 0);
            b.Damage(b.hp + 1f, false, Vector2.zero, 0f, null); yield return new WaitForSeconds(0.2f);
            Check("cdx_boss_no_lower", g.save.charBestStage[2] == 3, g.save.charBestStage[2] + "");
            g.save.charBestStage[2] = 0; g.save.Save();
            Check("cdx_real_keys_untouched", PlayerPrefs.GetString("vk_save_v1", "#none") == realSave && PlayerPrefs.GetString("vk_profiles", "#none") == realIdx, "");
        }

        IEnumerator T_cdx_b_ui()
        {
            yield return Arena(0, 5, 0); var g = Game.I; var ui = g.ui; g.state = GState.Menu;   // PadNav only works on menu screens
            int lang0 = g.save.lang; ui.ApplyLanguage(Loc.VI); ui.HideHint(); yield return null;
            var s = g.save; int bw = s.bestWave, tk = s.totalKills, bl = s.bestLevel, wn = s.wins; var bs = (int[])s.charBestStage.Clone();
            s.bestWave = 0; s.totalKills = 0; s.bestLevel = 0; s.wins = 0; System.Array.Clear(s.charBestStage, 0, s.charBestStage.Length);
            var ch = Content.Chars; var f = ch[0].forms;

            ui.ShowCodexSkills(0); yield return null;
            Check("cdx_stage1_only", ui.screen == UIScreen.Codex && UiCount(ch[0].skill) >= 1 && UiCount("Dạng khởi đầu.") == 1 && UiCount("???") == 3 && UiCount(f[0].name) == 0 && UiCount(f[0].full) == 0 && UiCount(f[0].desc) == 0, UiCount("???") + "");
            s.charBestStage[0] = 1; ui.ShowCodexSkills(0); yield return null;
            Check("cdx_stage2_shown", UiCount(f[0].name) == 1 && UiCount(f[0].full) == 1 && UiCount("So với Giai I: " + f[0].desc) == 1, "");
            Check("cdx_stage3_hidden", UiCount("???") == 2 && UiCount(f[1].name) == 0 && UiCount(f[1].full) == 0 && UiCount(f[1].desc) == 0 && UiCount(f[2].name) == 0 && UiCount(f[2].full) == 0, UiCount("???") + "");
            s.charBestStage[0] = 3; ui.ShowCodexSkills(0); yield return null;
            Check("cdx_all_stages", UiCount("???") == 0 && UiCount(f[2].name) == 1 && UiCount(f[2].full) == 1 && UiCount("So với Giai III: " + f[2].desc) == 1, "");

            for (int i = 1; i < ch.Count; i++) Check("cdx_locked_hidden_" + i, BtnByLabel(ch[i].name) == null && UiCount(ch[i].skill) == 0, ch[i].name);
            ui.ShowCodexSkills(4); yield return null;
            Check("cdx_locked_arg_falls_back", UiCount(ch[4].skill) == 0 && UiCount(ch[0].skill) >= 1, "");

            // pad: first press lands on the shown character, down goes to the next one, A opens it; its own stages apply
            s.bestWave = 5; ui.ShowCodexSkills(0); yield return null;
            var nav = g.GetComponent<PadNav>(); nav.Hide(); nav.Forget(); nav.Navigate(Vector2.right); yield return null;
            Check("cdx_pad_default", SelLabel(nav) == ch[0].name, SelLabel(nav));
            nav.Navigate(Vector2.down); yield return null;
            Check("cdx_pad_next", SelLabel(nav) == ch[1].name, SelLabel(nav));
            nav.Submit(); yield return null;
            Check("cdx_pad_opens", UiCount(ch[1].skill) >= 1 && UiCount(ch[1].skillDesc) >= 1 && UiCount("???") == 3, UiCount("???") + "");
            nav.Navigate(Vector2.up); nav.Navigate(Vector2.up); yield return null;
            Check("cdx_pad_reaches_tabs", SelLabel(nav) == "Ghi Chép" || SelLabel(nav) == "Thần Thông", SelLabel(nav));

            // the tabs (a tap and a click are the same onClick)
            Click("Ghi Chép"); yield return null;
            Check("cdx_tab_records", ui.screen == UIScreen.Codex && UiCount("Đã ghi chép") == 1 && UiCount("VŨ KHÍ") == 1, "");
            Click("Thần Thông"); yield return null;
            Check("cdx_tab_skills", UiCount("Đã ghi chép") == 0 && UiCount("Dạng khởi đầu.") == 1, "");
            Click("Quay Lại"); yield return null;
            Check("cdx_back", ui.screen == UIScreen.Menu, ui.screen + "");

            s.bestWave = bw; s.totalKills = tk; s.bestLevel = bl; s.wins = wn; s.charBestStage = bs;
            ui.ApplyLanguage(lang0); ui.ShowMenu(); yield return null;
        }

        IEnumerator T_cdx_c_shots()
        {
            yield return Arena(0, 5, 0); var g = Game.I; var ui = g.ui; g.state = GState.Menu;
            var s = g.save; int bw = s.bestWave, lang0 = s.lang; var bs = (int[])s.charBestStage.Clone();
            s.bestWave = 5; s.charBestStage[0] = 1; s.charBestStage[1] = 3;
            int[] langs = { Loc.VI, Loc.EN, Loc.ZH }; string[] tag = { "vi", "en", "zh" };
            for (int i = 0; i < 3; i++)
            {
                ui.ApplyLanguage(langs[i]); ui.HideHint();
                ui.ShowCodexSkills(0); yield return new WaitForSecondsRealtime(0.5f); g.bot.Shot("cdx_kiem_" + tag[i]); yield return new WaitForSecondsRealtime(0.3f);
                ui.ShowCodexSkills(1); yield return new WaitForSecondsRealtime(0.5f); g.bot.Shot("cdx_dan_" + tag[i]); yield return new WaitForSecondsRealtime(0.3f);
            }
            ui.ApplyLanguage(Loc.VI); ui.ShowCodex(); yield return new WaitForSecondsRealtime(0.5f); g.bot.Shot("cdx_records"); yield return new WaitForSecondsRealtime(0.3f);
            s.bestWave = bw; s.charBestStage = bs; ui.ApplyLanguage(lang0); ui.ShowMenu(); yield return null;
        }
    }
}
