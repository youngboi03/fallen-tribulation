using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace VK
{
    /// <summary>
    /// Anonymous play statistics sent to PostHog (EU cloud). Nothing is queued or sent unless the player said yes
    /// (Consent == 1), ProjectKey is set, and this is not a bot/test run. No name, e-mail or device id is collected:
    /// only a random id made when the player opts in, and it is deleted again when they opt out.
    /// </summary>
    public static class Telemetry
    {
        public const string Host = "https://eu.i.posthog.com";
        /// <summary>PostHog project key (public and write-only by design). Empty = telemetry off.</summary>
        public static string ProjectKey = "phc_u6nEVMtKCJcmXq5coRd5AJy3sjERj36jjgL4DziJQwuF";
        /// <summary>Test hook (url, json). Null = real HTTP POST through a hidden runner object.</summary>
        public static Action<string, string> Sender;
        /// <summary>Tests only: bots, the editor and -skilltest runs never send otherwise.</summary>
        public static bool AllowInAuto;
        /// <summary>What last hurt the player (boss move, enemy id, "status:..."): reported when a run ends in death.</summary>
        public static string Cause = "";

        const string ConsentKey = "vk_telemetry", IdKey = "vk_anon";
        const int MaxQueue = 200, BatchSize = 50;
        const float FlushEvery = 20f;

        static readonly List<string> queue = new List<string>();
        static int consent = -2;   // -2 = not read yet
        static string anon;
        static bool botChecked, isBot, quitHooked, sessionSent;
        static Runner runner;

        // frame timing of the current run (reported in run_end)
        static float frameSum; static int frameN, slowN;

        /// <summary>-1 not asked yet, 0 declined, 1 accepted. Stored on the device, not in a profile.</summary>
        public static int Consent
        {
            get
            {
                if (consent == -2)
                {
                    string s = Platform.Services.LoadString(ConsentKey);
                    consent = s == "1" ? 1 : s == "0" ? 0 : -1;
                }
                return consent;
            }
        }

        public static void SetConsent(bool on)
        {
            consent = on ? 1 : 0;
            Platform.Services.SaveString(ConsentKey, on ? "1" : "0");
            if (on) { anon = Guid.NewGuid().ToString("N"); Platform.Services.SaveString(IdKey, anon); }
            else { anon = ""; Platform.Services.DeleteKey(IdKey); queue.Clear(); }
        }

        /// <summary>The random install id (32 hex chars) while the player has opted in, else "".</summary>
        public static string AnonId
        {
            get
            {
                if (Consent != 1) return "";
                if (string.IsNullOrEmpty(anon)) anon = Platform.Services.LoadString(IdKey);
                if (string.IsNullOrEmpty(anon)) { anon = Guid.NewGuid().ToString("N"); Platform.Services.SaveString(IdKey, anon); }
                return anon;
            }
        }

        public static int Pending => queue.Count;

        /// <summary>True when the first-launch question must be shown: a key is set, the player never answered, and this is not a bot run.</summary>
        public static bool NeedsAsk => Consent == -1 && !string.IsNullOrEmpty(ProjectKey) && (AllowInAuto || !IsBot);

        public static bool Active => Consent == 1 && !string.IsNullOrEmpty(ProjectKey) && (AllowInAuto || !IsBot);

        static bool IsBot
        {
            get
            {
                if (!botChecked)
                {
                    botChecked = true;
                    isBot = Application.isEditor;
                    foreach (var a in Environment.GetCommandLineArgs()) if (a == "-autotest" || a == "-skilltest") isBot = true;
                }
                return isBot;
            }
        }

        /// <summary>Clears the queue and the cached consent (tests).</summary>
        public static void ResetForTest() { queue.Clear(); consent = -2; anon = null; sessionSent = false; Cause = ""; }

        // ------------------------------------------------------------------ events
        /// <summary>Queues an event. kv alternates key, value; values: string, bool, numbers, string[] or List of string.</summary>
        public static void Event(string name, params object[] kv)
        {
            if (!Active) return;
            var sb = new StringBuilder(256);
            sb.Append("{\"event\":").Append(Q(name)).Append(",\"distinct_id\":").Append(Q(AnonId))
              .Append(",\"timestamp\":").Append(Q(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)))
              .Append(",\"properties\":{\"$process_person_profile\":false,\"$geoip_disable\":true")
              .Append(",\"version\":").Append(Q(Application.version))
              .Append(",\"platform\":").Append(Q(Application.platform.ToString()))
              .Append(",\"lang\":").Append(Q(Loc.Lang == Loc.VI ? "vi" : Loc.Lang == Loc.EN ? "en" : "zh"));
            for (int i = 0; kv != null && i + 1 < kv.Length; i += 2) sb.Append(',').Append(Q(kv[i] as string ?? "?")).Append(':').Append(V(kv[i + 1]));
            sb.Append("}}");
            if (queue.Count >= MaxQueue) queue.RemoveAt(0);
            queue.Add(sb.ToString());
            if (Sender == null) EnsureRunner();
        }

        /// <summary>Once per launch (after the player has said yes): platform, language and input kind.</summary>
        public static void StartSession()
        {
            if (sessionSent || !Active) return;
            sessionSent = true;
            Event("session_start", "input", Platform.Touch ? "touch" : "kbm");
        }

        /// <summary>Sends everything queued, up to BatchSize events per request. Never throws; a failed send drops its events.</summary>
        public static void Flush()
        {
            while (queue.Count > 0)
            {
                if (!Active) { queue.Clear(); return; }
                int n = Math.Min(BatchSize, queue.Count);
                var body = "{\"api_key\":" + Q(ProjectKey) + ",\"batch\":[" + string.Join(",", queue.GetRange(0, n)) + "]}";
                queue.RemoveRange(0, n);
                try
                {
                    string url = Host + "/batch/";
                    if (Sender != null) Sender(url, body);
                    else if (EnsureRunner()) runner.StartCoroutine(Post(url, body));
                }
                catch (Exception e) { Debug.Log("[TEL] send failed: " + e.Message); }
            }
        }

        // ------------------------------------------------------------------ run helpers
        public static void RunStart() { frameSum = 0f; frameN = 0; slowN = 0; Cause = ""; }

        /// <summary>Called every playing frame: average fps and the share of slow frames (> 33 ms) for run_end.</summary>
        public static void Frame(float dt)
        {
            if (dt <= 0f || dt > 1f) return;
            frameSum += dt; frameN++;
            if (dt > 0.0334f) slowN++;
        }

        public static float AvgFps => frameSum > 0f ? frameN / frameSum : 0f;
        public static float SlowPct => frameN > 0 ? 100f * slowN / frameN : 0f;

        // ------------------------------------------------------------------ transport
        static IEnumerator Post(string url, string body)
        {
            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 10;
                yield return req.SendWebRequest();   // fire and forget: no retry; the result is only logged
                Debug.Log("[TEL] POST " + (req.result == UnityWebRequest.Result.Success ? req.responseCode.ToString() : req.result + " " + req.error));
            }
        }

        static bool EnsureRunner()
        {
            if (!Application.isPlaying) return false;
            if (!quitHooked) { quitHooked = true; Application.quitting += OnQuit; }
            if (runner != null) return true;
            var go = new GameObject("VKTelemetry") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
            return true;
        }

        static void OnQuit()
        {
            var g = Game.I;
            if (Active && g != null && g.run != null && (g.state == GState.Playing || g.state == GState.Between))
                Event("run_end", "abandoned", true, "cause", "quit", "wave", g.run.wave, "level", g.run.level, "kiep", g.run.kiep, "time_s", Mathf.RoundToInt(g.run.playTime));
            Flush();
        }

        class Runner : MonoBehaviour
        {
            float t;
            void Update()
            {
                t += Time.unscaledDeltaTime;
                if (t < FlushEvery) return;
                t = 0f;
                if (queue.Count > 0) Flush();
            }
        }

        // ------------------------------------------------------------------ JSON (no library: JsonUtility cannot write free-form objects)
        static string V(object v)
        {
            switch (v)
            {
                case null: return "null";
                case string s: return Q(s);
                case bool b: return b ? "true" : "false";
                case float f: return float.IsNaN(f) || float.IsInfinity(f) ? "null" : f.ToString("0.####", CultureInfo.InvariantCulture);
                case double d: return double.IsNaN(d) || double.IsInfinity(d) ? "null" : d.ToString("0.####", CultureInfo.InvariantCulture);
                case IEnumerable<string> list:
                    var sb = new StringBuilder("[");
                    bool first = true;
                    foreach (var x in list) { if (!first) sb.Append(','); first = false; sb.Append(Q(x)); }
                    return sb.Append(']').ToString();
                case IConvertible c: return Convert.ToString(c, CultureInfo.InvariantCulture);
                default: return Q(v.ToString());
            }
        }

        static string Q(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2).Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c); break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
