using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VK
{
    /// <summary>End-of-run cutscenes: "Thân Tử Đạo Tiêu" (defeat) and "Phi Thăng" (victory). Runs on unscaled time with
    /// Time.timeScale eased down; any key or click jumps to the summary. Abort() restores everything when the player leaves.</summary>
    public class EndSeq : MonoBehaviour
    {
        public bool Active { get; private set; }
        /// <summary>The bot skips the cutscene so probes stay fast; scenario tests set this to watch it.</summary>
        public bool testMode;

        static readonly string[] LoseSub = { "Đạo tâm tan vỡ", "Hồn về cát bụi", "Tiên lộ đứt đoạn", "Kiếp nạn chưa qua" };
        static readonly Color Gold = new Color(1f, 0.85f, 0.4f), Red = new Color(0.75f, 0.88f, 1f);   // defeat accent: pale light, the hero returns to heaven and earth
        static Sprite vig;
        Coroutine co;
        RectTransform layer;
        float baseScale = 1f, baseZoom;

        static float Ease(float k) => 1f - (1f - k) * (1f - k);

        /// <summary>Soft dark edges, clear centre.</summary>
        static Sprite Vig()
        {
            if (vig != null) return vig;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = new Vector2((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f).magnitude;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1.3f, r)) * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            return vig = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        Image Full(string name, Color c, Sprite s = null)
        {
            var i = UIK.Img(layer, c, name); i.rectTransform.Fill(); i.raycastTarget = false;
            if (s != null) i.sprite = s;
            return i;
        }

        public void Play(bool win, Action done)
        {
            Abort();
            Active = true;
            var g = Game.I;
            baseScale = Time.timeScale; baseZoom = g.cam.orthographicSize;
            co = StartCoroutine(Run(win, done));
        }

        public void Abort()
        {
            if (co != null) StopCoroutine(co);
            co = null;
            if (Active) Restore();
            if (layer) Destroy(layer.gameObject);
            layer = null;
        }

        void Restore()
        {
            Active = false;
            Time.timeScale = baseScale;
            var g = Game.I;
            if (g.cam != null && baseZoom > 0f) g.cam.orthographicSize = baseZoom;
        }

        IEnumerator Run(bool win, Action done)
        {
            var g = Game.I; var p = g.player; var ui = g.ui;
            ui.HideHint();
            Vector2 hero0 = p.Pos;
            Color accent = win ? Gold : Red;
            layer = UIK.R(ui.Root, "endSeq").Fill();
            layer.SetSiblingIndex(ui.Root.Find("screen").GetSiblingIndex());   // under the summary screen
            var lcg = layer.gameObject.AddComponent<CanvasGroup>(); lcg.blocksRaycasts = false;
            var dark = Full("dark", Color.clear);
            var vg = Full("vig", Color.clear, Vig());
            var flash = Full("flash", Color.clear);

            // title: dark brush bands, big title, one dim line
            var box = UIK.R(layer, "title").Place(0.5f, 0.5f, 2000, 300, 0, 40);
            var band = UIK.Img(box, win ? new Color(0.09f, 0.06f, 0.02f, 0.9f) : new Color(0.02f, 0.03f, 0.07f, 0.92f), "card");
            band.rectTransform.Anchor(0, 0.5f, 1, 0.5f, -60, -115, -60, -115); band.rectTransform.pivot = new Vector2(0, 0.5f); band.raycastTarget = false;
            var band2 = UIK.Img(box, new Color(accent.r, accent.g, accent.b, 0.22f), "card");
            band2.rectTransform.Anchor(0, 0.5f, 1, 0.5f, -60, -78, -60, -102); band2.rectTransform.pivot = new Vector2(0, 0.5f); band2.raycastTarget = false;
            band2.rectTransform.localRotation = Quaternion.Euler(0, 0, 0.7f);
            var tcg = UIK.R(box, "t").Fill().gameObject.AddComponent<CanvasGroup>();
            var title = UIK.Title(tcg.transform, win ? "Phi Thăng Thành Tiên!" : "Thân Tử Đạo Tiêu", 130, win ? UIK.Gold : new Color(0.82f, 0.92f, 1f));
            title.rectTransform.Place(0.5f, 0.5f, 1800, 150, 0, 22);
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 50; title.resizeTextMaxSize = 130;
            var scg = UIK.R(box, "s").Fill().gameObject.AddComponent<CanvasGroup>();
            var subT = UIK.Txt(scg.transform, win ? "Vạn Kiếp đã tận, đạo quả viên mãn" : LoseSub[UnityEngine.Random.Range(0, LoseSub.Length)], 36, UIK.Dim, TextAnchor.MiddleCenter, FontStyle.Italic);
            subT.rectTransform.Place(0.5f, 0.5f, 1600, 50, 0, -62);

            // weapons: fall and shatter (defeat) or follow the hero upward (ascension)
            var ws = new List<WeaponInst>(); var p0 = new List<Vector3>(); var srs = new List<SpriteRenderer[]>(); var cs = new List<Color[]>();
            foreach (var w in g.run.weapons)
            {
                if (!w.View) continue;
                ws.Add(w); p0.Add(w.View.position);
                var a = w.View.GetComponentsInChildren<SpriteRenderer>(); srs.Add(a);
                var c = new Color[a.Length]; for (int j = 0; j < a.Length; j++) c[j] = a[j].color; cs.Add(c);
            }
            var done_ = new bool[ws.Count];

            Fx.Ring(hero0, accent, 0.3f, win ? 8f : 5f, 0.8f);
            if (win) Sfx.Play("breakthrough", 0.9f, 0f);
            float t = 0f, T = win ? 3.3f : 6.2f, tb = win ? 1.3f : 3.6f, nextPillar = 0f, acc = 0f; bool hit = false, beam = false;
            while (t < T)
            {
                float dt = Time.unscaledDeltaTime; t += dt;
                if (t > tb + 0.7f && (Input.anyKeyDown || Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))) break;
                float sc = win ? (t < 0.4f ? Mathf.Lerp(1f, 0.35f, t / 0.4f) : t < 1f ? 0.35f : Mathf.Lerp(0.35f, 1f, Mathf.Clamp01((t - 1f) / 0.6f)))
                               : (t < 0.5f ? Mathf.Lerp(1f, 0.4f, t / 0.5f) : t < 5.2f ? 0.4f : Mathf.Lerp(0.4f, 0f, Mathf.Clamp01((t - 5.2f) / 0.8f)));
                Time.timeScale = baseScale * sc;
                g.cam.orthographicSize = baseZoom / (1f + (win ? 0.25f : 0.4f) * Ease(Mathf.Clamp01(t / (win ? 2f : 3f))));
                dark.color = new Color(0, 0, 0, (win ? 0.2f : 0.5f) * Ease(Mathf.Clamp01(t / 1.2f)));
                float va = Ease(Mathf.Clamp01(t / 1.4f));
                vg.color = win ? new Color(1f, 0.78f, 0.3f, 0.6f * va) : new Color(0.02f, 0.04f, 0.12f, 0.95f * va);
                flash.color = win ? new Color(1f, 1f, 0.9f, 0.6f * Mathf.Clamp01(1f - t / 0.5f)) : new Color(0.9f, 0.95f, 1f, 0.8f * Mathf.Clamp01(1f - Mathf.Abs(t - 2.7f) / 0.35f));   // defeat: one soft white pulse as the last of the body turns to light

                float k = Mathf.Clamp01((t - (win ? 0.4f : 0.5f)) / (win ? 2f : 2.3f));
                p.EndPose(k, win);
                Vector2 hero = hero0 + (win ? Vector2.up * (k * k * 3.5f) : Vector2.zero);

                if (win ? t < 2.6f : t > 0.6f && t < 2.9f)
                {   // pale souls drift up from the body / golden motes rise
                    acc += dt * (win ? 70f : 22f + 60f * Mathf.Clamp01((t - 0.6f) / 2f));
                    for (; acc >= 1f; acc--)
                    {
                        var at = hero + new Vector2(UnityEngine.Random.Range(-0.35f, 0.35f), UnityEngine.Random.Range(-0.3f, 0.4f));
                        if (win) Fx.Particle(at, new Vector2(UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(4f, 8f)), new Color(1f, 0.9f, 0.5f, 0.95f), new Color(1f, 0.7f, 0.2f, 0f), 0.3f, 0.06f, 0.6f, Res.Glow, true, 0.3f, 112);
                        else Fx.Particle(at, new Vector2(UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(1.2f, 3.2f)), new Color(0.92f, 0.96f, 1f, 0.95f), new Color(0.7f, 0.85f, 1f, 0f), 0.22f, 0.03f, 1.1f, Res.Glow, true, 0.8f, 112);   // motes shed from the body, rising slowly
                    }
                }
                if (!win && t >= nextPillar && t > 0.8f && t < 2.8f)
                {   // soft rings of light spread over the ground
                    nextPillar = t + 0.5f;
                    Fx.Ring(hero0, new Color(0.85f, 0.93f, 1f, 0.7f), 0.2f, 3.2f, 1.1f);
                }
                if (!win && !beam && t > 2.6f)
                {   // the last of the body: a thin column of light rises into the sky, motes scatter over heaven and earth
                    beam = true;
                    Fx.Pillar(hero0 + Vector2.down * 0.3f, new Color(0.8f, 0.9f, 1f), 14f, 1.4f);
                    Fx.Pillar(hero0 + Vector2.down * 0.3f, Color.white, 10f, 1.1f);
                    Fx.Ring(hero0, new Color(0.9f, 0.95f, 1f, 0.9f), 0.3f, 7f, 1.2f);
                    for (int q = 0; q < 40; q++)
                    {
                        float ang = UnityEngine.Random.value * Mathf.PI * 2f, sp = UnityEngine.Random.Range(1.5f, 5f);
                        Fx.Particle(hero0 + Vector2.up * 0.3f, new Vector2(Mathf.Cos(ang) * sp, Mathf.Sin(ang) * sp * 0.6f + 1.5f), new Color(0.95f, 0.98f, 1f, 1f), new Color(0.7f, 0.85f, 1f, 0f), 0.2f, 0.03f, 1.4f, Res.Glow, true, 0.9f, 112);
                    }
                    Sfx.Play("bell", 0.5f, 0f, 1f, 0f);
                }
                if (win && t >= nextPillar && t < 2.6f)
                {
                    nextPillar = t + 0.45f;
                    Fx.Pillar(hero0 + Vector2.down * 0.3f, Gold, 16f, 1.3f);
                    Fx.Pillar(hero0 + Vector2.down * 0.3f, Color.white, 12f, 1f);
                }

                for (int i = 0; i < ws.Count; i++)
                {
                    var v = ws[i].View; if (!v) continue;
                    if (win)
                    {
                        v.position = p0[i] + Vector3.up * (k * k * 3.5f);
                        for (int j = 0; j < srs[i].Length; j++) { var c = cs[i][j]; c.a *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1f, k)); srs[i][j].color = c; }
                        continue;
                    }
                    float s = t - (0.7f + i * 0.15f);
                    if (s < 0f) continue;
                    float wk = Mathf.Clamp01(s / 1.2f);
                    v.position = p0[i] + Vector3.up * (wk * 0.8f);
                    for (int j = 0; j < srs[i].Length; j++) { var c = cs[i][j]; c.a *= 1f - Mathf.SmoothStep(0f, 1f, wk); srs[i][j].color = c; }
                    if (wk < 1f && UnityEngine.Random.value < 0.5f) Fx.Particle(v.position, new Vector2(UnityEngine.Random.Range(-0.4f, 0.4f), UnityEngine.Random.Range(1f, 2.5f)), new Color(0.92f, 0.96f, 1f, 0.9f), new Color(0.7f, 0.85f, 1f, 0f), 0.16f, 0.03f, 0.9f, Res.Glow, true, 0.8f, 112);
                    if (wk >= 1f && !done_[i]) { done_[i] = true; ws[i].DestroyView(); }
                }

                float bk = Ease(Mathf.Clamp01((t - tb) / 0.35f));
                band.rectTransform.localScale = new Vector3(bk, 1, 1);
                band2.rectTransform.localScale = new Vector3(Ease(Mathf.Clamp01((t - tb - 0.08f) / 0.4f)), 1, 1);
                float tk = Mathf.Clamp01((t - tb - 0.15f) / 0.3f);
                if (!hit && tk > 0f)
                {   // the ink splashes in, then reveals the title (InkSplatFx drives the title's own alpha and scale)
                    hit = true; Sfx.Play("heavy", 1f, 0f, 1f); Fx.Shake(0.35f, 0.3f);
                    UIK.InkSplat(title, win ? new Color(0.42f, 0.27f, 0.04f, 0.94f) : new Color(0.02f, 0.03f, 0.08f, 0.97f), win ? 1 : 0, win ? (Color?)null : new Color(0.82f, 0.92f, 1f, 0.85f));
                }
                tcg.alpha = hit ? 1f : 0f;
                scg.alpha = Mathf.Clamp01((t - tb - 0.7f) / 0.4f);
                yield return null;
            }
            bool skipped = t < T;
            done();          // builds the summary, which fades in over this layer
            Restore();
            co = null;
            for (float f = 0f; !skipped && f < 0.4f; f += Time.unscaledDeltaTime) { lcg.alpha = 1f - f / 0.4f; yield return null; }
            if (layer) Destroy(layer.gameObject);
            layer = null;
        }
    }
}
