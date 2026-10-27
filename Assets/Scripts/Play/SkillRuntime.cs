using System;
using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    /// <summary>Timed parts of Thần Thông (delayed blasts, lingering fields, damage-reduction buffs). Everything ticks on Game.Dt, so pausing freezes it.</summary>
    public class SkillRuntime : MonoBehaviour
    {
        public static SkillRuntime I;

        class Delayed { public float left; public Action act; }
        class Lasting { public float left, interval, acc; public Action tick, end; }

        readonly List<Delayed> delayed = new List<Delayed>();
        readonly List<Lasting> lasting = new List<Lasting>();

        /// <summary>Damage-taken multiplier while drT &gt; 0 (the stronger reduction wins).</summary>
        public float drT, drMul = 1f;
        public float armorT, duBaT;

        void Awake() { I = this; }

        public int PendingCount => delayed.Count + lasting.Count;

        public void After(float delay, Action a) { delayed.Add(new Delayed { left = delay, act = a }); }

        public void During(float duration, float interval, Action tick, Action end = null)
        {
            lasting.Add(new Lasting { left = duration, interval = interval, acc = 0f, tick = tick, end = end });
        }

        /// <summary>Damage reduction from a skill; keeps the stronger multiplier and the longer time.</summary>
        public void Guard(float seconds, float mul)
        {
            if (drT > 0f && mul > drMul) { drT = Mathf.Max(drT, seconds); return; }
            drMul = mul; drT = Mathf.Max(drT, seconds);
        }

        public void ClearAll()
        {
            delayed.Clear(); lasting.Clear();
            drT = 0f; drMul = 1f; armorT = 0f; duBaT = 0f;
        }

        void Update()
        {
            if (!Game.I.Playing) return;
            float dt = Game.Dt;
            if (dt <= 0f) return;
            if (drT > 0f) { drT -= dt; if (drT <= 0f) drMul = 1f; }
            if (armorT > 0f) armorT -= dt;
            if (duBaT > 0f) duBaT -= dt;

            for (int i = delayed.Count - 1; i >= 0; i--)
            {
                if (i >= delayed.Count) continue;
                var d = delayed[i];
                d.left -= dt;
                if (d.left > 0f) continue;
                delayed.Remove(d);
                d.act?.Invoke();
            }
            for (int i = lasting.Count - 1; i >= 0; i--)
            {
                if (i >= lasting.Count) continue;
                var l = lasting[i];
                l.left -= dt; l.acc += dt;
                while (l.acc >= l.interval && l.interval > 0f) { l.acc -= l.interval; l.tick?.Invoke(); }
                if (l.left > 0f) continue;
                lasting.Remove(l);
                l.end?.Invoke();
            }
        }
    }
}
