using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    /// <summary>Spawns enemies (always with a telegraph), bosses and heavenly-tribulation lightning.</summary>
    public class WaveDirector : MonoBehaviour
    {
        public int wave;
        public float timeLeft, duration;
        public bool bossWave, bossSpawned, bossDead;
        public Enemy boss;
        public float HpMul { get; private set; } = 1f;
        public float SpeedMul = 1f;
        public float DmgMul { get; private set; } = 1f;

        float spawnT, strikeT, budget;
        int kiep;

        class Tele
        {
            public Vector2 pos; public float t, max, radius; public EnemyDef def; public bool isBoss, strike;
            public SpriteRenderer ring, fill;
        }
        readonly List<Tele> teles = new List<Tele>();

        public static float WaveTime(int w) => Mathf.Min(20f + w * 2.5f, 60f);

        public void Begin(int w)
        {
            ClearAll();
            wave = w;
            kiep = Game.I.run.kiep;
            HpMul = KiepNum.HpMul(kiep, w);   // kiếp bonus grows over the run so early waves stay fair
            DmgMul = KiepNum.DmgMul(kiep, w);
            bossWave = w % 5 == 0;
            duration = bossWave ? 0 : WaveTime(w);
            timeLeft = duration;
            spawnT = 0.6f; budget = 1.5f;
            strikeT = 4f;
            boss = null; bossSpawned = false; bossDead = false;
            int route = Game.I.run.activeRoute;
            RateMul = route == 1 ? 1.4f : 1f;
            EliteMul = route == 2 ? 4f : 1f;
            SpeedMul = route == 3 ? 1.25f : 1f;
            if (bossWave)
            {
                int bi = w / 5 - 1;
                var bd = Game.I.run.Boss(bi);
                AddTele(new Vector2(0, 5.5f), bd, true, false, 2.2f, 1.6f);
                Sfx.Play("bell", 0.8f, 0f, 0.6f);
            }
        }

        void Update()
        {
            var g = Game.I;
            if (!g.Playing) return;
            float dt = Game.Dt;

            if (!bossWave)
            {
                timeLeft -= dt;
                if (timeLeft <= 0) { g.EndWave(); return; }
            }

            budget += Rate() * dt;
            spawnT -= dt;
            if (spawnT <= 0)
            {
                SpawnGroup();
                spawnT = Random.Range(0.8f, 1.25f);
            }

            if (bossWave || kiep >= 4)
            {
                strikeT -= dt;
                if (strikeT <= 0)
                {
                    Strikes();
                    strikeT = bossWave ? (boss != null && boss.hp < boss.maxHp * 0.5f ? 2.4f : 3.2f) : 5.5f;
                }
            }

            for (int i = teles.Count - 1; i >= 0; i--)
            {
                var t = teles[i];
                t.t += dt;
                float k = t.t / t.max;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t.t * 18f);
                if (t.strike)
                {
                    t.fill.transform.localScale = Vector3.one * t.radius * 2f * k;
                    t.fill.color = new Color(1f, 0.85f, 0.3f, 0.25f + 0.15f * k);
                    t.ring.color = new Color(1f, 0.9f, 0.4f, 0.6f + 0.4f * pulse);
                }
                else
                {
                    t.ring.color = t.isBoss ? new Color(0.8f, 0.3f, 1f, 0.5f + 0.5f * pulse) : new Color(1f, 0.25f, 0.25f, 0.35f + 0.5f * pulse);
                    t.ring.transform.localScale = Vector3.one * t.radius * 2f * (1.3f - 0.3f * k);
                    if (t.fill) t.fill.color = new Color(1f, 0.2f, 0.2f, 0.15f * k);
                }
                if (t.t >= t.max)
                {
                    Resolve(t);
                    if (t.ring) Destroy(t.ring.gameObject);
                    if (t.fill) Destroy(t.fill.gameObject);
                    teles.RemoveAt(i);
                }
            }
        }

        /// <summary>Enemies per second.</summary>
        public float Rate()
        {
            float r = 1.2f + 0.35f * wave;
            if (kiep >= 3) r *= 1.2f;
            if (bossWave) r *= 0.35f;
            return r * RateMul;
        }
        public float RateMul = 1f, EliteMul = 1f;

        void SpawnGroup()
        {
            int n = Mathf.FloorToInt(budget);
            if (n <= 0) return;
            budget -= n;
            int room = 150 - (Enemy.All.Count + teles.Count);
            n = Mathf.Min(n, room);
            if (n <= 0) return;
            bool pack = Random.value < 0.3f;
            Vector2 center = RandomPos();
            for (int i = 0; i < n; i++)
            {
                Vector2 p = pack ? Arena.Clamp(center + Random.insideUnitCircle * 1.6f, 0.5f) : RandomPos();
                QueueSpawn(p, PickDef());
            }
            float eliteChance = wave >= 8 || EliteMul > 1f ? Mathf.Min(0.28f, (0.07f + (kiep >= 2 ? 0.06f : 0f)) * EliteMul) : 0f;   // Elite Gathering: many elites, not a wall of them
            if (!bossWave && Random.value < eliteChance)
            {
                var elites = Content.Enemies.FindAll(e => e.elite && e.minWave <= Mathf.Max(wave, EliteMul > 1f ? 8 : 0));
                if (elites.Count > 0) QueueSpawn(RandomPos(), elites[Random.Range(0, elites.Count)]);
            }
        }

        public void QueueSpawn(Vector2 p, EnemyDef def)
        {
            if (def == null) def = PickDef();
            AddTele(p, def, false, false, 0.85f, 0.45f * def.scale);
        }

        EnemyDef PickDef()
        {
            float total = 0;
            foreach (var e in Content.Enemies) if (!e.elite && e.minWave <= wave) total += W(e, wave);
            float r = Random.value * total;
            foreach (var e in Content.Enemies)
            {
                if (e.elite || e.minWave > wave) continue;
                r -= W(e, wave);
                if (r <= 0) return e;
            }
            return Content.Enemies[0];
        }

        public static float W(EnemyDef e, int w) => e.weight * (e.minWave <= 2 && w >= 10 ? 0.5f : 1f) * (e.minWave == w ? 1.5f : 1f);

        /// <summary>Weighted average trash HP at wave w (Kiếp 0), the base of the Thần Thông power scale.</summary>
        public static float ExpectedTrashHp(int w)
        {
            float tot = 0, hp = 0;
            foreach (var e in Content.Enemies) if (!e.elite && e.minWave <= w) { float x = W(e, w); tot += x; hp += x * e.hp; }
            return tot > 0 ? hp / tot * Enemy.WaveHpMul(w) : 1f;
        }

        Vector2 RandomPos()
        {
            Vector2 pp = Game.I.player.Pos;
            Vector2 best = Vector2.zero;
            for (int i = 0; i < 12; i++)
            {
                var p = new Vector2(Random.Range(-Arena.HalfW + 1f, Arena.HalfW - 1f), Random.Range(-Arena.HalfH + 1f, Arena.HalfH - 1f));
                best = p;
                if (Vector2.Distance(p, pp) > 4.5f) break;
            }
            return best;
        }

        void Strikes()
        {
            var pl = Game.I.player;
            AddTele(Arena.Clamp(pl.Pos + pl.Vel * 0.45f, 0.5f), null, false, true, 1.1f, 1.3f);
            int extra = bossWave ? 2 + wave / 10 : 1;
            for (int i = 0; i < extra; i++) AddTele(Arena.Clamp(pl.Pos + Random.insideUnitCircle * 5f, 0.5f), null, false, true, 1.1f + i * 0.15f, 1.3f);
            Sfx.Play("tick", 0.5f, 0f, 0.6f);
        }

        void AddTele(Vector2 p, EnemyDef def, bool isBoss, bool strike, float time, float radius)
        {
            var t = new Tele { pos = p, def = def, isBoss = isBoss, strike = strike, max = time, radius = radius };
            var go = new GameObject("tele");
            go.transform.position = p;
            t.ring = go.AddComponent<SpriteRenderer>();
            t.ring.sprite = Res.Ring; t.ring.sharedMaterial = Res.AddMat; t.ring.sortingOrder = -200;
            t.ring.transform.localScale = Vector3.one * radius * 2f;
            if (strike || isBoss)
            {
                var f = new GameObject("fill");
                f.transform.position = p;
                t.fill = f.AddComponent<SpriteRenderer>();
                t.fill.sprite = Res.Circle; t.fill.sortingOrder = -201;
                t.fill.transform.localScale = Vector3.one * radius * 2f;
                t.fill.color = new Color(1f, 0.2f, 0.2f, 0f);
            }
            teles.Add(t);
        }

        void Resolve(Tele t)
        {
            var g = Game.I;
            if (t.strike)
            {
                Vector2 p = t.pos;
                Fx.Lightning(p + new Vector2(Random.Range(-1f, 1f), 14f), p, new Color(1f, 0.95f, 0.6f), 0.5f, 0.3f, 12, 0.8f);
                Fx.Lightning(p + new Vector2(Random.Range(-1f, 1f), 14f), p, Color.white, 0.18f, 0.22f, 12, 0.5f);
                Fx.Ring(p, new Color(1f, 0.9f, 0.5f), 0.3f, t.radius * 1.3f, 0.35f);
                Fx.Glow(p, new Color(1f, 0.9f, 0.5f, 0.9f), t.radius * 3f, 0.3f);
                Fx.Burst(p, new Color(1f, 0.9f, 0.5f), 14, 7f, 0.12f, 0.4f, true);
                Fx.Shake(0.25f, 0.25f);
                Sfx.Play("zap", 0.7f, 0.1f, 0.6f, 0.05f);
                Sfx.Play("heavy", 0.5f, 0.1f, 0.8f, 0.05f);
                if (Vector2.Distance(g.player.Pos, p) < t.radius + g.player.radius * 0.5f)
                    g.player.Hurt((3f + wave * 0.45f) * DmgMul, p);
                var list = Enemy.Snapshot();
                foreach (var e in list)
                    if (!e.dead && !e.boss && Vector2.Distance(e.Pos, p) < t.radius + e.radius)
                        e.Damage(20f + wave * 8f, false, e.Pos - p, 1f, null, new Color(1f, 0.95f, 0.6f));
                Enemy.Release(list);
                return;
            }
            if (t.isBoss)
            {
                int bi = wave / 5 - 1;
                boss = Enemy.Spawn(t.def, t.pos, wave, HpMul, DmgMul, true, bi);
                bossSpawned = true;
                Fx.Ring(t.pos, new Color(0.8f, 0.3f, 1f), 1f, 8f, 0.8f);
                Fx.Shake(0.5f, 0.6f);
                Sfx.Play("heavy", 1f, 0f, 0.5f);
                Sfx.Play("j_boss", 0.6f, 0f, 0.8f);
                return;
            }
            Enemy.Spawn(t.def, t.pos, wave, HpMul, DmgMul);
        }

        public void OnBossDeath()
        {
            bossDead = true;
            var run = Game.I.run;
            run.skillStage = Mathf.Min(3, run.skillStage + 1);   // Tiến Giai: every boss kill advances the skill one form (spec section 2)
            run.stageUpWave = run.wave; run.stageCallout = true;
            Game.I.player.Invuln(3f);
            ClearTeles();
            foreach (var e in Enemy.Snapshot()) if (e && !e.dead && !e.boss) e.Vanish();
            Enemy.All.RemoveAll(e => e == null || e.dead);
            Projectile.ClearAll(true);
            StartCoroutine(EndSoon());
        }

        IEnumerator EndSoon()
        {
            yield return new WaitForSecondsRealtime(1.6f);
            while (Game.I.paused) yield return null;
            if (Game.I.state == GState.Playing) Game.I.EndWave();
        }

        /// <summary>Wave over: remaining enemies vanish, telegraphs and bullets are cleared.</summary>
        public void EndWave()
        {
            foreach (var e in Enemy.Snapshot()) if (e) e.Vanish();
            Enemy.All.Clear();
            Projectile.ClearAll(true);
            ClearTeles();
        }

        public void ClearAll()
        {
            StopAllCoroutines();
            ClearTeles();
            boss = null;
        }

        void ClearTeles()
        {
            foreach (var t in teles)
            {
                if (t.ring) Destroy(t.ring.gameObject);
                if (t.fill) Destroy(t.fill.gameObject);
            }
            teles.Clear();
        }
    }
}
