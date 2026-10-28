using UnityEngine;
using UnityEngine.UI;

namespace VK
{
    /// <summary>Borderlands-style boss card (spec 4.2). Built from anchors only, driven by Apply(t) on unscaled time so a skip just stops calling it.</summary>
    public class BossIntroCard
    {
        public const float Length = 2.5f;
        readonly GameObject root;
        readonly RectTransform band, portrait, epithet, nameRt, region;
        readonly CanvasGroup cg;
        bool slammed;

        public BossIntroCard(RectTransform parent, EnemyDef boss, BossKit kit, string regionName, Sprite portraitSprite)
        {
            var r = UIK.R(parent, "bossIntro").Fill(); root = r.gameObject;
            cg = root.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; r.SetAsLastSibling();
            var dark = kit.col * 0.35f; dark.a = 0.94f;
            band = UIK.Img(r, dark, "band").rectTransform.Anchor(-0.3f, 0.17f, 1.3f, 0.83f);
            band.localRotation = Quaternion.Euler(0, 0, -12f);
            for (int i = 0; i < 3; i++)   // brush streaks
            {
                var s = UIK.Img(band, new Color(kit.col.r, kit.col.g, kit.col.b, 0.18f), "streak").rectTransform;
                s.Anchor(0.05f + i * 0.3f, 0.2f + i * 0.25f, 0.65f + i * 0.2f, 0.26f + i * 0.25f);
            }
            UIK.Img(r, Color.black, "barTop").rectTransform.Anchor(0, 0.9f, 1, 1);
            UIK.Img(r, Color.black, "barBot").rectTransform.Anchor(0, 0, 1, 0.1f);

            var pi = UIK.Img(r, Color.white, "portrait");
            pi.sprite = portraitSprite != null ? portraitSprite : Res.Tile(boss.sprite); pi.preserveAspect = true;
            portrait = pi.rectTransform.Anchor(0.04f, 0.12f, 0.42f, 0.88f);
            var ol = pi.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0, 0, 0, 0.8f); ol.effectDistance = new Vector2(6, -6);

            var ep = UIK.Txt(r, Loc.Tr(kit.epithet), 64, new Color(1f, 0.25f, 0.25f), TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            epithet = ep.rectTransform.Anchor(0.46f, 0.62f, 0.97f, 0.76f);
            var nm = UIK.Txt(r, Loc.Tr(boss.name), 150, UIK.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            nm.resizeTextForBestFit = true; nm.resizeTextMinSize = 50; nm.resizeTextMaxSize = 150;
            nm.horizontalOverflow = HorizontalWrapMode.Wrap; nm.verticalOverflow = VerticalWrapMode.Truncate;
            var no = nm.gameObject.AddComponent<Outline>(); no.effectColor = new Color(0.4f, 0.04f, 0.06f); no.effectDistance = new Vector2(5, -5);
            nameRt = nm.rectTransform.Anchor(0.46f, 0.24f, 0.97f, 0.62f);
            var rg = UIK.Txt(r, Loc.Tr(regionName), 42, Color.white, TextAnchor.MiddleLeft, FontStyle.Italic);
            region = rg.rectTransform.Anchor(0.04f, 0.1f, 0.5f, 0.2f);
            Apply(0f);
        }

        static float Ease(float k) => 1f - (1f - Mathf.Clamp01(k)) * (1f - Mathf.Clamp01(k));

        /// <summary>t = seconds since the card started: band 0.2, portrait 0.25, epithet 0.15, name 0.15, hold, out 0.3.</summary>
        public void Apply(float t)
        {
            if (root == null) return;
            float bandIn = Ease(t / 0.2f), portIn = Ease((t - 0.2f) / 0.25f), epIn = Ease((t - 0.45f) / 0.15f), nameK = (t - 0.6f) / 0.15f;
            float outK = Mathf.Clamp01((t - (Length - 0.3f)) / 0.3f);
            band.anchoredPosition = new Vector2(-2400f * (1f - bandIn) - 2400f * outK, 0);
            float shake = t > 0.45f && t < 0.75f ? Mathf.Sin(t * 90f) * 14f * (0.75f - t) / 0.3f : 0f;
            portrait.anchoredPosition = new Vector2(-1300f * (1f - portIn) + shake - 1300f * outK, 0);
            epithet.anchoredPosition = new Vector2(900f * (1f - epIn) + 900f * outK, 0);
            float sc = nameK < 0f ? 0f : nameK < 1f ? Mathf.Lerp(1.5f, 1f, nameK) : 1f;
            nameRt.localScale = Vector3.one * sc;
            nameRt.anchoredPosition = new Vector2(900f * outK, 0);
            if (nameK >= 0f && !slammed) { slammed = true; Sfx.Play("heavy", 0.9f, 0f, 0.8f); }
            region.anchoredPosition = new Vector2(-900f * outK, 0);
            cg.alpha = 1f - outK * 0.5f;
        }

        public void Finish() { if (root != null) Object.Destroy(root); }
    }
}
