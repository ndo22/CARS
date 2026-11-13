using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Závod: auta, kamery, HUD, pauza, cíl, výsledky a šampionát.</summary>
public partial class LowPolyRaceBootstrap
{
    private Player player;
    private readonly List<AIPlayer> opponents = new List<AIPlayer>();
    private UIController hud;
    private PauseMenu pauseMenu;
    private Canvas raceCanvas;
    private bool racing;
    private bool playerFinished;
    private float playerRaceTime;
    private float recordBefore;
    private readonly List<RacerBase> finishOrder = new List<RacerBase>();
    private readonly Dictionary<RacerBase, float> finishTimes = new Dictionary<RacerBase, float>();
    private readonly Dictionary<RacerBase, float> raceTimes = new Dictionary<RacerBase, float>();
    private float raceStartTime = -1f;

    // HUD navíc
    private RectTransform nitroFill;
    private Text driftText;
    private GhostPlayer ghost;

    // ================================================================ start

    private void StartRace()
    {
        racing = true;
        PlayMusic(ProceduralAudio.RaceMusic());
        SkidMarks.Create(Mat("skid", new Color(0.09f, 0.09f, 0.1f), 0.05f));
        ParticleFX.Create(
            Mat("smoke", new Color(0.85f, 0.85f, 0.88f), 0.05f),
            Mat("dust", theme["vergeA"] * 1.1f, 0.02f),
            Mat("spark", new Color(1f, 0.6f, 0.2f), 0.3f, new Color(1f, 0.6f, 0.2f) * 3f),
            Mat("splash", new Color(0.75f, 0.82f, 0.9f), 0.6f));

        int opponentCount = RaceSetup.Opponents;
        float baseSkill = RaceSetup.DifficultySkill[Mathf.Clamp(RaceSetup.difficulty, 0, 2)];
        var usedColors = new List<int> { RaceSetup.colorIndex };
        float grip = theme.rain ? (theme.scenery == TrackTheme.Scenery.Snow ? 0.72f : 0.78f) : 1f;

        // Soupeři startují vepředu, hráč z poslední pozice
        for (int i = 0; i < opponentCount; i++)
        {
            GridSlot(i, out Vector3 pos, out Quaternion rot);
            int color = NextColor(usedColors);
            var style = (LowPolyCarFactory.BodyStyle)(((int)RaceSetup.carStyle + 1 + i) % 3);
            AIPlayer ai = CreateAI(pos, rot, style, CarColors[color], baseSkill * (1f - 0.022f * i), 2 + i * 7 % 90);
            ai.DisplayName = Championship.AiNames[i % Championship.AiNames.Length];
            ai.Controller.gripMultiplier = grip;
            opponents.Add(ai);
        }
        GridSlot(opponentCount, out Vector3 playerPos, out Quaternion playerRot);
        player = CreatePlayer(playerPos, playerRot);
        player.DisplayName = "YOU";
        player.Controller.gripMultiplier = grip;

        CreateCameras(player.Controller);
        CreateHud();

        if (RaceSetup.mode == RaceSetup.Mode.TimeTrial)
            SetupGhost();

        recordBefore = Leaderboard.BestTime(definition.LeaderboardKey);
        player.LapStarted += () => { if (raceStartTime < 0f) raceStartTime = Time.time; };
        player.LapCompleted += OnPlayerLap;
        foreach (var ai in opponents)
        {
            AIPlayer captured = ai;
            ai.LapCompleted += t => OnOpponentLap(captured, t);
        }
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

    private Player CreatePlayer(Vector3 position, Quaternion rotation)
    {
        // Vrstva Car2 - auta na ní do sebe narážejí (původní trať používá Car/Car2, které spolu nekolidují)
        GameObject go = LowPolyCarFactory.Build("PlayerCar", position, rotation, RaceSetup.carStyle,
            CarPalette(CarColors[RaceSetup.colorIndex], RaceSetup.colorIndex == 9), CarLayer, out Car car,
            new LowPolyCarFactory.Options { number = 1, headlights = theme.night, headlightIntensity = 2.6f });
        Player p = go.AddComponent<Player>();
        var fx = go.AddComponent<CarEffects>();
        fx.rain = theme.rain;
        AddEngineSound(go, 0.6f, false);
        go.SetActive(true);
        return p;
    }

    private AIPlayer CreateAI(Vector3 position, Quaternion rotation, LowPolyCarFactory.BodyStyle style, Color color, float skill, int number)
    {
        GameObject go = LowPolyCarFactory.Build("AICar", position, rotation, style, CarPalette(color), CarLayer, out AICar car,
            new LowPolyCarFactory.Options { number = number, headlights = theme.night, headlightIntensity = 1.6f });
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
        var fx = go.AddComponent<CarEffects>();
        fx.spatialAudio = true;
        fx.volume = 0.6f;
        fx.rain = theme.rain;
        AddEngineSound(go, 0.35f, true);
        go.SetActive(true);
        return ai;
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
        recorder.NewBest += lap =>
        {
            ghost.lap = lap;
            hud.ShowMessage("NEW GHOST!", 1.6f, new Color(0.5f, 0.9f, 1f));
        };
    }

    private void CreateCameras(CarController target)
    {
        var chase = new GameObject("ChaseCamera");
        chase.tag = "MainCamera";
        Camera chaseCam = SetupCamera(chase);
        chase.AddComponent<AudioListener>();
        chase.transform.position = target.transform.TransformPoint(0f, 3f, -8f);
        var follow2 = chase.AddComponent<CameraFollow2>();
        follow2.target = target.transform;
        follow2.distance = 7.5f;
        follow2.height = 2.8f;
        follow2.damping = 6f;
        follow2.rotationDamping = 8f;
        chase.AddComponent<ChaseCameraFX>().target = target;
        if (weather != null) weather.followCamera = chase.transform;

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
    }

    // ================================================================ HUD a pauza

    private void CreateHud()
    {
        raceCanvas = LowPolyUI.CreateCanvas("RaceCanvas");
        Transform root = raceCanvas.transform;
        Color playerColor = CarColors[RaceSetup.colorIndex];

        // --- Panel s koly a časy (vlevo nahoře)
        RectTransform panel = LowPolyUI.Panel(root, "RacePanel", new Color(0.03f, 0.05f, 0.1f, 0.6f));
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

        // --- Nitro (pod panelem)
        RectTransform nitroBg = LowPolyUI.Panel(root, "NitroBar", new Color(0.03f, 0.05f, 0.1f, 0.6f));
        LowPolyUI.Place(nitroBg, new Vector2(0f, 1f), new Vector2(24f, -284f), new Vector2(420f, 34f));
        Text nitroLabel = LowPolyUI.Label(nitroBg, "NITRO", 20, TextAnchor.MiddleLeft, Color.white, "NitroLabel");
        LowPolyUI.Place(nitroLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(100f, 30f));
        RectTransform track01 = LowPolyUI.Panel(nitroBg, "Track", new Color(1f, 1f, 1f, 0.12f));
        LowPolyUI.Place(track01, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(296f, 16f));
        nitroFill = LowPolyUI.Panel(track01, "Fill", new Color(0.3f, 0.85f, 1f));
        nitroFill.anchorMin = Vector2.zero;
        nitroFill.anchorMax = new Vector2(0.3f, 1f);
        nitroFill.offsetMin = nitroFill.offsetMax = Vector2.zero;

        driftText = LowPolyUI.Label(root, "DRIFT!", 44, TextAnchor.MiddleCenter, new Color(1f, 0.6f, 0.2f), "Drift");
        LowPolyUI.Place(driftText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(600f, 60f));
        driftText.enabled = false;

        // --- Minimapa (vlevo dole)
        Minimap minimap = Minimap.Create((RectTransform)root, track.centers, settings.roadWidth, 300f);
        foreach (var ai in opponents)
            minimap.AddRacer(ai.transform, ai.GetComponentInChildren<MeshRenderer>().sharedMaterials[0].color, false);
        minimap.AddRacer(player.transform, playerColor, true);

        // --- Pauza
        RectTransform pause = LowPolyUI.Panel(root, "Pause", new Color(0.02f, 0.04f, 0.08f, 0.78f));
        LowPolyUI.Stretch(pause);
        pause.gameObject.SetActive(false);

        Text title = LowPolyUI.Label(pause, "PAUSED", 96, TextAnchor.MiddleCenter, Color.white, "Title");
        LowPolyUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 140f));
        string conditions = (theme.night ? "NIGHT" : "DAY") + (theme.rain ? "   |   RAIN" : "");
        Text trackName = LowPolyUI.Label(pause, $"{definition.name}   |   {conditions}", 30, TextAnchor.MiddleCenter, LowPolyUI.Accent, "Track", true);
        LowPolyUI.Place(trackName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(1200f, 40f));

        var manager = new GameObject("RaceManager");
        pauseMenu = manager.AddComponent<PauseMenu>();
        pauseMenu.pauseMenuUI = pause.gameObject;
        manager.AddComponent<TuningOverlay>().car = player.Controller;

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

        // --- UIController (rychloměr, pořadí a odpočet si vytvoří sám)
        hud = manager.AddComponent<UIController>();
        hud.UIRacePanel = panel.gameObject;
        hud.UITextCurrentLap = lap;
        hud.UITextCurrentTime = time;
        hud.UITextLastLap = lines[0];
        hud.UITextBestLap = lines[1];
        hud.UITextAIBestLap = lines[2];
        hud.UpdateUIForPlayer = player;
        hud.UpdateUIForAIPlayer = opponents.Count > 0 ? opponents[0] : null;
        hud.leaderboard = leaderboard;
        hud.totalLaps = RaceSetup.laps;
        hud.lowPolyStyle = true;
        hud.overlayFont = LowPolyUI.numberFont;
        if (opponents.Count == 0) lines[2].text = "";

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
        if (!racing || player == null) return;
        bool paused = PauseMenu.GameIsPaused;
        CarController car = player.Controller;
        if (nitroFill != null)
        {
            nitroFill.anchorMax = new Vector2(Mathf.Clamp01(car.Nitro), 1f);
            nitroFill.GetComponent<Image>().color = car.NitroActive ? new Color(1f, 0.55f, 0.15f) : new Color(0.3f, 0.85f, 1f);
        }
        if (driftText != null)
        {
            driftText.enabled = !paused && car.DriftAmount > 0.35f && !playerFinished;
            if (driftText.enabled)
                driftText.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 12f) * 0.05f);
        }
    }

    // ================================================================ cíl a výsledky

    private float RacerTime(RacerBase racer)
    {
        return raceTimes.TryGetValue(racer, out float t) ? t : 0f;
    }

    private void AddLapTime(RacerBase racer, float lap)
    {
        raceTimes[racer] = RacerTime(racer) + lap;
    }

    private void OnPlayerLap(float lapTime)
    {
        if (playerFinished) return;
        AddLapTime(player, lapTime);
        playerRaceTime = RacerTime(player);

        int lapsDone = player.CurrentLap; // událost přichází před započetím dalšího kola
        if (lapsDone == RaceSetup.laps - 1 && RaceSetup.laps > 1)
            hud.ShowMessage("FINAL LAP!", 2f, LowPolyUI.Accent);

        if (lapsDone < RaceSetup.laps) return;

        playerFinished = true;
        finishOrder.Add(player);
        finishTimes[player] = playerRaceTime;
        player.Controller.InputEnabled = false;
        hud.ShowMessage("FINISH!", 2f, Color.white);
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
        yield return new WaitForSeconds(1.8f);
        if (pauseMenu != null)
        {
            pauseMenu.Resume();
            pauseMenu.enabled = false;
        }

        List<RacerBase> order = FinalOrder();
        int position = order.IndexOf(player) + 1;
        bool timeTrial = RaceSetup.mode == RaceSetup.Mode.TimeTrial;
        if (!timeTrial && position == 1) Profile.Wins++;

        bool championship = RaceSetup.mode == RaceSetup.Mode.Championship && Championship.active;
        if (championship)
            for (int i = 0; i < order.Count; i++)
                Championship.AddResult(order[i].DisplayName, i + 1);

        Transform root = raceCanvas.transform;
        RectTransform panel = LowPolyUI.Panel(root, "Results", new Color(0.03f, 0.05f, 0.1f, 0.9f));
        LowPolyUI.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(championship ? 1180f : 820f, 800f));

        string heading = championship ? $"ROUND {Championship.round + 1} OF {Championship.Rounds}" : timeTrial ? "TIME TRIAL" : "RACE FINISHED";
        Text title = LowPolyUI.Label(panel, heading, 36, TextAnchor.MiddleCenter, Dim, "Title");
        LowPolyUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(760f, 46f));

        Color[] medal = { new Color(1f, 0.82f, 0.2f), new Color(0.8f, 0.84f, 0.9f), new Color(0.85f, 0.55f, 0.3f) };
        float leftX = championship ? -190f : 0f;
        Text pos = LowPolyUI.Label(panel, timeTrial ? "DONE" : Ordinal(position), 120, TextAnchor.MiddleCenter,
            !timeTrial && position <= 3 ? medal[position - 1] : Color.white, "Position");
        LowPolyUI.Place(pos.rectTransform, new Vector2(0.5f, 1f), new Vector2(leftX, -76f), new Vector2(700f, 140f));
        Text of = LowPolyUI.Label(panel, $"{definition.name}", 24, TextAnchor.MiddleCenter, Dim, "Of", true);
        LowPolyUI.Place(of.rectTransform, new Vector2(0.5f, 1f), new Vector2(leftX, -214f), new Vector2(700f, 32f));

        // Pořadí všech jezdců
        float y = -262f;
        for (int i = 0; i < order.Count; i++)
        {
            RacerBase r = order[i];
            bool isPlayer = r == player;
            string t = finishTimes.TryGetValue(r, out float ft) ? TimeFormat.FormatRacing(ft) : "RUNNING";
            int points = championship && i < Championship.Points.Length ? Championship.Points[i] : 0;
            string pts = championship ? $"+{points}" : "";
            ResultRow(panel, $"{i + 1}.  {r.DisplayName}", t, pts, leftX, y, isPlayer ? LowPolyUI.Accent : Color.white);
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
                bool won = standings.Count > 0 && standings[0].Key == player.DisplayName;
                if (won) Profile.Championships++;
                Text champ = LowPolyUI.Label(panel, won ? "CHAMPION!" : $"FINAL: {Ordinal(standings.FindIndex(s => s.Key == player.DisplayName) + 1)}",
                    44, TextAnchor.MiddleCenter, won ? medal[0] : Color.white, "Champion");
                LowPolyUI.Place(champ.rectTransform, new Vector2(0.5f, 1f), new Vector2(370f, -420f), new Vector2(420f, 60f));
                if (won)
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
        labels.Add("CHANGE");
        actions.Add(() => { RaceSetup.configured = false; RaceSetup.screen = RaceSetup.Screen.Setup; Championship.active = false; pauseMenu.Restart(); });
        labels.Add("MENU");
        actions.Add(() => { Championship.active = false; pauseMenu.LoadMenu(); });

        for (int i = 0; i < labels.Count; i++)
        {
            Image b = LowPolyUI.Button(panel, labels[i], 28, actions[i], out Text label);
            if (i == 0)
            {
                b.color = LowPolyUI.Accent;
                label.color = new Color(0.1f, 0.08f, 0.05f);
            }
            LowPolyUI.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2((i - 1) * 250f, 36f), new Vector2(234f, 76f));
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
