using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace VK
{
    public enum GState { Menu, Playing, Between, Over }

    public class RunSummary
    {
        public bool win; public int wave, level, kills, daoHanh, kiep; public string charName;
        public float dmgDealt, bestHit, skillDmg; public int bestCombo; public List<string> unlocks = new List<string>();
    }

    /// <summary>Bootstraps the whole game from code and owns the run state machine.</summary>
    [DefaultExecutionOrder(-100)]
    public class Game : MonoBehaviour
    {
        public static Game I;
        /// <summary>Gameplay delta time (0 while paused / between waves, slowed during hit-stop).</summary>
        public static float Dt;
        public static float T;

        public GState state = GState.Menu;
        public bool paused;
        public RunState run;
        public Player player;
        public WaveDirector dir;
        public UIRoot ui;
        public Camera cam;
        public SaveData save;
        public AutoTest bot;
        public Arena arena;

        float hitstopT, shakeAmp, shakeDecay;
        Vector3 camPos;
        float pickupCombo, lastPickup;
        public int combo; float comboT;

        /// <summary>Boss cutscene running (BossCinema): gameplay stands still, Fx keep animating.</summary>
        public bool cinematic;
        /// <summary>Camera looks here instead of at the player (cutscenes).</summary>
        public Vector2? camFocus;
        public BossCinema cinema;
        public bool Playing => state == GState.Playing && !paused && !cinematic;
        /// <summary>Frame on which the pause menu closed (that click must not cast the skill).</summary>
        public int unpauseFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("VK_Game");
            DontDestroyOnLoad(go);
            go.AddComponent<Game>();
        }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 1;
            Application.runInBackground = true;

            bool auto = Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest") >= 0;
            if (auto) { SaveData.Key = "vk_save_autotest"; PlayerPrefs.DeleteKey(SaveData.Key); }
            Content.ApplyIcons();
            save = SaveData.Load();
            if (save.lang < 0) { save.lang = save.runs > 0 ? Loc.VI : Loc.SystemDefault(); save.Save(); }   // existing players keep Vietnamese
            Loc.SetLang(save.lang);

            cam = Camera.main;
            if (cam == null)
            {
                var c = new GameObject("Main Camera");
                cam = c.AddComponent<Camera>();
                c.tag = "MainCamera";
            }
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.orthographicSize = 7.2f;
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.08f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = camPos = new Vector3(0, 0, -10);

            Sfx.Init(gameObject);
            Sfx.Preload();
            Fx.Init();
            gameObject.AddComponent<MenuAmbience>();
            if (Platform.Touch) gameObject.AddComponent<TouchPad>();
            arena = new GameObject("Arena").AddComponent<Arena>();
            arena.Build();
            dir = gameObject.AddComponent<WaveDirector>();
            player = Player.Create();
            player.gameObject.SetActive(false);
            ui = gameObject.AddComponent<UIRoot>();
            ui.Init();
            gameObject.AddComponent<SkillRuntime>();
            cinema = gameObject.AddComponent<BossCinema>();
            if (auto) bot = gameObject.AddComponent<AutoTest>();
            GoMenu();
        }

        void Update()
        {
            float raw = Mathf.Min(Time.deltaTime, 0.05f);
            if (hitstopT > 0) { hitstopT -= Time.unscaledDeltaTime; Dt = raw * 0.05f; }
            else Dt = raw;
            if (!Playing) Dt = 0;
            T += Dt;
            if (Playing && combo > 0) { comboT -= Dt; if (comboT <= 0) combo = 0; }

            if (state == GState.Playing && GameInput.Pause) { if (cinematic) cinema.Skip(); else SetPause(!paused); }
            if (Input.GetKeyDown(KeyCode.F11) && !Application.isMobilePlatform)
                Screen.fullScreenMode = Screen.fullScreen ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;

            Fx.Tick(paused ? 0 : (state == GState.Playing && !cinematic ? Dt : raw));
            Sfx.Tick(Time.unscaledDeltaTime);
        }

        /// <summary>Where the camera wants to be: on the player, clamped so the view stays inside the arena walls.</summary>
        Vector3 CamTarget()
        {
            Vector2 target = camFocus.HasValue ? camFocus.Value : player != null && player.gameObject.activeSelf && state != GState.Menu ? player.Pos : Vector2.zero;
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            float limX = Mathf.Max(0, Arena.HalfW + 1f - halfW), limY = Mathf.Max(0, Arena.HalfH + 1.2f - halfH);
            return new Vector3(Mathf.Clamp(target.x, -limX, limX), Mathf.Clamp(target.y, -limY, limY), -10);
        }

        /// <summary>Jump onto the target instead of sliding there (wave start: the player was just moved to the centre).</summary>
        void SnapCamera() { camPos = CamTarget(); cam.transform.position = camPos; }

        void LateUpdate()
        {
            float k = 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
            camPos = Vector3.Lerp(camPos, CamTarget(), k);
            shakeAmp = Mathf.MoveTowards(shakeAmp, 0, shakeDecay * Time.unscaledDeltaTime);
            Vector3 shake = shakeAmp > 0 && !paused ? (Vector3)(UnityEngine.Random.insideUnitCircle * shakeAmp) : Vector3.zero;
            cam.transform.position = camPos + shake;
        }

        float lastSmallShake;
        public void AddShake(float mag, float dur)
        {
            mag *= save.shake;
            if (mag <= 0.001f || mag < shakeAmp) return;
            if (mag < 0.15f)
            {
                if (Time.unscaledTime - lastSmallShake < 0.08f) return;
                lastSmallShake = Time.unscaledTime;
            }
            shakeAmp = Mathf.Min(mag, 0.9f);
            shakeDecay = shakeAmp / Mathf.Max(0.05f, dur);
        }

        public void Hitstop(float t) => hitstopT = Mathf.Max(hitstopT, t);

        public void PickupSound()
        {
            if (Time.unscaledTime - lastPickup > 0.6f) pickupCombo = 0;
            lastPickup = Time.unscaledTime;
            pickupCombo = Mathf.Min(pickupCombo + 1, 24);
            Sfx.Play("pickup", 0.22f, 0.02f, 1f + pickupCombo * 0.035f, 0.03f);
        }

        public void OnKill(Vector2 pos)
        {
            combo++;
            comboT = 2.2f;
            if (run == null) return;
            run.bestCombo = Mathf.Max(run.bestCombo, combo);
            if (combo % 25 == 0)
            {
                int bonus = 1 + Mathf.Min(combo, 200) / 100;
                run.gold += bonus;
                Fx.Text(player.Pos + Vector2.up * 1.2f, "LIÊN SÁT x" + combo + "  +" + bonus, new Color(1f, 0.8f, 0.3f), 1.1f, 1.1f);
                Sfx.Play("coin", 0.6f, 0f, 1f + Mathf.Min(combo, 200) / 400f);
                if (combo >= 100) Ach.Unlock("combo100");
            }
        }

        public static IEnumerator Delay(float t, Action a)
        {
            yield return new WaitForSecondsRealtime(t);
            a();
        }

        // ------------------------------------------------------------------ flow
        public void GoMenu()
        {
            state = GState.Menu; paused = false;
            ClearWorld();
            player.gameObject.SetActive(false);
            run = null;
            ui.ShowMenu();
            arena.SetTheme(0);
            Sfx.Music("menu");
            Sfx.MusicLevel(1f);
        }

        void ClearWorld()
        {
            if (SkillRuntime.I != null) SkillRuntime.I.ClearAll();
            if (cinema != null) cinema.Abort();
            dir.ClearAll();
            foreach (var e in new List<Enemy>(Enemy.All)) if (e) { if (e.brain != null) e.brain.Stop(); Destroy(e.gameObject); }   // Stop: a deferred Destroy would let the boss coroutines resume once more this frame
            Enemy.All.Clear();
            foreach (var p in new List<Projectile>(Projectile.All)) if (p) Destroy(p.gameObject);
            Projectile.All.Clear();
            foreach (var o in new List<Orb>(Orb.All)) if (o) Destroy(o.gameObject);
            Orb.All.Clear();
            if (run != null) foreach (var w in run.weapons) w.DestroyView();
            BossHazard.ClearAll();
            BossHooks.ClearAll();
            Fx.ClearAll();
        }

        public void StartRun(int ci, int kiep)
        {
            ClearWorld();
            save.lastChar = ci; save.lastKiep = kiep; save.runs++; save.Save();
            player.gameObject.SetActive(true);
            player.Setup(Content.Chars[ci]);
            run = new RunState(ci, kiep, save);
            run.AddWeapon(Content.W(Content.Chars[ci].startWeapon), 0);
            run.hp = run.MaxHP;
            ui.ShowHUD();
            BeginWave(1);
        }

        public void BeginWave(int n)
        {
            ui.HideScreen();
            run.wave = n;
            combo = 0;
            run.riteT = 0;
            run.hitsThisWave = 0; run.eliteChests = 0; run.rerolls = 0; run.levelRerolls = 0; run.shopRolled = false;
            Skills.OnWaveStart(run);
            run.Recalc();
            run.hp = run.MaxHP;
            save.bestWave = Mathf.Max(save.bestWave, n);
            player.ResetForWave();
            SnapCamera();
            foreach (var w in run.weapons) w.cd = UnityEngine.Random.Range(0.1f, 0.4f);
            int region = (n - 1) / 5;
            bool newRegion = region != arena.Theme;
            arena.SetTheme(region, n == 1);
            dir.Begin(n);
            state = GState.Playing; paused = false;
            bool bossWave = n % 5 == 0;
            Sfx.Music(bossWave ? "boss" : "battle");
            Sfx.MusicLevel(1f);
            if (bossWave) ui.Banner("ĐỢT " + n + " — THIÊN KIẾP", run.Boss(n / 5 - 1).name + " giáng lâm!", new Color(1f, 0.4f, 0.4f));
            else if (run.activeRoute > 0) ui.Banner("ĐỢT " + n + " — " + Content.RouteName[run.activeRoute].ToUpper(), Content.RouteDesc[run.activeRoute], new Color(1f, 0.55f, 0.45f));
            else if (newRegion || n == 1) ui.Banner("ĐỢT " + n, "Tiến vào " + Arena.RegionName[region] + (n == 1 ? GameInput.K("  ·  WASD di chuyển · SPACE lướt · E thần thông", "  ·  Joystick trái di chuyển · nút phải để lướt") : ""), Color.white);
            else ui.Banner("ĐỢT " + n, "", Color.white);
            if (n == 1) ui.Hint("move", GameInput.K("WASD / phím mũi tên", "Kéo joystick bên trái") + " để di chuyển. <color=#FFD266>Vũ khí tự tấn công</color> kẻ địch gần nhất — hãy giữ khoảng cách vừa tầm.");
            if (bossWave) ui.Hint("boss", "BOSS! Né <color=#FF7070>vạch đỏ</color> (lao húc) và vòng đạn. Vòng vàng là sét Thiên Kiếp — đứng ngoài vòng!");
        }

        public void EndWave()
        {
            if (state != GState.Playing) return;
            state = GState.Between;
            run.riteT = 0;
            if (SkillRuntime.I != null) SkillRuntime.I.ClearAll();
            dir.EndWave();
            Orb.CollectAll();
            int harvest = Mathf.RoundToInt(Mathf.Max(0, run.stats[Stat.Harvest]));
            int interest = run.Has("tu_bao_bon") ? Mathf.Min(20, run.gold / 10) : 0;
            if (run.activeRoute == 3) run.chests++;
            run.activeRoute = 0;
            run.gold += harvest + interest;
            if (run.gold >= 400) Ach.Unlock("rich");

            Ach.Unlock("wave1");
            if (run.wave >= 10) Ach.Unlock("wave10");
            if (System.Array.IndexOf(Events.Waves, run.wave) >= 0) run.eventPending = true;
            Debug.Log("[VK] wave " + run.wave + " end: lvl " + run.level + " gold " + run.gold + " kills " + run.kills + " hp " + run.hp + "/" + run.MaxHP);
            if (run.wave >= 5 && run.hitsThisWave == 0) Ach.Unlock("nohit");
            save.Save();

            Sfx.Play("j_wave", 0.6f, 0f);
            Sfx.MusicLevel(0.35f);
            string sub = "";
            if (harvest > 0) sub += "Thu hoạch +" + harvest + "  ";
            if (interest > 0) sub += "Lãi +" + interest;
            ui.Banner("ĐỢT " + run.wave + " HOÀN THÀNH", sub, new Color(0.6f, 1f, 0.7f));

            if (run.stageUpWave == run.wave && !(run.chests > 0 && run.wave % 5 == 0))
            {   // Tiến Giai without a Bảo Khố screen to carry the line: say it as a toast
                ui.Hint("tiengiai", "Mỗi boss hạ gục sẽ <color=#FFD266>Tiến Giai</color> Thần Thông: chiêu đổi dạng, mạnh hơn. Xem tên dạng ở thanh chiêu.");
                ui.Toast("THẦN THÔNG TIẾN GIAI", Skills.FormName(run.ch, run.skillStage) + " (" + Skills.StageLabel(run.skillStage) + ")");
            }

            if (run.wave >= 20) { StartCoroutine(Delay(1.8f, () => FinishRun(true))); return; }
            StartCoroutine(Delay(1.3f, EnterBetween));
        }

        public void EnterBetween()
        {
            if (state != GState.Between) return;
            if (run.chests > 0 && run.wave % 5 == 0 && run.relicWave != run.wave)
            {
                if (run.relicChoices == null || run.relicChoices.Length == 0) run.relicChoices = run.RollRelics();
                if (run.relicChoices.Length == 0) { run.relicWave = run.wave; EnterBetween(); return; }
                ui.ShowRelics();
            }
            else if (run.chests > 0)
            {
                if (run.chestItem == null) run.chestItem = run.RollChestItem();
                ui.ShowChest();
            }
            else if (run.eventPending)
            {
                if (run.currentEvent == null) run.currentEvent = Events.Pick(run);
                ui.ShowEvent(run.currentEvent);
            }
            else if (run.pendingLevels > 0)
            {
                if (run.levelOpts == null) run.RollLevelOpts();
                ui.ShowLevelUp();
            }
            else
            {
                if (!run.shopRolled) { run.RollShop(); run.shopRolled = true; run.routeOffer = Random.Range(1, 4); }
                ui.ShowShop();
            }
        }

        public void ChooseLevel(int i)
        {
            if (run.levelOpts == null || i < 0 || i >= run.levelOpts.Length) return;
            bool brk = run.IsBreakthrough;
            run.ChooseLevel(i);
            Sfx.Play(brk ? "breakthrough" : "confirm", 0.7f, 0.02f);
            EnterBetween();
        }

        public void TakeRelic(int i)
        {
            if (run.relicChoices == null || ui.screen != UIScreen.Relic) return;
            run.TakeRelic(i);
            Sfx.Play("confirm", 0.7f);
            Sfx.Play("breakthrough", 0.6f);
            EnterBetween();
        }

        public void TakeChest(bool sell)
        {
            if (run.chestItem == null) return;
            var it = run.chestItem;
            run.TakeChest(sell);
            Sfx.Play(sell ? "coin" : "confirm", 0.7f);
            if (!sell && it.tier >= 2) Sfx.Play("breakthrough", 0.5f);
            EnterBetween();
        }

        public void ChooseEvent(int i)
        {
            var ev = run.currentEvent;
            if (ev == null || i < 0 || i >= ev.options.Length || !ev.options[i].can(run)) return;
            string res = ev.options[i].apply(run);
            run.eventPending = false; run.currentEvent = null;
            Sfx.Play("card", 0.7f);
            ui.Toast(ev.title, res);
            EnterBetween();
        }

        public void NextWave(int route = 0)
        {
            if (state != GState.Between) return;
            run.activeRoute = (run.wave + 1) % 5 == 0 ? 0 : route;
            run.routeOffer = 0;
            Sfx.Play("open", 0.7f);
            BeginWave(run.wave + 1);
        }

        /// <summary>Banner subtitle tail listing the Uy Áp reward of a breakthrough.</summary>
        public static string UyApText(int level)
        {
            int realm = level / 10;
            if (realm >= 1 && realm - 1 < Content.UyAp.Length)
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var m in Content.UyAp[realm - 1]) parts.Add(StatInfo.Fmt(m, false));
                return " · Uy Áp: " + string.Join(", ", parts);
            }
            return realm >= 3 ? " · Uy Áp: Nguyên Anh hộ chủ mỗi đợt" : "";
        }

        public void OnLevelUp(int level)
        {
            if (player == null) return;
            save.bestLevel = Mathf.Max(save.bestLevel, level);
            if (level % 10 == 0)
            {
                int realm = level / 10;
                if (realm - 1 < Content.UyAp.Length) { run.levelStats.Add(Content.UyAp[realm - 1]); run.Recalc(); }
                run.hp = run.MaxHP;
                Fx.Ring(player.Pos, new Color(1f, 0.85f, 0.4f), 0.5f, 6f, 0.8f);
                Fx.Ring(player.Pos, new Color(1f, 1f, 1f), 0.3f, 4f, 0.6f);
                Fx.Burst(player.Pos, new Color(1f, 0.85f, 0.4f), 40, 9f, 0.18f, 0.8f, true);
                for (int i = 0; i < 3; i++)
                    Fx.Lightning(player.Pos + new Vector2((i - 1) * 0.5f, 12f), player.Pos + new Vector2((i - 1) * 0.3f, 0), new Color(1f, 0.9f, 0.5f), 0.6f - i * 0.1f, 0.7f, 10, 0.25f);
                for (int i = 0; i < 40; i++)
                    Fx.Particle(player.Pos + Random.insideUnitCircle * 2f, new Vector2(0, Random.Range(2f, 6f)), new Color(1f, 0.9f, 0.5f, 0.9f), new Color(1f, 0.6f, 0.2f, 0f),
                        0.14f, 0.03f, Random.Range(0.8f, 1.6f), Res.Glow, true, 0.5f);
                Fx.Hitstop(0.12f);
                Fx.Shake(0.35f, 0.4f);
                Sfx.Play("breakthrough", 0.9f, 0f);
                Sfx.Play("j_levelup", 0.6f, 0f);
                ui.Banner("ĐỘT PHÁ " + Content.RealmOnly(level).ToUpper() + "!", "Máu hồi đầy · phần thưởng phẩm Tiên" + UyApText(level), new Color(1f, 0.85f, 0.4f));
                if (level >= 10) Ach.Unlock("lv10");
                if (level >= 20) Ach.Unlock("lv20");
                if (level >= 30) Ach.Unlock("lv30");
            }
            else
            {
                Fx.Text(player.Pos + Vector2.up * 0.9f, Content.Realm(level), new Color(0.6f, 1f, 1f), 0.9f, 1.1f);
                Fx.Ring(player.Pos, new Color(0.5f, 1f, 1f, 0.8f), 0.3f, 2.5f, 0.5f);
                Sfx.Play("pluck", 0.7f, 0f, 1.5f);
            }
        }

        public void OnPlayerDeath()
        {
            if (state != GState.Playing) return;
            if (SkillRuntime.I != null) SkillRuntime.I.ClearAll();
            if (run.Has("bat_tu_phu") && !run.reviveUsed)
            {
                run.reviveUsed = true;
                run.hp = run.MaxHP * 0.5f;
                player.Revive();
                ui.Banner("BẤT TỬ PHÙ!", "Nghịch thiên cải mệnh", new Color(1f, 0.85f, 0.4f));
                return;
            }
            state = GState.Over;
            player.Die();
            Fx.Shake(0.6f, 0.6f);
            Sfx.Play("j_lose", 0.7f, 0f);
            Sfx.MusicLevel(0.2f);
            ui.Banner("THÂN TỬ ĐẠO TIÊU", "", new Color(1f, 0.35f, 0.35f));
            StartCoroutine(Delay(2f, () => FinishRun(false)));
        }

        public void SetPause(bool p)
        {
            if (state != GState.Playing) return;
            paused = p;
            if (!p) unpauseFrame = Time.frameCount;
            Sfx.MusicLevel(p ? 0.35f : 1f);
            if (p) ui.ShowPause(); else ui.HideScreen();
        }

        public void Abandon()
        {
            paused = false;
            state = GState.Over;
            FinishRun(false);
        }

        public void FinishRun(bool win)
        {
            state = GState.Over;
            dir.ClearAll();
            Debug.Log("[VK] RUN END win=" + win + " wave=" + run.wave + " level=" + run.level + " kills=" + run.kills);
            var before = new bool[Content.Chars.Count];
            for (int i = 0; i < before.Length; i++) before[i] = run.unlockedAtStart != null ? run.unlockedAtStart[i] : Content.CharUnlocked(i, save);

            int cleared = win ? 20 : run.wave - 1;
            var s = new RunSummary
            {
                win = win, wave = run.wave, level = run.level, kills = run.kills, kiep = run.kiep,
                charName = run.ch.name, dmgDealt = run.dmgDealt, skillDmg = run.skillDmg, bestHit = run.bestHit, bestCombo = run.bestCombo
            };
            float dh = cleared * 3 + run.kills / 40f + (win ? 60 : 0);
            s.daoHanh = Mathf.RoundToInt(dh * (1f + 0.5f * run.kiep));
            save.daoHanh += s.daoHanh;
            save.totalKills += run.kills;
            save.bestLevel = Mathf.Max(save.bestLevel, run.level);
            int kiepBefore = save.maxKiepUnlocked;
            if (win)
            {
                save.wins++;
                save.charWinKiep[run.charIndex] = Mathf.Max(save.charWinKiep[run.charIndex], run.kiep);
                if (run.kiep >= save.maxKiepUnlocked && run.kiep < Content.MaxKiep) save.maxKiepUnlocked = run.kiep + 1;
                Ach.Unlock("win");
                if (run.kiep >= 3) Ach.Unlock("kiep3");
                if (run.kiep >= 5) Ach.Unlock("kiep5");
                bool all = true; foreach (var k in save.charWinKiep) if (k < 0) all = false;
                if (all) Ach.Unlock("allwin");
            }
            if (save.totalKills >= 1000) Ach.Unlock("k1000");
            if (save.totalKills >= 10000) Ach.Unlock("k10000");
            bool allUnlocked = true;
            for (int i = 0; i < before.Length; i++)
            {
                bool now = Content.CharUnlocked(i, save);
                if (now && !before[i]) s.unlocks.Add("Mở khóa nhân vật: " + Content.Chars[i].name);
                if (!now) allUnlocked = false;
            }
            if (allUnlocked) Ach.Unlock("allchars");
            if (save.CodexSeen() >= SaveData.CodexTotal) Ach.Unlock("codex");
            if (save.maxKiepUnlocked > kiepBefore) s.unlocks.Add("Mở khóa Kiếp " + save.maxKiepUnlocked);
            save.Save();

            if (win)
            {
                Sfx.Play("j_win", 0.8f, 0f);
                Fx.Burst(player.Pos, new Color(1f, 0.85f, 0.4f), 60, 12f, 0.2f, 1.2f, true);
            }
            Sfx.Music("menu");
            Sfx.MusicLevel(1f);
            ui.ShowGameOver(s);
        }
    }
}
