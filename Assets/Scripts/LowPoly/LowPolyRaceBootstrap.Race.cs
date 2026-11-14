using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Závod: auta, kamery (i split-screen), HUD, pauza, cíl, výsledky, replay a šampionát.</summary>
public partial class LowPolyRaceBootstrap
{
    /// <summary>HUD jednoho hráče (ve split-screenu má každý svůj).</summary>
    private class PlayerHud
    {
        public Player player;
        public UIController ui;
        public RectTransform nitroFill;
        public CarController chaseCar; // režim POLICE CHASE (bez RacerBase)
        public Text drift;
    }

    private readonly List<Player> humans = new List<Player>();
    private readonly List<PlayerHud> huds = new List<PlayerHud>();
    private Player player; // hráč 1
    private readonly List<AIPlayer> opponents = new List<AIPlayer>();
    private UIController hud;  // HUD hráče 1
    private PauseMenu pauseMenu;
    private Canvas raceCanvas; // překryv přes celou obrazovku (pauza, výsledky)
    private bool racing;
    private bool resultsShown;
    private float recordBefore;
    private readonly List<RacerBase> finishOrder = new List<RacerBase>();
    private readonly Dictionary<RacerBase, float> finishTimes = new Dictionary<RacerBase, float>();
    private readonly Dictionary<RacerBase, float> raceTimes = new Dictionary<RacerBase, float>();
    private GhostPlayer ghost;
    private bool ghostBeaten;
    private ReplaySystem replay;

    private bool SplitScreen => RaceSetup.Players == 2;

    // ================================================================ start

    private void StartRace()
    {
        racing = true;
        PlayMusic(ProceduralAudio.RaceMusic);
        SkidMarks.Create(Mat("skid", new Color(0.09f, 0.09f, 0.1f), 0.05f));
        ParticleFX.Create(
            Mat("smoke", new Color(0.85f, 0.85f, 0.88f), 0.05f),
            Mat("dust", theme["vergeA"] * 1.1f, 0.02f),
            Mat("spark", new Color(1f, 0.6f, 0.2f), 0.3f, new Color(1f, 0.6f, 0.2f) * 3f),
            Mat("splash", new Color(0.75f, 0.82f, 0.9f), 0.6f));
        replay = new GameObject("Replay").AddComponent<ReplaySystem>();
        replay.SetTvSpots(TvSpots());

        int opponentCount = RaceSetup.Opponents;
        float baseSkill = RaceSetup.DifficultySkill[Mathf.Clamp(RaceSetup.difficulty, 0, 2)];
        var usedColors = new List<int> { RaceSetup.colorIndex, SplitScreen ? RaceSetup.carColor2 : -1 };
        float grip = theme.rain ? (theme.scenery == TrackTheme.Scenery.Snow ? 0.72f : 0.78f) : 1f;

        // Soupeři startují vepředu, hráči z posledních pozic
        for (int i = 0; i < opponentCount; i++)
        {
            GridSlot(i, out Vector3 pos, out Quaternion rot);
            int color = NextColor(usedColors);
            var style = (LowPolyCarFactory.BodyStyle)(((int)RaceSetup.carStyle + 1 + i) % 3);
            AIPlayer ai = CreateAI(pos, rot, style, CarColors[color], baseSkill * (1f - 0.022f * i), 2 + i * 7 % 90);
            ai.DisplayName = Championship.AiNames[i % Championship.AiNames.Length];
            ai.Controller.gripMultiplier = grip;
            opponents.Add(ai);
            replay.Add(ai.Controller, ai.DisplayName);
        }

        for (int h = 0; h < RaceSetup.Players; h++)
        {
            GridSlot(opponentCount + h, out Vector3 pos, out Quaternion rot);
            Player p = h == 0
                ? CreatePlayer(pos, rot, RaceSetup.carStyle, PlayerPalette(), RaceSetup.number, SplitScreen ? 0 : -1)
                : CreatePlayer(pos, rot, RaceSetup.carStyle2, CarPalette(CarColors[RaceSetup.carColor2]), RaceSetup.number % 99 + 1, 1);
            p.DisplayName = SplitScreen ? "P" + (h + 1) : "YOU";
            p.Controller.gripMultiplier = grip;
            humans.Add(p);
            replay.Add(p.Controller, p.DisplayName);
        }
        player = humans[0];

        raceCanvas = LowPolyUI.CreateCanvas("RaceCanvas", 10);
        for (int h = 0; h < humans.Count; h++)
        {
            Camera cam = CreateCameras(humans[h].Controller, h);
            Canvas canvas = SplitScreen ? LowPolyUI.CreateCanvas("HudP" + (h + 1), cam) : raceCanvas;
            huds.Add(CreatePlayerHud(humans[h], canvas, h == 0));
        }
        hud = huds[0].ui;
        CreatePauseMenu();

        if (RaceSetup.mode == RaceSetup.Mode.TimeTrial)
            SetupGhost();
        if (Dynamic)
            SetupDynamicConditions();
        if (!SplitScreen)
        {
            if (GameSettings.UseTouch) TouchControls.Create(raceCanvas.transform);
            if (!GameSettings.TutorialDone) SetupTutorial();
            SetupAchievements();
        }

        recordBefore = Leaderboard.BestTime(definition.LeaderboardKey);
        foreach (var hp in humans)
        {
            Player captured = hp;
            hp.LapCompleted += t => OnHumanLap(captured, t);
        }
        foreach (var ai in opponents)
        {
            AIPlayer captured = ai;
            ai.LapCompleted += t => OnOpponentLap(captured, t);
        }
        if (definition.id.StartsWith("custom_") == false)
        {
            var ids = new List<string>();
            foreach (var d in TrackLibrary.All) ids.Add(d.id);
            Achievements.TrackVisited(definition.id, ids);
        }
    }

    private Vector3[] TvSpots()
    {
        var spots = new List<Vector3>();
        float offset = settings.roadWidth * 0.5f + settings.barrierOffset + 7f;
        for (int i = 0, k = 0; i < track.centers.Length; i += 28, k++)
            spots.Add(track.Center(i) + track.Right(i) * (k % 2 == 0 ? offset : -offset) + Vector3.up * 6f);
        return spots.ToArray();
    }

    private static int NextColor(List<int> used)
    {
        for (int i = 0; i < 8; i++)
        {
            if (!used.Contains(i))
            {
                used.Add(i);
                return i;
            }
        }
        return used.Count % 8;
    }

    private int CarLayer => Mathf.Max(0, LayerMask.NameToLayer("Car2"));

    private bool Headlights => theme.night || RaceSetup.dynamicTime;

    private Player CreatePlayer(Vector3 position, Quaternion rotation, LowPolyCarFactory.BodyStyle style, LowPolyCarFactory.Palette palette, int number, int playerIndex)
    {
        // Vrstva Car2 - auta na ní do sebe narážejí (původní trať používá Car/Car2, které spolu nekolidují)
        GameObject go = LowPolyCarFactory.Build("PlayerCar", position, rotation, style, palette, CarLayer, out Car car,
            new LowPolyCarFactory.Options { number = number, pattern = playerIndex <= 0 ? RaceSetup.pattern : 0, headlights = Headlights, headlightIntensity = 2.6f });
        car.playerIndex = playerIndex;
        Player p = go.AddComponent<Player>();
        if (GameSettings.SteerAssist > 0 || GameSettings.BrakeAssist)
        {
            var assist = go.AddComponent<DrivingAssist>();
            assist.steering = GameSettings.SteerAssist == 2 ? 0.65f : GameSettings.SteerAssist == 1 ? 0.35f : 0f;
            assist.braking = GameSettings.BrakeAssist;
        }
        AddCarExtras(go, palette, false);
        go.SetActive(true);
        return p;
    }

    private AIPlayer CreateAI(Vector3 position, Quaternion rotation, LowPolyCarFactory.BodyStyle style, Color color, float skill, int number)
    {
        LowPolyCarFactory.Palette palette = CarPalette(color);
        GameObject go = LowPolyCarFactory.Build("AICar", position, rotation, style, palette, CarLayer, out AICar car,
            new LowPolyCarFactory.Options { number = number, headlights = Headlights, headlightIntensity = 1.6f });
        AIPlayer ai = go.AddComponent<AIPlayer>();
        // Checkpointy jsou husté (~24 m), proto AI sleduje zatáčky víc dopředu
        float wet = theme.rain ? 0.88f : 1f;
        ai.straightSpeedKmh = 170f;
        ai.cornerSpeedKmh = 62f * wet;
        ai.cornerAngleForMinSpeed = 40f;
        ai.cornerLookAhead = 5;
        ai.brakingSlope = 1.3f * wet;
        ai.lookAheadDistance = 12f;
        ai.skill = skill;
        AddCarExtras(go, palette, true);
        go.SetActive(true);
        return ai;
    }

    private void AddCarExtras(GameObject go, LowPolyCarFactory.Palette palette, bool ai)
    {
        var fx = go.AddComponent<CarEffects>();
        fx.rain = theme.rain;
        fx.spatialAudio = ai || SplitScreen;
        fx.volume = ai ? 0.6f : 1f;
        if (GameSettings.Damage > 0)
        {
            var damage = go.AddComponent<CarDamage>();
            damage.level = GameSettings.Damage;
            damage.debrisMaterial = palette.paint;
        }
        AddEngineSound(go, ai ? 0.35f : 0.6f, ai || SplitScreen);
    }

    private void AddEngineSound(GameObject car, float volume, bool spatial)
    {
        if (engineClip == null) return;
        var source = car.AddComponent<AudioSource>();
        source.clip = engineClip;
        source.loop = true;
        source.playOnAwake = true;
        source.volume = volume * GameSettings.Sfx;
        source.pitch = 0.3f;
        source.spatialBlend = spatial ? 1f : 0f;
        source.maxDistance = 120f;
        source.rolloffMode = AudioRolloffMode.Linear;

        var sounds = car.AddComponent<CarSounds>();
        sounds.minSpeed = 0.3f;
        sounds.maxSpeed = 50f;
        sounds.minPitch = 0.3f;
        sounds.maxPitch = 1.25f;
    }

    private void SetupGhost()
    {
        var recorder = player.gameObject.AddComponent<GhostRecorder>();
        recorder.trackId = definition.id;
        recorder.style = (int)RaceSetup.carStyle;

        GhostData.Lap saved = GhostData.Load(definition.id);
        recorder.NewBest += lap =>
        {
            if (saved != null && !ghostBeaten)
            {
                ghostBeaten = true;
                Achievements.Unlock("ghostbuster");
            }
            if (ghost != null) ghost.lap = lap;
            hud.ShowMessage("NEW GHOST!", 1.6f, new Color(0.5f, 0.9f, 1f));
        };
        if (!GameSettings.ShowGhost) return;

        var ghostStyle = saved != null ? (LowPolyCarFactory.BodyStyle)Mathf.Clamp(saved.style, 0, 5) : RaceSetup.carStyle;
        GameObject visual = LowPolyCarFactory.BuildVisual("Ghost", ghostStyle, GhostPalette(), 2);
        foreach (var r in visual.GetComponentsInChildren<MeshRenderer>())
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ghost = visual.AddComponent<GhostPlayer>();
        ghost.lap = saved;
        visual.SetActive(saved != null);

        player.LapStarted += () =>
        {
            if (ghost.lap == null) return;
            ghost.gameObject.SetActive(true);
            ghost.Restart();
        };
    }

    private void SetupDynamicConditions()
    {
        var dc = new GameObject("DynamicConditions").AddComponent<DynamicConditions>();
        dc.start = theme;
        dc.end = endTheme;
        dc.changeTime = RaceSetup.dynamicTime;
        dc.changeWeather = RaceSetup.dynamicWeather;
        dc.sun = sun;
        dc.skyMaterial = RenderSettings.skybox;
        dc.leader = player;
        dc.totalLaps = RaceSetup.laps;
        dc.weather = weather;
        dc.snow = theme.scenery == TrackTheme.Scenery.Snow;
        dc.rainMaterial = Mat("precip", dc.snow ? Color.white : new Color(0.75f, 0.8f, 0.9f), 0.6f);
        if (materials.TryGetValue("asphaltA", out Material a)) dc.asphalt.Add(a);
        if (materials.TryGetValue("asphaltB", out Material b)) dc.asphalt.Add(b);
    }

    private void SetupTutorial()
    {
        Text text = LowPolyUI.Label(raceCanvas.transform, "", 30, TextAnchor.MiddleCenter, Color.white, "Tutorial", true);
        LowPolyUI.Place(text.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 260f), new Vector2(1500f, 60f));
        var bg = text.gameObject.AddComponent<Outline>();
        bg.effectColor = new Color(0f, 0f, 0f, 0.9f);
        bg.effectDistance = new Vector2(2f, -2f);
        var t = player.gameObject.AddComponent<TutorialHints>();
        t.car = player.Controller;
        t.racer = player;
        t.text = text;
    }

    private void SetupAchievements()
    {
        Text toast = LowPolyUI.Label(raceCanvas.transform, "", 30, TextAnchor.UpperCenter, LowPolyUI.Accent, "Toast", true);
        LowPolyUI.Place(toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1200f, 90f));
        toast.enabled = false;
        var tracker = player.gameObject.AddComponent<AchievementTracker>();
        tracker.car = player.Controller;
        tracker.racer = player;
        tracker.toast = toast;
    }

    private Camera CreateCameras(CarController target, int playerIndex)
    {
        var chase = new GameObject("ChaseCamera" + (playerIndex + 1));
        if (playerIndex == 0) chase.tag = "MainCamera";
        Camera chaseCam = SetupCamera(chase);
        if (playerIndex == 0) chase.AddComponent<AudioListener>();
        chase.transform.position = target.transform.TransformPoint(0f, 3f, -8f);
        var follow2 = chase.AddComponent<CameraFollow2>();
        follow2.target = target.transform;
        follow2.distance = 7.5f;
        follow2.height = 2.8f;
        follow2.damping = 6f;
        follow2.rotationDamping = 8f;
        chase.AddComponent<ChaseCameraFX>().target = target;
        if (weather != null && playerIndex == 0) weather.followCamera = chase.transform;

        if (SplitScreen)
        {
            // Hráč 1 nahoře, hráč 2 dole
            chaseCam.rect = playerIndex == 0 ? new Rect(0f, 0.5f, 1f, 0.5f) : new Rect(0f, 0f, 1f, 0.5f);
            return chaseCam;
        }

        var top = new GameObject("TopCamera");
        Camera topCam = SetupCamera(top);
        var follow = top.AddComponent<CameraFollow>();
        follow.Target = target.transform;
        follow.offset = new Vector3(0f, 45f, -28f);
        follow.eulerrotation = new Vector3(58f, 0f, 0f);
        follow.damper = 3f;

        var swap = new GameObject("CameraSwap").AddComponent<CameraSwap>();
        swap.cameraOne = chaseCam;
        swap.cameraTwo = topCam;
        return chaseCam;
    }

    // ================================================================ HUD a pauza

    private PlayerHud CreatePlayerHud(Player p, Canvas canvas, bool primary)
    {
        Transform root = canvas.transform;
        Color playerColor = p == humans[0] ? CarColors[RaceSetup.colorIndex] : CarColors[RaceSetup.carColor2];
        float panelAlpha = GameSettings.HighContrast ? 0.9f : 0.6f;

        RectTransform panel = LowPolyUI.Panel(root, "RacePanel", new Color(0.03f, 0.05f, 0.1f, panelAlpha));
        LowPolyUI.Place(panel, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(420f, 250f));
        RectTransform stripe = LowPolyUI.Panel(panel, "Stripe", playerColor);
        LowPolyUI.Place(stripe, new Vector2(0f, 1f), Vector2.zero, new Vector2(10f, 250f));

        Text lap = LowPolyUI.Label(panel, "Lap", 46, TextAnchor.MiddleLeft, Color.white, "Lap");
        LowPolyUI.Place(lap.rectTransform, new Vector2(0f, 1f), new Vector2(32f, -12f), new Vector2(380f, 56f));
        Text time = LowPolyUI.Label(panel, "", 34, TextAnchor.MiddleLeft, LowPolyUI.Accent, "Time", true);
        LowPolyUI.Place(time.rectTransform, new Vector2(0f, 1f), new Vector2(32f, -72f), new Vector2(380f, 40f));
        var lines = new Text[3];
        for (int i = 0; i < 3; i++)
        {
            lines[i] = LowPolyUI.Label(panel, "", 24, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.85f), "Line" + i, true);
            LowPolyUI.Place(lines[i].rectTransform, new Vector2(0f, 1f), new Vector2(32f, -124f - i * 38f), new Vector2(380f, 34f));
        }
        lines[2].color = new Color(0.6f, 0.8f, 1f);

        // Nitro
        RectTransform nitroBg = LowPolyUI.Panel(root, "NitroBar", new Color(0.03f, 0.05f, 0.1f, panelAlpha));
        LowPolyUI.Place(nitroBg, new Vector2(0f, 1f), new Vector2(24f, -284f), new Vector2(420f, 34f));
        Text nitroLabel = LowPolyUI.Label(nitroBg, "NITRO", 20, TextAnchor.MiddleLeft, Color.white, "NitroLabel");
        LowPolyUI.Place(nitroLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(100f, 30f));
        RectTransform barTrack = LowPolyUI.Panel(nitroBg, "Track", new Color(1f, 1f, 1f, 0.12f));
        LowPolyUI.Place(barTrack, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(296f, 16f));
        RectTransform nitroFill = LowPolyUI.Panel(barTrack, "Fill", new Color(0.3f, 0.85f, 1f));
        nitroFill.anchorMin = Vector2.zero;
        nitroFill.anchorMax = new Vector2(0.3f, 1f);
        nitroFill.offsetMin = nitroFill.offsetMax = Vector2.zero;

        Text drift = LowPolyUI.Label(root, "DRIFT!", 44, TextAnchor.MiddleCenter, new Color(1f, 0.6f, 0.2f), "Drift");
        LowPolyUI.Place(drift.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(600f, 60f));
        drift.enabled = false;

        if (!SplitScreen || primary)
        {
            Minimap minimap = Minimap.Create((RectTransform)root, track.centers, settings.roadWidth, SplitScreen ? 220f : 300f);
            foreach (var ai in opponents)
                minimap.AddRacer(ai.transform, ai.GetComponentInChildren<MeshRenderer>().sharedMaterials[0].color, false);
            for (int h = 0; h < humans.Count; h++)
                minimap.AddRacer(humans[h].transform, h == 0 ? CarColors[RaceSetup.colorIndex] : CarColors[RaceSetup.carColor2], true);
        }

        var manager = new GameObject("HudManager" + p.DisplayName);
        var ui = manager.AddComponent<UIController>();
        ui.UIRacePanel = panel.gameObject;
        ui.UITextCurrentLap = lap;
        ui.UITextCurrentTime = time;
        ui.UITextLastLap = lines[0];
        ui.UITextBestLap = lines[1];
        ui.UITextAIBestLap = lines[2];
        ui.UpdateUIForPlayer = p;
        ui.UpdateUIForAIPlayer = opponents.Count > 0 ? opponents[0] : null;
        ui.totalLaps = RaceSetup.laps;
        ui.lowPolyStyle = true;
        ui.overlayFont = LowPolyUI.numberFont;
        ui.useCountdown = primary; // odpočet stačí jeden
        if (opponents.Count == 0) lines[2].text = "";
        if (primary) manager.AddComponent<TuningOverlay>().car = p.Controller;

        return new PlayerHud { player = p, ui = ui, nitroFill = nitroFill, drift = drift };
    }

    private void CreatePauseMenu()
    {
        Transform root = raceCanvas.transform;
        RectTransform pause = LowPolyUI.Panel(root, "Pause", new Color(0.02f, 0.04f, 0.08f, 0.85f));
        LowPolyUI.Stretch(pause);
        pause.gameObject.SetActive(false);

        Text title = LowPolyUI.Label(pause, "PAUSED", 96, TextAnchor.MiddleCenter, Color.white, "Title");
        LowPolyUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 140f));
        string time = Dynamic ? (RaceSetup.dynamicTime ? "DUSK" : "DAY") : theme.night ? "NIGHT" : "DAY";
        string conditions = time + (RaceSetup.dynamicWeather ? "   |   CHANGING" : theme.rain ? "   |   RAIN" : "");
        Text trackName = LowPolyUI.Label(pause, $"{definition.name.ToUpper()}   |   {conditions}", 30, TextAnchor.MiddleCenter, LowPolyUI.Accent, "Track", true);
        LowPolyUI.Place(trackName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(1400f, 40f));

        var manager = new GameObject("RaceManager");
        pauseMenu = manager.AddComponent<PauseMenu>();
        pauseMenu.pauseMenuUI = pause.gameObject;

        string[] labels = { "RESUME", "RESTART", "CHANGE RACE", "MAIN MENU" };
        System.Action[] actions =
        {
            () => pauseMenu.Resume(),
            () => pauseMenu.Restart(),
            () => { RaceSetup.configured = false; RaceSetup.screen = RaceSetup.Screen.Setup; Championship.active = false; pauseMenu.Restart(); },
            () => { Championship.active = false; pauseMenu.LoadMenu(); },
        };
        for (int i = 0; i < labels.Length; i++)
        {
            Image b = LowPolyUI.Button(pause, labels[i], 34, actions[i]);
            LowPolyUI.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(-270f, 150f - i * 92f), new Vector2(400f, 76f));
        }

        // Tabulka nejlepších časů (struktura, kterou očekává Leaderboard)
        RectTransform board = LowPolyUI.Panel(pause, "TopTimesBackground", new Color(1f, 1f, 1f, 0.08f));
        LowPolyUI.Place(board, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(270f, 150f), new Vector2(440f, 360f));
        Text boardTitle = LowPolyUI.Label(board, "TOP LAPS", 34, TextAnchor.MiddleCenter, LowPolyUI.Accent, "TopTimesTitle");
        LowPolyUI.Place(boardTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(400f, 50f));

        RectTransform topTimes = LowPolyUI.Empty(pause, "TopTimesPanel");
        LowPolyUI.Place(topTimes, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(270f, 70f), new Vector2(380f, 260f));
        RectTransform template = LowPolyUI.Empty(topTimes, "LeaderboardTemplate");
        template.anchorMin = new Vector2(0f, 1f);
        template.anchorMax = new Vector2(1f, 1f);
        template.pivot = new Vector2(0.5f, 1f);
        template.anchoredPosition = Vector2.zero;
        template.sizeDelta = new Vector2(0f, 30f);
        CreateTmp("PositionText", template, TextAlignmentOptions.Left);
        CreateTmp("TimeText", template, TextAlignmentOptions.Right);

        var leaderboard = pause.gameObject.AddComponent<Leaderboard>();
        leaderboard.storageKey = definition.LeaderboardKey;
        leaderboard.racingTimeFormat = true;
        foreach (var h in huds) h.ui.leaderboard = leaderboard;

        pause.SetAsLastSibling();
    }

    private void CreateTmp(string name, Transform parent, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (tmpFont != null) tmp.font = tmpFont;
        tmp.fontSize = 26;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        LowPolyUI.Stretch(tmp.rectTransform);
        tmp.rectTransform.offsetMin = new Vector2(20f, 0f);
        tmp.rectTransform.offsetMax = new Vector2(-20f, 0f);
    }

    private void UpdateRace()
    {
        if (!racing) return;
        bool paused = PauseMenu.GameIsPaused;
        foreach (var h in huds)
        {
            if (h.player == null) continue;
            CarController car = h.player.Controller;
            h.nitroFill.anchorMax = new Vector2(Mathf.Clamp01(car.Nitro), 1f);
            h.nitroFill.GetComponent<Image>().color = car.NitroActive ? new Color(1f, 0.55f, 0.15f) : new Color(0.3f, 0.85f, 1f);
            h.drift.enabled = !paused && car.DriftAmount > 0.35f && car.InputEnabled;
            if (h.drift.enabled)
                h.drift.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 12f) * 0.05f);
        }
    }

    // ================================================================ cíl a výsledky

    private float RacerTime(RacerBase racer) => raceTimes.TryGetValue(racer, out float t) ? t : 0f;

    private void AddLapTime(RacerBase racer, float lap) => raceTimes[racer] = RacerTime(racer) + lap;

    private void OnHumanLap(Player p, float lapTime)
    {
        if (finishOrder.Contains(p)) return;
        AddLapTime(p, lapTime);
        UIController ui = huds.Find(h => h.player == p).ui;

        int lapsDone = p.CurrentLap; // událost přichází před započetím dalšího kola
        if (lapsDone == RaceSetup.laps - 1 && RaceSetup.laps > 1)
            ui.ShowMessage("FINAL LAP!", 2f, LowPolyUI.Accent);
        if (lapsDone < RaceSetup.laps) return;

        finishOrder.Add(p);
        finishTimes[p] = RacerTime(p);
        p.Controller.InputEnabled = false;
        ui.ShowMessage("FINISH!", 2f, Color.white);

        // Výsledky, až dojedou všichni lidští hráči
        if (humans.TrueForAll(h => finishOrder.Contains(h)) && !resultsShown)
            StartCoroutine(ShowResults());
    }

    private void OnOpponentLap(AIPlayer ai, float lapTime)
    {
        if (finishOrder.Contains(ai)) return;
        AddLapTime(ai, lapTime);
        if (ai.CurrentLap >= RaceSetup.laps)
        {
            finishOrder.Add(ai);
            finishTimes[ai] = RacerTime(ai);
        }
    }

    /// <summary>Konečné pořadí: kdo dojel, podle pořadí v cíli; zbytek podle ujeté vzdálenosti.</summary>
    private List<RacerBase> FinalOrder()
    {
        var order = new List<RacerBase>(finishOrder);
        var rest = new List<RacerBase>();
        foreach (var ai in opponents)
            if (!order.Contains(ai)) rest.Add(ai);
        rest.Sort((a, b) => b.RaceProgress.CompareTo(a.RaceProgress));
        order.AddRange(rest);
        return order;
    }

    private IEnumerator ShowResults()
    {
        resultsShown = true;
        yield return new WaitForSeconds(1.8f);
        if (pauseMenu != null)
        {
            pauseMenu.Resume();
            pauseMenu.enabled = false;
        }

        List<RacerBase> order = FinalOrder();
        int position = order.IndexOf(player) + 1;
        bool timeTrial = RaceSetup.mode == RaceSetup.Mode.TimeTrial;
        bool won = !timeTrial && !SplitScreen && position == 1;
        if (won)
        {
            Profile.Wins++;
            Achievements.Unlock("first_win");
            if (Profile.Wins >= 3) Achievements.Unlock("hat_trick");
            if (endTheme.night) Achievements.Unlock("night_rider");
            if (endTheme.rain) Achievements.Unlock("rain_man");
        }

        bool championship = RaceSetup.mode == RaceSetup.Mode.Championship && Championship.active;
        if (championship)
            for (int i = 0; i < order.Count; i++)
                Championship.AddResult(order[i].DisplayName, i + 1);

        Transform root = raceCanvas.transform;
        RectTransform panel = LowPolyUI.Panel(root, "Results", new Color(0.03f, 0.05f, 0.1f, 0.92f));
        LowPolyUI.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(championship ? 1180f : 820f, 820f));

        string heading = championship ? $"ROUND {Championship.round + 1} OF {Championship.Rounds}" : timeTrial ? "TIME TRIAL" : "RACE FINISHED";
        Text title = LowPolyUI.Label(panel, heading, 36, TextAnchor.MiddleCenter, Dim, "Title");
        LowPolyUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(760f, 46f));

        Color[] medal = { new Color(1f, 0.82f, 0.2f), new Color(0.8f, 0.84f, 0.9f), new Color(0.85f, 0.55f, 0.3f) };
        float leftX = championship ? -190f : 0f;
        string big = timeTrial ? "DONE" : SplitScreen ? (order.IndexOf(humans[0]) < order.IndexOf(humans[1]) ? "P1 WINS" : "P2 WINS") : Ordinal(position);
        Text pos = LowPolyUI.Label(panel, big, SplitScreen ? 90 : 120, TextAnchor.MiddleCenter,
            !timeTrial && !SplitScreen && position <= 3 ? medal[position - 1] : Color.white, "Position");
        LowPolyUI.Place(pos.rectTransform, new Vector2(0.5f, 1f), new Vector2(leftX, -76f), new Vector2(700f, 140f));
        Text of = LowPolyUI.Label(panel, definition.name.ToUpper(), 24, TextAnchor.MiddleCenter, Dim, "Of", true);
        LowPolyUI.Place(of.rectTransform, new Vector2(0.5f, 1f), new Vector2(leftX, -214f), new Vector2(700f, 32f));

        // Pořadí všech jezdců
        float y = -262f;
        for (int i = 0; i < order.Count; i++)
        {
            RacerBase r = order[i];
            bool human = r is Player;
            string t = finishTimes.TryGetValue(r, out float ft) ? TimeFormat.FormatRacing(ft) : "RUNNING";
            int points = championship && i < Championship.Points.Length ? Championship.Points[i] : 0;
            ResultRow(panel, $"{i + 1}.  {r.DisplayName}", t, championship ? $"+{points}" : "", leftX, y, human ? LowPolyUI.Accent : Color.white);
            y -= 40f;
        }
        y -= 10f;
        ResultRow(panel, "BEST LAP", TimeFormat.FormatRacing(player.BestLapTime), "", leftX, y, Dim);
        y -= 40f;
        if (player.BestLapTime < recordBefore)
        {
            Text record = LowPolyUI.Label(panel, "NEW TRACK RECORD!", 30, TextAnchor.MiddleCenter, LowPolyUI.Accent, "Record");
            LowPolyUI.Place(record.rectTransform, new Vector2(0.5f, 1f), new Vector2(leftX, y), new Vector2(700f, 40f));
        }

        // Průběžné pořadí šampionátu
        bool champFinished = false;
        if (championship)
        {
            Text standingsTitle = LowPolyUI.Label(panel, "STANDINGS", 30, TextAnchor.MiddleLeft, LowPolyUI.Accent, "StandingsTitle");
            LowPolyUI.Place(standingsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(330f, -100f), new Vector2(400f, 40f));
            List<KeyValuePair<string, int>> standings = Championship.Sorted();
            for (int i = 0; i < standings.Count; i++)
            {
                bool me = standings[i].Key == player.DisplayName;
                Text row = LowPolyUI.Label(panel, $"{i + 1}.  {standings[i].Key}", 26, TextAnchor.MiddleLeft, me ? LowPolyUI.Accent : Color.white, "Standing", true);
                LowPolyUI.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(300f, -150f - i * 40f), new Vector2(260f, 36f));
                Text p = LowPolyUI.Label(panel, standings[i].Value + " PTS", 26, TextAnchor.MiddleRight, me ? LowPolyUI.Accent : Color.white, "Points", true);
                LowPolyUI.Place(p.rectTransform, new Vector2(0.5f, 1f), new Vector2(430f, -150f - i * 40f), new Vector2(160f, 36f));
            }

            if (Championship.IsLastRound)
            {
                champFinished = true;
                bool champion = standings.Count > 0 && standings[0].Key == player.DisplayName;
                if (champion)
                {
                    Profile.Championships++;
                    Achievements.Unlock("champion");
                }
                Text champ = LowPolyUI.Label(panel, champion ? "CHAMPION!" : $"FINAL  {Ordinal(standings.FindIndex(s => s.Key == player.DisplayName) + 1)}",
                    44, TextAnchor.MiddleCenter, champion ? medal[0] : Color.white, "Champion");
                LowPolyUI.Place(champ.rectTransform, new Vector2(0.5f, 1f), new Vector2(370f, -420f), new Vector2(420f, 60f));
                if (champion)
                {
                    Text unlock = LowPolyUI.Label(panel, "FORMULA, GOLD AND CHROME UNLOCKED", 20, TextAnchor.MiddleCenter, LowPolyUI.Accent, "Unlock");
                    LowPolyUI.Place(unlock.rectTransform, new Vector2(0.5f, 1f), new Vector2(370f, -470f), new Vector2(520f, 30f));
                }
                Championship.active = false;
            }
        }

        // Tlačítka
        var labels = new List<string>();
        var actions = new List<System.Action>();
        if (championship && !champFinished)
        {
            labels.Add("NEXT RACE");
            actions.Add(() =>
            {
                Championship.round++;
                RaceSetup.trackIndex = Championship.round;
                pauseMenu.Restart();
            });
        }
        else
        {
            labels.Add(championship ? "NEW SEASON" : "RACE AGAIN");
            actions.Add(() =>
            {
                if (RaceSetup.mode == RaceSetup.Mode.Championship)
                {
                    Championship.Start();
                    RaceSetup.trackIndex = 0;
                }
                pauseMenu.Restart();
            });
        }
        labels.Add("REPLAY");
        actions.Add(() =>
        {
            // Schovat HUD i výsledky, po skončení replaye je vrátit
            var canvases = new List<Canvas> { raceCanvas };
            foreach (var c in FindObjectsOfType<Canvas>()) if (c.name.StartsWith("HudP")) canvases.Add(c);
            foreach (var c in canvases) c.enabled = false;
            replay.Play(() => { foreach (var c in canvases) if (c != null) c.enabled = true; });
        });
        labels.Add("CHANGE");
        actions.Add(() => { RaceSetup.configured = false; RaceSetup.screen = RaceSetup.Screen.Setup; Championship.active = false; pauseMenu.Restart(); });
        labels.Add("MENU");
        actions.Add(() => { Championship.active = false; pauseMenu.LoadMenu(); });

        float width = 200f, gap = 14f;
        float startX = -(labels.Count - 1) * (width + gap) * 0.5f;
        for (int i = 0; i < labels.Count; i++)
        {
            Image b = i == 0 ? AccentButton(panel, labels[i], 24, actions[i]) : LowPolyUI.Button(panel, labels[i], 24, actions[i]);
            LowPolyUI.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(startX + i * (width + gap), 36f), new Vector2(width, 72f));
        }
    }

    private static void ResultRow(RectTransform panel, string label, string value, string extra, float x, float y, Color color)
    {
        Text l = LowPolyUI.Label(panel, label, 26, TextAnchor.MiddleLeft, color, label, true);
        LowPolyUI.Place(l.rectTransform, new Vector2(0.5f, 1f), new Vector2(x - 150f, y), new Vector2(300f, 36f));
        Text v = LowPolyUI.Label(panel, value, 26, TextAnchor.MiddleRight, color, label + "Value", true);
        LowPolyUI.Place(v.rectTransform, new Vector2(0.5f, 1f), new Vector2(x + 130f, y), new Vector2(260f, 36f));
        if (!string.IsNullOrEmpty(extra))
        {
            Text e = LowPolyUI.Label(panel, extra, 26, TextAnchor.MiddleRight, LowPolyUI.Accent, label + "Extra", true);
            LowPolyUI.Place(e.rectTransform, new Vector2(0.5f, 1f), new Vector2(x + 300f, y), new Vector2(80f, 36f));
        }
    }

    private static string Ordinal(int n)
    {
        switch (n)
        {
            case 1: return "1ST";
            case 2: return "2ND";
            case 3: return "3RD";
            default: return n + "TH";
        }
    }
}
