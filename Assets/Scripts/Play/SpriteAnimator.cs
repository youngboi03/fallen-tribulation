using UnityEngine;

namespace VK
{
    public class SpriteAnimator : MonoBehaviour
    {
        public bool Active { get; private set; }
        public int Frame { get; private set; }
        public Sprite Base
        {
            get => baseSpr;
            set { baseSpr = value; if (!Active && sr != null) sr.sprite = value; }
        }

        Sprite baseSpr;
        SpriteRenderer sr;
        Sprite[] sprites;
        AnimClip clip;
        string curKey;
        float t;

        void Awake() { sr = GetComponent<SpriteRenderer>(); }

        public bool Play(string id, string clipName)
        {
            string key = id + "/" + clipName;
            if (Active && curKey == key) return true;
            var def = AnimDefs.Get(id, clipName);
            if (def == null) return false;
            var spr = Res.Anim(id, clipName);
            if (spr == null) return false;
            clip = def;
            sprites = spr;
            curKey = key;
            Active = true;
            t = 0f;
            Frame = 0;
            if (sr != null) sr.sprite = sprites[0];
            return true;
        }

        public void Stop()
        {
            Active = false;
            curKey = null;
            clip = null;
            sprites = null;
            Frame = 0;
            t = 0f;
            if (sr != null && baseSpr != null) sr.sprite = baseSpr;
        }

        void Update() { if (Active) Tick(Game.Dt); }

        public void Tick(float dt)
        {
            if (!Active || clip == null) return;
            t += dt;
            int f = Mathf.FloorToInt(t * clip.fps);
            if (clip.loop) f = f % clip.frames;
            else f = Mathf.Min(f, clip.frames - 1);
            if (f != Frame)
            {
                Frame = f;
                if (sr != null) sr.sprite = sprites[f];
            }
        }
    }
}
