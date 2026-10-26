using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    /// <summary>Living background behind the main menu: the last-played cultivator meditating, monsters wandering, qi motes rising.</summary>
    public class MenuAmbience : MonoBehaviour
    {
        class Walker { public Transform t; public SpriteRenderer sr; public Vector2 target; public float speed, phase, scale; }
        readonly List<Walker> walkers = new List<Walker>();
        SpriteRenderer hero, heroAura;
        float moteT;
        bool shown;

        void Start()
        {
            var h = new GameObject("menuHero");
            h.transform.SetParent(transform, false);
            heroAura = new GameObject("aura").AddComponent<SpriteRenderer>();
            heroAura.transform.SetParent(h.transform, false);
            heroAura.sprite = Res.Glow; heroAura.sharedMaterial = Res.AddMat; heroAura.sortingOrder = -110;
            hero = new GameObject("body").AddComponent<SpriteRenderer>();
            hero.transform.SetParent(h.transform, false);
            hero.transform.localScale = Vector3.one * 1.6f;
            int[] pool = { 123, 120, 108, 122, 121, 124, 111, 110 };
            for (int i = 0; i < 9; i++)
            {
                var go = new GameObject("menuWalker");
                go.transform.SetParent(transform, false);
                var w = new Walker { t = go.transform, sr = go.AddComponent<SpriteRenderer>(), speed = Random.Range(0.6f, 1.4f), phase = Random.Range(0f, 10f), scale = Random.Range(0.9f, 1.3f) };
                w.sr.sprite = Res.Tile(pool[i % pool.Length]);
                w.sr.color = new Color(0.75f, 0.7f, 0.8f, 0.85f);
                w.t.position = RandomPoint();
                w.target = RandomPoint();
                if (w.target.x * w.t.position.x < 0) w.target.x = -w.target.x;
                walkers.Add(w);
            }
            SetVisible(false);
        }

        static Vector2 RandomPoint()
        {
            // the flanks only, below the title, so the menu column and logo stay readable
            return new Vector2((Random.value < 0.5f ? -1f : 1f) * Random.Range(5f, 13f), Random.Range(-7.5f, 2.5f));
        }

        void SetVisible(bool v)
        {
            shown = v;
            hero.transform.parent.gameObject.SetActive(v);
            foreach (var w in walkers) w.t.gameObject.SetActive(v);
        }

        void Update()
        {
            var g = Game.I;
            if (g == null) return;
            bool want = g.state == GState.Menu;
            if (want != shown) SetVisible(want);
            if (!want) return;
            float t = Time.time, dt = Time.deltaTime;

            hero.sprite = Res.Tile(Content.Chars[Mathf.Clamp(g.save.lastChar, 0, Content.Chars.Count - 1)].sprite);
            hero.transform.localPosition = new Vector3(-9.5f, -0.8f + Mathf.Sin(t * 1.5f) * 0.12f, 0);   // floating in meditation
            heroAura.transform.localPosition = hero.transform.localPosition;
            heroAura.transform.localScale = Vector3.one * (4.5f + Mathf.Sin(t * 2f) * 0.4f);
            heroAura.color = new Color(0.45f, 0.9f, 1f, 0.22f + Mathf.Sin(t * 2f) * 0.06f);

            foreach (var w in walkers)
            {
                Vector2 p = w.t.position;
                Vector2 d = w.target - p;
                if (d.magnitude < 0.3f)
                {
                    w.target = RandomPoint();
                    if (w.target.x * p.x < 0) w.target.x = -w.target.x;   // stay on this flank
                }
                p += d.normalized * w.speed * dt;
                w.phase += dt * 8f;
                w.t.position = p;
                float bob = Mathf.Abs(Mathf.Sin(w.phase)) * 0.08f;
                w.t.localScale = new Vector3(w.scale * (1f + bob), w.scale * (1f - bob), 1);
                w.sr.flipX = d.x < 0;
                w.sr.sortingOrder = Mathf.RoundToInt(-p.y * 5f);
            }

            moteT -= dt;
            if (moteT <= 0)
            {
                moteT = 0.06f;
                var pos = new Vector2(Random.Range(-14f, 14f), Random.Range(-9f, 7f));
                var c = Random.value < 0.7f ? new Color(0.45f, 1f, 0.9f, 0.6f) : new Color(1f, 0.85f, 0.4f, 0.6f);
                Fx.Particle(pos, new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.4f, 1.2f)), c, new Color(c.r, c.g, c.b, 0f),
                    0.18f, 0.05f, Random.Range(2f, 4f), Res.Glow, true, 0f, 105);
            }
        }
    }
}
