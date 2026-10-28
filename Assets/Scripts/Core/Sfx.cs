using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    public static class Sfx
    {
        static AudioSource[] srcs;
        static int next;
        static AudioSource music;
        static readonly Dictionary<string, float> last = new Dictionary<string, float>();
        static readonly Dictionary<string, AudioClip> tracks = new Dictionary<string, AudioClip>();
        static string currentTrack;
        public static string CurrentTrack => currentTrack;
        static float musicTarget = 1f, musicFade = 1f;

        public static void Init(GameObject host)
        {
            srcs = new AudioSource[24];
            for (int i = 0; i < srcs.Length; i++)
            {
                srcs[i] = host.AddComponent<AudioSource>();
                srcs[i].playOnAwake = false;
            }
            music = host.AddComponent<AudioSource>();
            music.loop = true; music.playOnAwake = false;
        }

        public static void Play(string name, float vol = 1f, float pitchVar = 0.08f, float pitch = 1f, float throttle = 0.035f)
        {
            if (srcs == null) return;
            float now = Time.unscaledTime;
            if (last.TryGetValue(name, out float t) && now - t < throttle) return;
            last[name] = now;
            var clip = Res.Clip(name);
            if (clip == null) return;
            var s = srcs[next]; next = (next + 1) % srcs.Length;
            s.clip = clip;
            s.volume = vol * Game.I.save.sfxVol;
            s.pitch = pitch * (1f + Random.Range(-pitchVar, pitchVar));
            s.Play();
        }

        public static void Preload()
        {
            Get("menu"); Get("battle"); Get("boss"); Get("boss_climax");
            Res.RegisterClip("sk_chime", Synth.Chime()); Res.RegisterClip("sk_boom", Synth.Boom());
            Res.RegisterClip("sk_whoosh", Synth.Whoosh()); Res.RegisterClip("sk_choir", Synth.Choir());
        }

        static AudioClip Get(string track)
        {
            if (!tracks.TryGetValue(track, out var clip))
            {
                switch (track)
                {
                    case "menu": clip = Synth.Track(7, 76, 0); break;
                    case "boss": clip = Synth.Track(23, 128, 2); break;
                    case "boss_climax": clip = Synth.Track(23, 150, 3); break;
                    case "battle": clip = Synth.Track(11, 104, 1); break;
                    default: return null;   // unknown track names are ignored by Music
                }
                tracks[track] = clip;
            }
            return clip;
        }

        public static void Music(string track)
        {
            if (music == null || currentTrack == track) return;
            var clip = Get(track);
            if (clip == null) return;
            currentTrack = track;
            music.clip = clip;
            music.Play();
            musicFade = 0f;
        }

        /// <summary>Duck music (e.g. on pause / between waves).</summary>
        public static void MusicLevel(float target) => musicTarget = target;

        public static void Tick(float dt)
        {
            if (music == null) return;
            musicFade = Mathf.MoveTowards(musicFade, 1f, dt * 0.7f);
            float tgt = Game.I.save.musicVol * musicTarget * 0.55f * musicFade;
            music.volume = Mathf.MoveTowards(music.volume, tgt, dt * 1.5f);
        }
    }

    /// <summary>
    /// Procedural music: A-minor pentatonic, Karplus-Strong plucked strings (guzheng-like),
    /// bass, drone and drums. Repeating motifs make it hum-able.
    /// </summary>
    public static class Synth
    {
        const int SR = 22050;

        static AudioClip Clip(string name, float[] buf)
        {
            var c = AudioClip.Create(name, buf.Length, 1, SR, false);
            c.SetData(buf, 0);
            return c;
        }

        /// <summary>Pentatonic bell: three partials with an exponential tail.</summary>
        public static AudioClip Chime()
        {
            var b = new float[SR];
            float[] notes = { 659.3f, 783.99f, 987.77f };
            for (int n = 0; n < notes.Length; n++)
                for (int i = 0; i < SR / 2; i++)
                {
                    int k = i + n * SR / 14; if (k >= b.Length) break;
                    float t = i / (float)SR;
                    b[k] += (Mathf.Sin(6.2832f * notes[n] * t) + 0.4f * Mathf.Sin(6.2832f * notes[n] * 2.76f * t)) * Mathf.Exp(-t * 7f) * 0.25f;
                }
            return Clip("sk_chime", b);
        }

        /// <summary>Low explosion: falling sine plus a decaying noise burst.</summary>
        public static AudioClip Boom()
        {
            var b = new float[SR * 3 / 4]; var rng = new System.Random(5); float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR; ph += 6.2832f * (110f * Mathf.Exp(-t * 4f) + 30f) / SR;
                b[i] = (Mathf.Sin(ph) * 0.7f + ((float)rng.NextDouble() * 2f - 1f) * 0.35f * Mathf.Exp(-t * 14f)) * Mathf.Exp(-t * 5f);
            }
            return Clip("sk_boom", b);
        }

        /// <summary>Rising filtered-noise sweep.</summary>
        public static AudioClip Whoosh()
        {
            var b = new float[SR / 2]; var rng = new System.Random(9); float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)b.Length; float a = Mathf.Lerp(0.05f, 0.5f, t);
                lp += a * (((float)rng.NextDouble() * 2f - 1f) - lp);
                b[i] = lp * Mathf.Sin(t * Mathf.PI) * 1.6f;
            }
            return Clip("sk_whoosh", b);
        }

        /// <summary>Long sustained fifth with slow vibrato.</summary>
        public static AudioClip Choir()
        {
            var b = new float[SR * 2];
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR; float env = Mathf.Clamp01(t / 0.4f) * Mathf.Exp(-Mathf.Max(0, t - 0.8f) * 2.2f);
                float vib = 1f + 0.006f * Mathf.Sin(6.2832f * 5.2f * t);
                b[i] = (Mathf.Sin(6.2832f * 220f * vib * t) + 0.6f * Mathf.Sin(6.2832f * 330f * vib * t) + 0.3f * Mathf.Sin(6.2832f * 440f * vib * t)) * env * 0.22f;
            }
            return Clip("sk_choir", b);
        }

        public static AudioClip Track(int seed, float bpm, int intensity)
        {
            var rng = new System.Random(seed);
            int bars = 16, stepsPerBar = 8;
            float stepDur = 60f / bpm / 2f;
            int stepLen = Mathf.RoundToInt(stepDur * SR);
            int total = stepLen * stepsPerBar * bars;
            var buf = new float[total];

            int[] roots = { 45, 41, 43, 40, 45, 41, 43, 45 };          // A F G E A F G A (per 2 bars)
            int[] scale = { 57, 60, 62, 64, 67, 69, 72, 74, 76, 79, 81 };

            // motifs: 4 two-bar phrases
            var motifs = new int[4][];
            for (int m = 0; m < 4; m++)
            {
                motifs[m] = new int[16];
                int idx = 4 + rng.Next(3);
                float density = intensity == 0 ? 0.38f : intensity == 1 ? 0.55f : intensity == 2 ? 0.65f : 0.72f;
                for (int s = 0; s < 16; s++)
                {
                    bool strong = s % 4 == 0;
                    if (rng.NextDouble() < (strong ? density + 0.3f : density))
                    {
                        idx = Mathf.Clamp(idx + rng.Next(-2, 3), 0, scale.Length - 1);
                        motifs[m][s] = scale[idx];
                    }
                    else motifs[m][s] = 0;
                }
                motifs[m][0] = scale[Mathf.Clamp(idx, 2, 7)];
            }
            int[] phraseOrder = { 0, 1, 0, 2, 0, 1, 3, 2 };

            for (int bar = 0; bar < bars; bar++)
            {
                int phrase = bar / 2;
                int root = roots[phrase % roots.Length];
                var motif = motifs[phraseOrder[phrase % phraseOrder.Length]];
                for (int s = 0; s < stepsPerBar; s++)
                {
                    int step = bar * stepsPerBar + s;
                    int pos = step * stepLen;
                    int ms = (bar % 2) * 8 + s;

                    // melody
                    int note = motif[ms];
                    if (note > 0)
                    {
                        float vel = (s % 2 == 0 ? 0.5f : 0.36f);
                        Pluck(buf, pos, Freq(note), vel, 1.3f, rng);
                        if (intensity == 0 && rng.NextDouble() < 0.25) Pluck(buf, pos + stepLen / 2, Freq(note + 12), 0.15f, 0.9f, rng);
                    }
                    // bass
                    bool bassHit = intensity == 0 ? s == 0 : intensity == 1 ? s % 4 == 0 || s == 6 : s % 2 == 0;
                    if (bassHit) Bass(buf, pos, Freq(root), intensity == 0 ? 0.35f : 0.42f, stepDur * (intensity == 0 ? 7 : 3));
                    // drums
                    if (intensity >= 1)
                    {
                        if (s % 4 == 0 || (intensity >= 2 && s % 2 == 0) || (intensity == 3 && s % 4 == 3)) Kick(buf, pos, intensity >= 2 ? 0.75f : 0.6f);
                        if (s % 2 == 1) Hat(buf, pos, 0.08f, rng);
                        if ((s == 4 || (intensity == 3 && s == 12)) && (bar % 2 == 1 || intensity >= 2)) Snare(buf, pos, 0.25f, rng);
                    }
                    else if (s == 0 && bar % 4 == 0) Kick(buf, pos, 0.25f);
                }
                // drone pad per bar
                Pad(buf, bar * stepsPerBar * stepLen, stepsPerBar * stepLen, Freq(root + 12), Freq(root + 19), intensity == 0 ? 0.07f : 0.05f);
            }

            // soft clip + normalize
            float peak = 0.001f;
            for (int i = 0; i < total; i++) { buf[i] = (float)System.Math.Tanh(buf[i] * 1.2f); peak = Mathf.Max(peak, Mathf.Abs(buf[i])); }
            float g = 0.85f / peak;
            for (int i = 0; i < total; i++) buf[i] *= g;

            var clip = AudioClip.Create("music_" + seed, total, 1, SR, false);
            clip.SetData(buf, 0);
            return clip;
        }

        static float Freq(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        static void Add(float[] buf, int i, float v) { buf[i % buf.Length] += v; }

        static void Pluck(float[] buf, int start, float f, float vel, float dur, System.Random rng)
        {
            int n = Mathf.Max(2, Mathf.RoundToInt(SR / f));
            var ring = new float[n];
            for (int i = 0; i < n; i++) ring[i] = (float)(rng.NextDouble() * 2 - 1);
            int len = Mathf.RoundToInt(dur * SR);
            int p = 0;
            float decay = 0.996f;
            for (int i = 0; i < len; i++)
            {
                int q = (p + 1) % n;
                float v = ring[p];
                ring[p] = (ring[p] + ring[q]) * 0.5f * decay;
                p = q;
                float env = i < 40 ? i / 40f : 1f;
                Add(buf, start + i, v * vel * env);
            }
        }

        static void Bass(float[] buf, int start, float f, float vel, float dur)
        {
            int len = Mathf.RoundToInt(dur * SR);
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / SR;
                float env = Mathf.Exp(-t * 2.2f) * Mathf.Min(1f, i / 200f);
                float ph = 2 * Mathf.PI * f * t;
                Add(buf, start + i, (Mathf.Sin(ph) + 0.25f * Mathf.Sin(ph * 2) + 0.1f * Mathf.Sin(ph * 3)) * vel * env);
            }
        }

        static void Kick(float[] buf, int start, float vel)
        {
            int len = (int)(0.28f * SR);
            float ph = 0;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / SR;
                float f = 45f + 110f * Mathf.Exp(-t * 30f);
                ph += 2 * Mathf.PI * f / SR;
                Add(buf, start + i, Mathf.Sin(ph) * Mathf.Exp(-t * 12f) * vel);
            }
        }

        static void Hat(float[] buf, int start, float vel, System.Random rng)
        {
            int len = (int)(0.05f * SR);
            float prev = 0;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / SR;
                float n = (float)(rng.NextDouble() * 2 - 1);
                float hp = n - prev; prev = n;
                Add(buf, start + i, hp * Mathf.Exp(-t * 70f) * vel);
            }
        }

        static void Snare(float[] buf, int start, float vel, System.Random rng)
        {
            int len = (int)(0.18f * SR);
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / SR;
                float n = (float)(rng.NextDouble() * 2 - 1);
                float tone = Mathf.Sin(2 * Mathf.PI * 190f * t) * Mathf.Exp(-t * 25f);
                Add(buf, start + i, (n * Mathf.Exp(-t * 22f) * 0.7f + tone * 0.5f) * vel);
            }
        }

        static void Pad(float[] buf, int start, int len, float f1, float f2, float vel)
        {
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / SR;
                float env = Mathf.Sin(Mathf.PI * i / len);
                float v = Mathf.Sin(2 * Mathf.PI * f1 * t) + 0.7f * Mathf.Sin(2 * Mathf.PI * f2 * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * f1 * 1.003f * t);
                Add(buf, start + i, v * vel * env);
            }
        }
    }
}
