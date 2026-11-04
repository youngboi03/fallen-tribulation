using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace VK
{
    /// <summary>Anonymous statistics (prefix "tel"): consent gating, payload shape, batching, opt-out, first-launch dialog, run events. Never touches the network.</summary>
    public partial class SkillTest
    {
        [Serializable] class TelEv { public string @event, distinct_id; }
        [Serializable] class TelBatch { public string api_key; public TelEv[] batch; }

        string telConsent, telAnon, telKey; bool telAllow; Action<string, string> telSender;
        List<string> telSent = new List<string>(), telUrls = new List<string>();

        void TelBegin()
        {
            telConsent = Platform.Services.LoadString("vk_telemetry"); telAnon = Platform.Services.LoadString("vk_anon");
            telKey = Telemetry.ProjectKey; telAllow = Telemetry.AllowInAuto; telSender = Telemetry.Sender;
            telSent = new List<string>(); telUrls = new List<string>();
            Telemetry.Sender = (u, j) => { telUrls.Add(u); telSent.Add(j); };
            Telemetry.ProjectKey = "phc_test"; Telemetry.AllowInAuto = true;
            Platform.Services.DeleteKey("vk_telemetry"); Platform.Services.DeleteKey("vk_anon");
            Telemetry.ResetForTest();
        }

        void TelEnd()
        {
            Telemetry.Sender = telSender; Telemetry.ProjectKey = telKey; Telemetry.AllowInAuto = telAllow;
            if (string.IsNullOrEmpty(telConsent)) Platform.Services.DeleteKey("vk_telemetry"); else Platform.Services.SaveString("vk_telemetry", telConsent);
            if (string.IsNullOrEmpty(telAnon)) Platform.Services.DeleteKey("vk_anon"); else Platform.Services.SaveString("vk_anon", telAnon);
            Telemetry.ResetForTest();
        }

        bool TelSent(string needle) { foreach (var j in telSent) if (j.Contains(needle)) return true; return false; }
        static bool HasText(string s) { foreach (var t in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None)) if (t.text.Contains(Loc.Tr(s))) return true; return false; }

        IEnumerator T_tel_core()
        {
            TelBegin();
            var culture = CultureInfo.CurrentCulture;
            try
            {
                Check("tel_unasked", Telemetry.Consent == -1 && Telemetry.AnonId == "" && !Telemetry.Active, Telemetry.Consent + "");
                Telemetry.Event("x", "a", 1); Telemetry.Flush();
                Check("tel_unasked_silent", Telemetry.Pending == 0 && telSent.Count == 0, "");

                Telemetry.SetConsent(true);
                string id = Telemetry.AnonId; bool hex = id.Length == 32; foreach (char c in id) hex &= Uri.IsHexDigit(c);
                Check("tel_id_hex", hex && Telemetry.Consent == 1 && Platform.Services.LoadString("vk_telemetry") == "1" && Telemetry.AnonId == id, id);

                CultureInfo.CurrentCulture = new CultureInfo("vi-VN");   // a decimal comma must never leak into the JSON
                Telemetry.Event("run_end", "wave", 7, "win", false, "hp", 0.5f, "weapons", new[] { "a:1", "b:2" }, "note", "he said \"hi\"\n");
                Check("tel_queued", Telemetry.Pending == 1, Telemetry.Pending + "");
                Telemetry.Flush();
                CultureInfo.CurrentCulture = culture;
                string j = telSent.Count > 0 ? telSent[0] : "";
                var b = telSent.Count > 0 ? JsonUtility.FromJson<TelBatch>(j) : null;
                Check("tel_sent_once", telSent.Count == 1 && telUrls[0] == "https://eu.i.posthog.com/batch/" && Telemetry.Pending == 0, telSent.Count + " " + (telUrls.Count > 0 ? telUrls[0] : ""));
                Check("tel_json_parses", b != null && b.api_key == "phc_test" && b.batch != null && b.batch.Length == 1 && b.batch[0].@event == "run_end" && b.batch[0].distinct_id == id, j);
                Check("tel_json_props", j.Contains("\"$process_person_profile\":false") && j.Contains("\"$geoip_disable\":true") && j.Contains("\"wave\":7") && j.Contains("\"win\":false")
                    && j.Contains("\"hp\":0.5") && j.Contains("\"weapons\":[\"a:1\",\"b:2\"]") && j.Contains("\"note\":\"he said \\\"hi\\\"\\n\""), j);
                Check("tel_json_common", j.Contains("\"version\":") && j.Contains("\"platform\":") && j.Contains("\"lang\":"), "");
                Check("tel_json_no_pii", !j.Contains("\"$ip\"") && !j.Contains("email") && !j.Contains("device"), "");

                telSent.Clear(); telUrls.Clear();
                for (int i = 0; i < 120; i++) Telemetry.Event("e", "i", i);
                Telemetry.Flush();
                int total = 0, maxb = 0; foreach (var s in telSent) { var bb = JsonUtility.FromJson<TelBatch>(s); total += bb.batch.Length; maxb = Mathf.Max(maxb, bb.batch.Length); }
                Check("tel_batching", telSent.Count == 3 && total == 120 && maxb <= 50, telSent.Count + " " + total + " " + maxb);

                for (int i = 0; i < 300; i++) Telemetry.Event("e", "i", i);
                Check("tel_queue_cap", Telemetry.Pending == 200, Telemetry.Pending + "");
                Telemetry.Flush();

                Telemetry.Sender = (u, jj) => { throw new Exception("offline"); };
                bool threw = false;
                Telemetry.Event("e"); try { Telemetry.Flush(); } catch { threw = true; }
                Check("tel_failure_swallowed", !threw && Telemetry.Pending == 0, "");
                Telemetry.Sender = (u, jj) => { telUrls.Add(u); telSent.Add(jj); };

                Telemetry.Event("pending_at_optout"); Telemetry.Event("pending_at_optout");
                Telemetry.SetConsent(false);
                Check("tel_optout", Telemetry.Consent == 0 && Telemetry.AnonId == "" && Telemetry.Pending == 0 && Platform.Services.LoadString("vk_telemetry") == "0" && Platform.Services.LoadString("vk_anon") == "", "");
                Telemetry.Event("after_optout");
                Check("tel_optout_silent", Telemetry.Pending == 0 && !Telemetry.NeedsAsk, "");
                Telemetry.SetConsent(true);
                Check("tel_new_id_after_reconsent", Telemetry.AnonId.Length == 32 && Telemetry.AnonId != id, Telemetry.AnonId);

                Telemetry.AllowInAuto = false;   // this run is a bot/test run: nothing may be queued even with consent
                Telemetry.Event("bot");
                Check("tel_bot_never_sends", !Telemetry.Active && Telemetry.Pending == 0, "");
                Telemetry.AllowInAuto = true; Telemetry.ProjectKey = "";
                Telemetry.Event("nokey");
                Check("tel_no_key_off", !Telemetry.Active && Telemetry.Pending == 0 && !Telemetry.NeedsAsk, "");
            }
            finally { CultureInfo.CurrentCulture = culture; TelEnd(); }
            yield return null;
        }

        IEnumerator T_tel_ui()
        {
            TelBegin();
            yield return Arena(0, 5, 0); var g = Game.I; var ui = g.ui; g.state = GState.Menu;
            int lang0 = g.save.lang; ui.ApplyLanguage(Loc.VI); yield return null;
            try
            {
                Check("tel_ask_needed", Telemetry.NeedsAsk, "");
                ui.ShowMenu(); yield return null;
                Check("tel_ask_dialog", HasText("Giúp cải thiện game?") && BtnByLabel("Đồng ý gửi") != null && BtnByLabel("Không, cảm ơn") != null && !HasText("Nhập Đạo"), "");
                Click("Không, cảm ơn"); yield return null;
                Check("tel_ask_no", Telemetry.Consent == 0 && Telemetry.AnonId == "" && HasText("Nhập Đạo") && !Telemetry.NeedsAsk, Telemetry.Consent + "");
                Check("tel_support_button", BtnByLabel("Ủng Hộ Game") != null, "");

                ui.ShowSettings(false); yield return null;
                Check("tel_settings_row", HasText("Thống kê ẩn danh") && HasText("Tắt"), "");

                Telemetry.ResetForTest(); Platform.Services.DeleteKey("vk_telemetry");
                ui.ShowMenu(); yield return null;
                Click("Đồng ý gửi"); yield return null;
                Check("tel_ask_yes", Telemetry.Consent == 1 && Telemetry.AnonId.Length == 32 && HasText("Nhập Đạo") && Telemetry.Pending == 1, Telemetry.Consent + " " + Telemetry.Pending);
                Telemetry.Flush();
                Check("tel_session_start", TelSent("\"event\":\"session_start\"") && TelSent("\"input\":"), "");
            }
            finally { ui.ApplyLanguage(lang0); TelEnd(); }
        }

        IEnumerator T_tel_run()
        {
            TelBegin();
            try
            {
                Telemetry.SetConsent(true);
                yield return Arena(0, 5, 0); var g = Game.I;
                Telemetry.Flush();
                Check("tel_run_start", TelSent("\"event\":\"run_start\"") && TelSent("\"char\":0") && TelSent("\"kiep\":0"), "");
                telSent.Clear();
                g.run.wave = 7; g.run.level = 9; Telemetry.Cause = "boss:noc_phun";
                g.FinishRun(false); yield return null;
                string all = string.Join("\n", telSent);
                Check("tel_run_end", all.Contains("\"event\":\"run_end\"") && all.Contains("\"win\":false") && all.Contains("\"abandoned\":false") && all.Contains("\"cause\":\"boss:noc_phun\"") && all.Contains("\"wave\":7") && all.Contains("\"level\":9"), all);
                Check("tel_run_end_fields", all.Contains("\"weapons\":[") && all.Contains("\"items\":[") && all.Contains("\"fps_avg\":") && all.Contains("\"slow_pct\":") && all.Contains("\"time_s\":") && Telemetry.Pending == 0, "");

                yield return Arena(0, 3, 0); telSent.Clear();
                g.Abandon(); yield return null;
                all = string.Join("\n", telSent);
                Check("tel_abandon", all.Contains("\"abandoned\":true") && all.Contains("\"cause\":\"abandon\"") && all.Contains("\"wave\":3"), all);
            }
            finally { TelEnd(); }
        }
    }
}
