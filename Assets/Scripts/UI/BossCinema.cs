using System.Collections;
using UnityEngine;

namespace VK
{
    /// <summary>Boss cutscenes (spec 4): intro, Kiếp Biến (Kiếp ≥ 3) and Ma Thân (Kiếp 5, phase 2).
    /// Gameplay stands still through Game.cinematic; the visuals here run on unscaled time. Skip() jumps to the end state.</summary>
    public class BossCinema : MonoBehaviour
    {
        public bool Active { get; private set; }
        /// <summary>The bot skips cutscenes at once so probes stay fast; scenario tests set this to watch them.</summary>
        public bool testMode;
        public bool CardUp { get; private set; }

        /// <summary>First meeting of each boss; the final boss every time (spec 4.2).</summary>
        public static bool NeedsIntro(string bossId)
        {
            var seen = Game.I.save.bossIntroSeen;
            return bossId == "b4" || seen == null || !seen.Contains(bossId);
        }
        public int Skips;   // tests: cutscenes that ended through Skip()

        BossBrain cur;
        Coroutine co;
        bool skipReq;
        float startT, baseZoom;
        SpriteRenderer veil;

        static readonly Color KbTint = new Color(0.9f, 0.6f, 1f), KbAura = new Color(0.7f, 0.4f, 1f);
        static readonly Color MtTint = new Color(0.55f, 0.45f, 0.2f), MtAura = new Color(1f, 0.82f, 0.3f);

        void Update()
        {
            if (!Active || Time.unscaledTime - startT < 0.5f) return;
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)) skipReq = true;
        }

        public void Skip() { if (Active) skipReq = true; }

        /// <summary>Kiếp 5 bosses play Ma Thân instead of the short phase transition.</summary>
        public void Attach(BossBrain b)
        {
            if (b.kiep < 5) return;
            b.PhaseTwoHook = br =>
            {
                if (Game.I.bot == null || testMode) { Begin(br, MaThan(br)); return; }
                BossHazard.ClearOwner(br.e); Projectile.ClearHostileWithin(br.e.Pos, 999f);
                ApplyMaThan(br);
            };
        }

        /// <summary>Boss just appeared: intro → Kiếp Biến → 0.6 s grace → fight.</summary>
        public void OnBossSpawn(BossBrain b)
        {
            Attach(b);
            b.Hold = true;
            if (Game.I.bot != null && !testMode) { if (b.kiep >= 3) ApplyKiepBien(b); b.Hold = false; return; }
            Begin(b, Spawn(b));
        }

        void Begin(BossBrain b, IEnumerator body)
        {
            Abort();
            cur = b; skipReq = false; startT = Time.unscaledTime; Active = true;
            var g = Game.I;
            g.cinematic = true;
            baseZoom = g.cam.orthographicSize;
            co = StartCoroutine(Run(b, body));
        }

        IEnumerator Run(BossBrain b, IEnumerator body)
        {
            yield return body;
            if (skipReq) Skips++;
            Restore();
            float t = 0f;   // 0.6 s of gameplay to get oriented before the first move
            while (t < 0.6f) { t += Game.Dt; yield return null; }
            if (b != null) b.Hold = false;
            cur = null; co = null;
        }

        /// <summary>Run abandoned / world cleared: drop the cutscene without an end state.</summary>
        public void Abort()
        {
            if (co != null) StopCoroutine(co);
            co = null;
            if (Active) Restore();
            cur = null;
        }

        void Restore()
        {
            var g = Game.I;
            Active = false; CardUp = false; g.cinematic = false; g.camFocus = null;
            if (g.cam != null && baseZoom > 0f) g.cam.orthographicSize = baseZoom;
            SetVeil(Color.clear);
            if (Sfx.CurrentTrack != null) Sfx.MusicLevel(1f);
        }

        // ---------------------------------------------------------------- timelines
        IEnumerator Spawn(BossBrain b)
        {
            if (NeedsIntro(b.kit.bossId))
            {
                CardUp = true;
                int region = Mathf.Clamp(Game.I.run.wave / 5 - 1, 0, Arena.RegionName.Length - 1);
                var card = new BossIntroCard(Game.I.ui.Root, b.e.def, b.kit, Arena.RegionName[region], Res.BossPortrait(b.kit.bossId));
                for (float t = 0f; t < BossIntroCard.Length && !skipReq; t += Time.unscaledDeltaTime) { card.Apply(t); yield return null; }
                card.Finish(); CardUp = false;
                var seen = Game.I.save.bossIntroSeen ?? (Game.I.save.bossIntroSeen = new System.Collections.Generic.List<string>());
                if (!seen.Contains(b.kit.bossId)) { seen.Add(b.kit.bossId); Game.I.save.Save(); }
            }
            if (b.kiep >= 3) yield return KiepBien(b);
        }

        public IEnumerator KiepBien(BossBrain b)
        {
            var e = b.e; var g = Game.I;
            g.camFocus = e.Pos;
            Sfx.Play("heavy", 1f, 0f, 0.6f);
            yield return Tween(0.6f, k => { SetVeil(new Color(0f, 0f, 0.05f, 0.45f * k)); Zoom(1f + 0.25f * k); });
            if (skipReq) goto done;
            for (int i = 0; i < 3; i++)
            {
                Bolt(e.Pos, KbAura); Sfx.Play("zap", 1f, 0f, 0.8f);
                SetVeil(new Color(1f, 1f, 1f, 0.5f));
                yield return Wait(0.12f); SetVeil(new Color(0f, 0f, 0.05f, 0.45f));
                yield return Wait(0.28f);
                if (skipReq) goto done;
            }
            e.SetTint(KbTint); e.SetAura(KbAura);
            Fx.Ring(e.Pos, KbAura, 1f, 6f, 0.7f); Sfx.Play("heavy", 1f, 0f, 0.7f);
            yield return Wait(0.8f);
            if (skipReq) goto done;
            Speak(e, b.kit.quoteMutate, KbAura);
            Banner(e, "Kiếp Biến", KbAura);
            yield return Wait(1.4f);
            if (skipReq) goto done;
            yield return Tween(0.4f, k => { SetVeil(new Color(0f, 0f, 0.05f, 0.45f * (1f - k))); Zoom(1.25f - 0.25f * k); });
        done:
            ApplyKiepBien(b);
        }

        void ApplyKiepBien(BossBrain b) { b.e.SetTint(KbTint); b.e.SetAura(KbAura); }

        public IEnumerator MaThan(BossBrain b)
        {
            var e = b.e; var g = Game.I;
            b.Hold = true;
            BossHazard.ClearOwner(e); Projectile.ClearHostileWithin(e.Pos, 999f);
            g.camFocus = e.Pos;
            Sfx.MusicLevel(0f);
            yield return Tween(0.5f, k => { SetVeil(new Color(0f, 0f, 0.05f, 0.5f * k)); Zoom(1f + 0.3f * k); });
            if (skipReq) goto done;
            Fx.Sigil(e.Pos, MtAura, 3.5f, 1.8f);
            for (int i = 0; i < 9; i++)
            {
                Bolt(e.Pos, MtAura); Sfx.Play("zap", 0.9f, 0f, 0.7f); Fx.Shake(0.15f, 0.15f);
                yield return Wait(0.2f);
                if (skipReq) goto done;
            }
            Fx.Ring(e.Pos, Color.white, 1f, 9f, 0.6f);
            SetVeil(new Color(1f, 1f, 1f, 0.8f)); Sfx.Play("heavy", 1f, 0f, 0.5f);
            ApplyMaThan(b);
            yield return Wait(0.15f);
            SetVeil(new Color(0f, 0f, 0.05f, 0.5f));
            yield return Wait(0.45f);
            if (skipReq) goto done;
            Speak(e, b.kit.quoteMaThan, MtAura);
            Banner(e, "Ma Thân", MtAura);
            yield return Wait(1.7f);
            if (skipReq) goto done;
            yield return Tween(0.4f, k => { SetVeil(new Color(0f, 0f, 0.05f, 0.5f * (1f - k))); Zoom(1.3f - 0.3f * k); });
        done:
            ApplyMaThan(b);
        }

        void ApplyMaThan(BossBrain b)
        {
            b.e.SetScaleMul(1.15f); b.e.SetTint(MtTint); b.e.SetAura(MtAura);
            Sfx.Music("boss_climax"); Sfx.MusicLevel(1f);
        }

        // ---------------------------------------------------------------- helpers
        /// <summary>Unscaled wait that ends early on skip.</summary>
        IEnumerator Wait(float s)
        {
            for (float t = 0f; t < s && !skipReq; t += Time.unscaledDeltaTime) yield return null;
        }

        IEnumerator Tween(float s, System.Action<float> f)
        {
            for (float t = 0f; t < s && !skipReq; t += Time.unscaledDeltaTime) { f(t / s); yield return null; }
            if (!skipReq) f(1f);
        }

        void Zoom(float z) { var g = Game.I; if (g.cam != null) g.cam.orthographicSize = baseZoom / z; }

        void Bolt(Vector2 p, Color c)
        {
            Fx.Lightning(p + new Vector2(Random.Range(-1.5f, 1.5f), 9f), p, c, 0.2f, 0.25f, 10, 0.6f);
            Fx.Burst(p, c, 10, 6f, 0.16f, 0.5f, true);
        }

        void Speak(Enemy e, string q, Color c) => Fx.Text(e.Pos + Vector2.up * (e.radius + 1.2f), Loc.Tr(q), Color.white, 1.6f, 1.4f);

        void Banner(Enemy e, string what, Color c)
        {
            if (Game.I.ui != null) Game.I.ui.Banner("", e.def.name + " · " + Loc.Tr(what), c);
        }

        void SetVeil(Color c)
        {
            var cam = Game.I.cam;
            if (cam == null) return;
            if (veil == null)
            {
                var o = new GameObject("cineVeil");
                o.transform.SetParent(cam.transform, false);
                o.transform.localPosition = new Vector3(0f, 0f, 5f);
                o.transform.localScale = new Vector3(80f, 80f, 1f);
                veil = o.AddComponent<SpriteRenderer>();
                veil.sprite = Res.White; veil.sortingOrder = 900;
            }
            veil.color = c;
        }
    }
}
