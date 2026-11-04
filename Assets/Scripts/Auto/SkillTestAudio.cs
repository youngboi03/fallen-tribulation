using System.Collections;
using UnityEngine;

namespace VK
{
    /// <summary>Audio (prefix "audio"): every sound loads, each boss has one signature on a real move, sounds land on the frame of the action.</summary>
    public partial class SkillTest
    {
        static readonly string[] AudioNames =
        {
            "hit", "dash", "swing", "shoot", "zap", "heavy", "sk_dan", "sk_loi", "sk_the", "sk_phu", "sk_ma",
            "ult_b1", "ult_b1b", "ult_b2", "ult_b2b", "ult_b3", "ult_b4", "bell", "sk_chime", "breakthrough",
            "coin", "pickup", "tick", "click", "confirm", "error", "open", "pluck",
        };

        /// <summary>Milliseconds until the clip first reaches half of its peak.</summary>
        static float AttackMs(AudioClip c)
        {
            var d = new float[c.samples * c.channels];
            c.GetData(d, 0);
            float peak = 0f;
            foreach (var x in d) peak = Mathf.Max(peak, Mathf.Abs(x));
            for (int i = 0; i < d.Length; i++)
                if (Mathf.Abs(d[i]) >= peak * 0.5f) return i / (float)c.channels / c.frequency * 1000f;
            return 0f;
        }

        IEnumerator T_audio_clips()
        {
            yield return Arena(0, 1, 0);
            foreach (var n in AudioNames)
            {
                var c = Res.Clip(n);
                Check("audio_clip_" + n, c != null, "");
                // the meteor rises on purpose and the code starts it early; everything else must hit at once
                if (c != null && n != "ult_b2")
                    Check("audio_attack_" + n, AttackMs(c) <= 200f, AttackMs(c).ToString("0") + " ms");
            }
            foreach (var t in new[] { "menu", "battle", "boss_climax" })
                Check("audio_track_" + t, Resources.Load<AudioClip>("Music/bgm_" + t) != null, "");
        }

        IEnumerator T_audio_short_sounds_instant()   // the sounds tied to a single input or hit have no lead-in at all
        {
            yield return Arena(0, 1, 0);
            foreach (var n in new[] { "hit", "swing", "shoot", "zap", "heavy", "coin", "pickup", "tick", "click", "confirm", "error", "open" })
            {
                var c = Res.Clip(n);
                Check("audio_instant_" + n, c != null && AttackMs(c) <= 60f, c == null ? "null" : AttackMs(c).ToString("0") + " ms");
            }
        }

        IEnumerator T_audio_fits_the_effect()   // a sound must not outlive its visual effect (0.4-0.6 s for skills)
        {
            yield return Arena(0, 1, 0);
            foreach (var n in new[] { "sk_dan", "sk_loi", "sk_the", "sk_phu", "sk_ma" })
            {
                var c = Res.Clip(n);
                Check("audio_len_" + n, c != null && c.length <= 0.85f, c == null ? "null" : c.length.ToString("0.00") + " s");
            }
            foreach (var n in new[] { "ult_b1b", "ult_b2b", "ult_b4" })
            {
                var c = Res.Clip(n);
                Check("audio_len_" + n, c != null && c.length <= 0.9f, c == null ? "null" : c.length.ToString("0.00") + " s");
            }
        }

        IEnumerator T_audio_signatures()
        {
            yield return Arena(0, 1, 0);
            foreach (var kit in Content.Kits)
            {
                int n = 0;
                foreach (var m in kit.moves) if (BossMoves.Signature.TryGetValue(m.id, out var clip) && Res.Clip(clip) != null) n++;
                Check("audio_signature_" + kit.bossId, n == 1, n.ToString());
            }
        }
    }
}
