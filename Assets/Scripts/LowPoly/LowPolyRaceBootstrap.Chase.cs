using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Režim POLICE CHASE: překážková dráha ze startu do cíle, za hráčem jede policie.
/// Když se policie udrží těsně u hráče, je to BUSTED. Dojetí do cíle = splněný level (1-3 hvězdy podle času).
/// </summary>
public partial class LowPolyRaceBootstrap
{
    private ObstacleCourse.Plan course;
    private Car chaseCar;
    private readonly List<ChaseAI> police = new List<ChaseAI>();
    private bool chaseActive, chaseOver;
    private float chaseTime, bustedMeter, flipTimer;
    private int chaseIndex, furthestIndex;
    private Text chaseTimeText, chaseInfoText, policeText, chaseMessage;
    private RectTransform progressBar, bustedFill;
    private RectTransform playerMarker;
    private readonly List<RectTransform> policeMarkers = new List<RectTransform>();
    private float messageUntil;
    private bool airborne;
    private Vector3 takeoff;
    private int completedBefore;

    private const float RespawnPenalty = 3f;

    // ================================================================ start

    private void StartChase()
    {
        int level = Mathf.Clamp(RaceSetup.chaseLevel, 1, ChaseLevels.Count);
        RaceSetup.chaseLevel = level;
        completedBefore = ChaseLevels.Completed;

        // Prostředí podle levelu (louka, poušť, sníh, pobřeží, noční město)
        if (world != null)
        {
            Transform old = world.Find("Checkpoints");
            if (old != null) old.name = "Checkpoints_old";
            Destroy(world.gameObject);
        }
        theme = TrackLibrary.Themes[ChaseLevels.ThemeIndex(level)]().Finish();
        endTheme = theme;
        settings = new TrackGenerator.TrackSettings
        {
            seed = 500 + level * 13, treeCount = theme.treeCount, rockCount = theme.rockCount, cloudCount = theme.cloudCount,
        };
        definition = new TrackLibrary.TrackDefinition { id = "chase_" + level, name = ChaseLevels.Name(level), description = "" };
        materials.Clear();
        LampCuller.Darkness = theme.night ? 1f : 0f;
        SetupEnvironment();

        world = new GameObject("World").transform;
        course = ObstacleCourse.Generate(level);
        ObstacleCourse.Build(world, course, theme, new Palette(this), settings.seed, settings);

        PlayMusic(ProceduralAudio.RaceMusic);
        SkidMarks.Create(Mat("skid", new Color(0.09f, 0.09f, 0.1f), 0.05f));
        ParticleFX.Create(
            Mat("smoke", new Color(0.85f, 0.85f, 0.88f), 0.05f),
            Mat("dust", theme["vergeA"] * 1.1f, 0.02f),
            Mat("spark", new Color(1f, 0.6f, 0.2f), 0.3f, new Color(1f, 0.6f, 0.2f) * 3f),
            Mat("splash", new Color(0.75f, 0.82f, 0.9f), 0.6f));
        replay = new GameObject("Replay").AddComponent<ReplaySystem>();
        replay.SetTvSpots(ChaseTvSpots());

        // Hráč
        int start = course.playerStart;
        Vector3 pos = course.points[start] + Vector3.up * 0.6f;
        Quaternion rot = Quaternion.LookRotation(course.Forward(start), Vector3.up);
        LowPolyCarFactory.Palette palette = PlayerPalette();
        GameObject go = LowPolyCarFactory.Build("PlayerCar", pos, rot, RaceSetup.carStyle, palette, CarLayer, out chaseCar,
            new LowPolyCarFactory.Options { number = RaceSetup.number, pattern = RaceSetup.pattern, headlights = theme.night, headlightIntensity = 2.6f });
        go.AddComponent<AirControl>();
        AddCarExtras(go, palette, false);
        go.SetActive(true);
        replay.Add(chaseCar, "YOU");

        // Policie
        LowPolyCarFactory.Palette policePalette = PolicePalette();
        int count = ChaseLevels.PoliceCount(level);
        for (int k = 0; k < count; k++)
        {
            int i = Mathf.Max(0, course.policeStart - k * 3);
            Vector3 p = course.points[i] + course.Right(i) * (k % 2 == 0 ? -2.5f : 2.5f) * (k == 0 ? 0f : 1f) + Vector3.up * 0.6f;
            GameObject pg = LowPolyCarFactory.Build("Police", p, Quaternion.LookRotation(course.Forward(i), Vector3.up),
                LowPolyCarFactory.BodyStyle.Police, policePalette, CarLayer, out AICar pc,
                new LowPolyCarFactory.Options { number = 10 + k, pattern = 2, headlights = theme.night, headlightIntensity = 1.6f });
            var ai = pg.AddComponent<ChaseAI>();
            ai.plan = course;
            ai.target = chaseCar;
            ai.skill = ChaseLevels.PoliceSkill(level) * (1f - k * 0.03f);
            var air = pg.AddComponent<AirControl>();
            air.playerControlled = false;
            pg.AddComponent<PoliceLights>();
            AddCarExtras(pg, policePalette, true);
            pg.SetActive(true);
            police.Add(ai);
            replay.Add(pc, "POLICE " + (k + 1));
        }

        CreateCameras(chaseCar, 0);
        CreateChaseHud(level);
        FinishLine.Crossed += OnFinishCrossed;
        BoostPad.Boosted += OnBoosted;
        StartCoroutine(ChaseCountdown(level));
    }

    private void OnDestroy()
    {
        FinishLine.Crossed -= OnFinishCrossed;
        BoostPad.Boosted -= OnBoosted;
    }

    private Vector3[] ChaseTvSpots()
    {
        var spots = new List<Vector3>();
        for (int i = 0, k = 0; i < course.Count; i += 22, k++)
            spots.Add(course.points[i] + course.Right(i) * (k % 2 == 0 ? 14f : -14f) + Vector3.up * 7f);
        return spots.ToArray();
    }

    private LowPolyCarFactory.Palette PolicePalette()
    {
        LowPolyCarFactory.Palette p = CarPalette(new Color(0.08f, 0.1f, 0.2f));
        p.siren = Mat("sirenBlue", new Color(0.1f, 0.2f, 0.9f), 0.5f, new Color(0.2f, 0.4f, 1f) * 2.5f);
        p.taillight = Mat("sirenRed", new Color(0.8f, 0.05f, 0.05f), 0.5f, new Color(1f, 0.1f, 0.05f) * 2.5f);
        return p;
    }

    private IEnumerator ChaseCountdown(int level)
    {
        chaseCar.InputEnabled = false;
        yield return new WaitForSeconds(0.6f);
        for (int i = 3; i > 0; i--)
        {
            ChaseMessage(i.ToString(), 1.1f, Color.white);
            yield return new WaitForSeconds(1f);
        }
        chaseCar.InputEnabled = true;
        chaseActive = true;
        ChaseMessage("GO!", 1f, new Color(0.3f, 1f, 0.4f));
        // Policie se rozjede o chvilku později (v těžších levelech dřív)
        yield return new WaitForSeconds(Mathf.Lerp(2.2f, 1.0f, (level - 1) / 14f));
        foreach (var p in police) p.released = true;
        ChaseMessage("POLICE!", 1.2f, new Color(1f, 0.3f, 0.3f));
    }

    // ================================================================ HUD

    private void CreateChaseHud(int level)
    {
        raceCanvas = LowPolyUI.CreateCanvas("ChaseCanvas", 10);
        Transform root = raceCanvas.transform;
        float alpha = GameSettings.HighContrast ? 0.9f : 0.6f;

        RectTransform panel = LowPolyUI.Panel(root, "ChasePanel", new Color(0.03f, 0.05f, 0.1f, alpha));
        LowPolyUI.Place(panel, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(440f, 200f));
        RectTransform stripe = LowPolyUI.Panel(panel, "Stripe", new Color(1f, 0.25f, 0.2f));
        LowPolyUI.Place(stripe, new Vector2(0f, 1f), Vector2.zero, new Vector2(10f, 200f));
        Text title = LowPolyUI.Label(panel, "LEVEL " + level, 40, TextAnchor.MiddleLeft, Color.white, "Level");
        LowPolyUI.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(32f, -10f), new Vector2(400f, 50f));
        chaseTimeText = LowPolyUI.Label(panel, "0'00\"000", 34, TextAnchor.MiddleLeft, LowPolyUI.Accent, "Time", true);
        LowPolyUI.Place(chaseTimeText.rectTransform, new Vector2(0f, 1f), new Vector2(32f, -64f), new Vector2(400f, 40f));
        string targets = $"3 STARS  {TimeFormat.FormatRacing(ChaseLevels.ThreeStarTime(course.RaceLength))}\n2 STARS  {TimeFormat.FormatRacing(ChaseLevels.TwoStarTime(course.RaceLength))}";
        chaseInfoText = LowPolyUI.Label(panel, targets, 22, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.8f), "Targets", true);
        LowPolyUI.Place(chaseInfoText.rectTransform, new Vector2(0f, 1f), new Vector2(32f, -112f), new Vector2(400f, 70f));

        // Nitro
        RectTransform nitroBg = LowPolyUI.Panel(root, "NitroBar", new Color(0.03f, 0.05f, 0.1f, alpha));
        LowPolyUI.Place(nitroBg, new Vector2(0f, 1f), new Vector2(24f, -234f), new Vector2(440f, 34f));
        Text nitroLabel = LowPolyUI.Label(nitroBg, "NITRO", 20, TextAnchor.MiddleLeft, Color.white, "NitroLabel");
        LowPolyUI.Place(nitroLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(100f, 30f));
        RectTransform barTrack = LowPolyUI.Panel(nitroBg, "Track", new Color(1f, 1f, 1f, 0.12f));
        LowPolyUI.Place(barTrack, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(316f, 16f));
        RectTransform nitroFill = LowPolyUI.Panel(barTrack, "Fill", new Color(0.3f, 0.85f, 1f));
        nitroFill.anchorMin = Vector2.zero;
        nitroFill.anchorMax = new Vector2(0.3f, 1f);
        nitroFill.offsetMin = nitroFill.offsetMax = Vector2.zero;
        Text drift = LowPolyUI.Label(root, "DRIFT!", 44, TextAnchor.MiddleCenter, new Color(1f, 0.6f, 0.2f), "Drift");
        LowPolyUI.Place(drift.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(600f, 60f));
        drift.enabled = false;
        huds.Add(new PlayerHud { nitroFill = nitroFill, drift = drift, chaseCar = chaseCar });

        // Postup po dráze (nahoře uprostřed): start - cíl, hráč a policie
        progressBar = LowPolyUI.Panel(root, "Progress", new Color(0.03f, 0.05f, 0.1f, alpha));
        LowPolyUI.Place(progressBar, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(760f, 18f));
        RectTransform finishFlag = LowPolyUI.Panel(progressBar, "Finish", Color.white);
        LowPolyUI.Place(finishFlag, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(8f, 34f));
        for (int k = 0; k < police.Count; k++)
            policeMarkers.Add(Marker(progressBar, new Color(0.3f, 0.45f, 1f), 18f));
        playerMarker = Marker(progressBar, CarColors[RaceSetup.colorIndex], 24f);

        policeText = LowPolyUI.Label(root, "", 30, TextAnchor.UpperCenter, Color.white, "PoliceDistance", true);
        LowPolyUI.Place(policeText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(600f, 40f));

        // BUSTED - plní se, když je policie těsně u hráče
        RectTransform bustedBg = LowPolyUI.Panel(root, "Busted", new Color(0.03f, 0.05f, 0.1f, alpha));
        LowPolyUI.Place(bustedBg, new Vector2(0.5f, 1f), new Vector2(0f, -108f), new Vector2(420f, 26f));
        bustedFill = LowPolyUI.Panel(bustedBg, "Fill", new Color(1f, 0.2f, 0.15f));
        bustedFill.anchorMin = Vector2.zero;
        bustedFill.anchorMax = new Vector2(0f, 1f);
        bustedFill.offsetMin = bustedFill.offsetMax = Vector2.zero;
        Text bustedLabel = LowPolyUI.Label(bustedBg, "BUSTED", 18, TextAnchor.MiddleCenter, Color.white, "Label");
        LowPolyUI.Stretch(bustedLabel.rectTransform);

        chaseMessage = LowPolyUI.Label(root, "", 90, TextAnchor.MiddleCenter, Color.white, "Message", true);
        LowPolyUI.Place(chaseMessage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1400f, 250f));
        chaseMessage.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

        // Rychloměr
        speedLabel = LowPolyUI.Label(root, "", 40, TextAnchor.LowerRight, Color.white, "Speed", true);
        LowPolyUI.Place(speedLabel.rectTransform, new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(400f, 60f));
        speedLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

        // Pauza
        RectTransform pause = LowPolyUI.Panel(root, "Pause", new Color(0.02f, 0.04f, 0.08f, 0.85f));
        LowPolyUI.Stretch(pause);
        pause.gameObject.SetActive(false);
        Text paused = LowPolyUI.Label(pause, "PAUSED", 96, TextAnchor.MiddleCenter, Color.white, "Title");
        LowPolyUI.Place(paused.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(900f, 140f));
        Text name = LowPolyUI.Label(pause, $"LEVEL {level}   |   {ChaseLevels.Name(level)}", 30, TextAnchor.MiddleCenter, LowPolyUI.Accent, "Name", true);
        LowPolyUI.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(1200f, 40f));
        var manager = new GameObject("ChaseManager");
        pauseMenu = manager.AddComponent<PauseMenu>();
        pauseMenu.pauseMenuUI = pause.gameObject;
        manager.AddComponent<TuningOverlay>().car = chaseCar;
        string[] labels = { "RESUME", "RESTART", "LEVELS", "MAIN MENU" };
        System.Action[] actions = { () => pauseMenu.Resume(), () => pauseMenu.Restart(), OpenLevelsFromRace, () => pauseMenu.LoadMenu() };
        for (int i = 0; i < labels.Length; i++)
        {
            Image b = LowPolyUI.Button(pause, labels[i], 34, actions[i]);
            LowPolyUI.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 150f - i * 92f), new Vector2(420f, 76f));
        }

        if (GameSettings.UseTouch) TouchControls.Create(root);
        pause.SetAsLastSibling();
    }

    private Text speedLabel;

    private static RectTransform Marker(RectTransform bar, Color color, float size)
    {
        RectTransform m = LowPolyUI.Panel(bar, "Marker", color);
        m.anchorMin = m.anchorMax = new Vector2(0f, 0.5f);
        m.pivot = new Vector2(0.5f, 0.5f);
        m.sizeDelta = new Vector2(size, size);
        m.localRotation = Quaternion.Euler(0f, 0f, 45f);
        m.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        return m;
    }

    private void ChaseMessage(string text, float duration, Color color)
    {
        if (chaseMessage == null) return;
        chaseMessage.text = text;
        chaseMessage.color = color;
        messageUntil = Time.time + duration;
    }

    private void OpenLevelsFromRace()
    {
        RaceSetup.configured = false;
        RaceSetup.screen = RaceSetup.Screen.Levels;
        pauseMenu.Restart();
    }

    // ================================================================ průběh

    private void UpdateChase()
    {
        if (course == null || chaseCar == null) return;
        if (chaseMessage != null) chaseMessage.enabled = Time.time < messageUntil && !PauseMenu.GameIsPaused;
        if (speedLabel != null) speedLabel.text = Mathf.RoundToInt(chaseCar.SpeedKmh) + " KMH";
        if (!chaseActive || chaseOver) return;

        float dt = Time.deltaTime;
        chaseTime += dt;
        chaseIndex = course.Nearest(chaseCar.transform.position, chaseIndex, 8, 40);
        furthestIndex = Mathf.Max(furthestIndex, chaseIndex);
        foreach (var p in police) p.SetTargetIndex(chaseIndex);
        chaseTimeText.text = TimeFormat.FormatRacing(chaseTime);

        // Pád z dráhy, převrácení nebo reset
        if (chaseCar.transform.position.y < course.points[chaseIndex].y - 6f) RespawnPlayer("FELL OFF");
        if (Vector3.Dot(chaseCar.transform.up, Vector3.up) < 0.3f) flipTimer += dt; else flipTimer = 0f;
        if (flipTimer > 2.5f) RespawnPlayer("FLIPPED");
        if (GameInput.Pressed(GameInput.Action.Reset) && !PauseMenu.GameIsPaused) RespawnPlayer("RESET");

        // Skok přes 30 m (úspěch HIGH FLYER)
        bool grounded = chaseCar.wheelColliderLeftBack.isGrounded || chaseCar.wheelColliderRightBack.isGrounded
                        || chaseCar.wheelColliderLeftFront.isGrounded || chaseCar.wheelColliderRightFront.isGrounded;
        if (!grounded && !airborne) { airborne = true; takeoff = chaseCar.transform.position; }
        if (grounded && airborne)
        {
            airborne = false;
            Vector3 jump = chaseCar.transform.position - takeoff;
            jump.y = 0f;
            if (jump.magnitude > 30f) Achievements.Unlock("high_flyer");
            if (jump.magnitude > 15f) ChaseMessage($"JUMP {jump.magnitude:0} M", 1.2f, LowPolyUI.Accent);
        }

        // Policie: vzdálenost a BUSTED
        float closest = float.MaxValue;
        float nearestGap = float.MaxValue;
        bool touching = false;
        foreach (var p in police)
        {
            float dist = Vector3.Distance(p.transform.position, chaseCar.transform.position);
            closest = Mathf.Min(closest, dist);
            float gap = course.distance[chaseIndex] - course.distance[p.Index];
            if (gap > -10f) nearestGap = Mathf.Min(nearestGap, Mathf.Max(0f, gap));
            if (dist < 6f) touching = true;
        }
        if (touching && police.Count > 0 && police[0].released)
            bustedMeter += dt * (chaseCar.SpeedKmh < 30f ? 0.9f : 0.4f);
        else
            bustedMeter = Mathf.Max(0f, bustedMeter - dt * 0.35f);
        bustedFill.anchorMax = new Vector2(Mathf.Clamp01(bustedMeter), 1f);
        if (bustedMeter >= 1f) { Busted(); return; }

        policeText.text = nearestGap < float.MaxValue ? $"POLICE  {nearestGap:0} M" : "";
        policeText.color = nearestGap < 25f ? new Color(1f, 0.35f, 0.3f) : Color.white;

        // Ukazatel postupu
        float total = Mathf.Max(1f, course.length);
        float width = progressBar.rect.width;
        playerMarker.anchoredPosition = new Vector2(Mathf.Clamp01(course.distance[chaseIndex] / total) * width, 0f);
        for (int k = 0; k < police.Count; k++)
            policeMarkers[k].anchoredPosition = new Vector2(Mathf.Clamp01(course.distance[police[k].Index] / total) * width, 0f);

        foreach (var h in huds)
        {
            if (h.chaseCar == null) continue;
            h.nitroFill.anchorMax = new Vector2(Mathf.Clamp01(chaseCar.Nitro), 1f);
            h.nitroFill.GetComponent<Image>().color = chaseCar.NitroActive ? new Color(1f, 0.55f, 0.15f) : new Color(0.3f, 0.85f, 1f);
            h.drift.enabled = !PauseMenu.GameIsPaused && chaseCar.DriftAmount > 0.35f;
        }
    }

    private void RespawnPlayer(string reason)
    {
        int i = course.SafeBefore(chaseIndex);
        chaseCar.Teleport(course.points[i] + Vector3.up * 0.8f, Quaternion.LookRotation(course.Forward(i), Vector3.up));
        chaseIndex = i;
        flipTimer = 0f;
        airborne = false;
        chaseTime += RespawnPenalty;
        bustedMeter = Mathf.Max(0f, bustedMeter - 0.3f);
        // Policii, která je moc blízko, posunout dozadu - ať hráč hned po návratu nemá BUSTED
        foreach (var p in police)
            if (Mathf.Abs(course.distance[p.Index] - course.distance[i]) < 45f)
                p.RespawnBehind(i, 18);
        ChaseMessage(reason + "\n+" + RespawnPenalty.ToString("0") + " S", 1.5f, new Color(1f, 0.8f, 0.3f));
    }

    private void OnBoosted(CarController car)
    {
        if (car == chaseCar) ChaseMessage("BOOST!", 0.8f, new Color(0.3f, 0.9f, 1f));
    }

    private void OnFinishCrossed(CarController car)
    {
        if (car != chaseCar || !chaseActive || chaseOver) return;
        chaseOver = true;
        chaseCar.InputEnabled = false;
        foreach (var p in police) p.released = false;
        ChaseMessage("ESCAPED!", 2f, new Color(0.4f, 1f, 0.5f));
        StartCoroutine(ChaseResults(true));
    }

    private void Busted()
    {
        chaseOver = true;
        chaseCar.InputEnabled = false;
        foreach (var p in police) p.released = false;
        ChaseMessage("BUSTED!", 2.5f, new Color(1f, 0.25f, 0.2f));
        StartCoroutine(ChaseResults(false));
    }

    // ================================================================ výsledky

    private IEnumerator ChaseResults(bool escaped)
    {
        int level = RaceSetup.chaseLevel;
        int stars = escaped ? ChaseLevels.StarsFor(chaseTime, course.RaceLength) : 0;
        bool newBest = escaped && ChaseLevels.Save(level, stars, chaseTime);
        int completed = ChaseLevels.Completed;
        if (completed >= 5) Achievements.Unlock("getaway");
        if (completed >= 10) Achievements.Unlock("most_wanted");
        if (completed >= 15) Achievements.Unlock("legend");

        yield return new WaitForSeconds(1.8f);
        if (pauseMenu != null)
        {
            pauseMenu.Resume();
            pauseMenu.enabled = false;
        }

        RectTransform panel = LowPolyUI.Panel(raceCanvas.transform, "Results", new Color(0.03f, 0.05f, 0.1f, 0.92f));
        LowPolyUI.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 720f));
        Text title = LowPolyUI.Label(panel, $"LEVEL {level}   |   {ChaseLevels.Name(level)}", 30, TextAnchor.MiddleCenter, Dim, "Title", true);
        LowPolyUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(860f, 40f));
        Text big = LowPolyUI.Label(panel, escaped ? "ESCAPED!" : "BUSTED!", 110, TextAnchor.MiddleCenter,
            escaped ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.3f, 0.25f), "Big");
        LowPolyUI.Place(big.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(860f, 140f));

        float y = -230f;
        if (escaped)
        {
            // Hvězdy jako low-poly kosočtverce
            for (int i = 0; i < 3; i++)
            {
                RectTransform s = LowPolyUI.Panel(panel, "Star", i < stars ? LowPolyUI.Accent : new Color(1f, 1f, 1f, 0.15f));
                LowPolyUI.Place(s, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 110f, y - 30f), new Vector2(64f, 64f));
                s.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            y -= 100f;
            ChaseRow(panel, "TIME", TimeFormat.FormatRacing(chaseTime), y);
            y -= 44f;
            ChaseRow(panel, "BEST", TimeFormat.FormatRacing(ChaseLevels.BestTime(level)), y);
            y -= 44f;
            ChaseRow(panel, "3 STARS UNDER", TimeFormat.FormatRacing(ChaseLevels.ThreeStarTime(course.RaceLength)), y);
            y -= 50f;
            if (newBest)
            {
                Text nb = LowPolyUI.Label(panel, "NEW BEST TIME!", 28, TextAnchor.MiddleCenter, LowPolyUI.Accent, "NewBest");
                LowPolyUI.Place(nb.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(860f, 36f));
                y -= 44f;
            }
            // Nově odemčená auta
            foreach (var style in new[] { LowPolyCarFactory.BodyStyle.Monster, LowPolyCarFactory.BodyStyle.Police, LowPolyCarFactory.BodyStyle.Rocket })
            {
                int need = ChaseLevels.RequiredFor(style);
                if (completedBefore < need && completed >= need)
                {
                    Text u = LowPolyUI.Label(panel, $"NEW CAR UNLOCKED   {LowPolyCarFactory.StyleNames[(int)style]}", 32, TextAnchor.MiddleCenter, new Color(0.4f, 1f, 0.5f), "Unlock");
                    LowPolyUI.Place(u.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(860f, 40f));
                    y -= 46f;
                }
            }
        }
        else
        {
            float percent = Mathf.Clamp01(course.distance[furthestIndex] / Mathf.Max(1f, course.length)) * 100f;
            Text info = LowPolyUI.Label(panel, $"YOU MADE IT {percent:0} PERCENT OF THE WAY", 30, TextAnchor.MiddleCenter, Color.white, "Info", true);
            LowPolyUI.Place(info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y - 20f), new Vector2(860f, 40f));
            Text tip = LowPolyUI.Label(panel, "TIP  -  USE NITRO ON STRAIGHTS AND DON'T STOP NEXT TO THE POLICE", 20, TextAnchor.MiddleCenter, Dim, "Tip", true);
            LowPolyUI.Place(tip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y - 70f), new Vector2(860f, 30f));
        }

        var labels = new List<string>();
        var actions = new List<System.Action>();
        if (escaped && level < ChaseLevels.Count)
        {
            labels.Add("NEXT LEVEL");
            actions.Add(() => { RaceSetup.chaseLevel = level + 1; pauseMenu.Restart(); });
        }
        labels.Add("RETRY");
        actions.Add(() => pauseMenu.Restart());
        labels.Add("REPLAY");
        actions.Add(() =>
        {
            raceCanvas.enabled = false;
            replay.Play(() => { if (raceCanvas != null) raceCanvas.enabled = true; });
        });
        labels.Add("LEVELS");
        actions.Add(OpenLevelsFromRace);

        float w = 190f, gap = 14f;
        float startX = -(labels.Count - 1) * (w + gap) * 0.5f;
        for (int i = 0; i < labels.Count; i++)
        {
            Image b = i == 0 ? AccentButton(panel, labels[i], 24, actions[i]) : LowPolyUI.Button(panel, labels[i], 24, actions[i]);
            LowPolyUI.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(startX + i * (w + gap), 36f), new Vector2(w, 72f));
        }
    }

    private static void ChaseRow(RectTransform panel, string label, string value, float y)
    {
        Text l = LowPolyUI.Label(panel, label, 26, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.75f), label, true);
        LowPolyUI.Place(l.rectTransform, new Vector2(0.5f, 1f), new Vector2(-150f, y), new Vector2(320f, 36f));
        Text v = LowPolyUI.Label(panel, value, 28, TextAnchor.MiddleRight, Color.white, label + "Value", true);
        LowPolyUI.Place(v.rectTransform, new Vector2(0.5f, 1f), new Vector2(150f, y), new Vector2(300f, 36f));
    }

    // ================================================================ výběr levelu (menu)

    private void BuildLevelsScreen()
    {
        Transform root = menuCanvas.transform;
        Text title = LowPolyUI.Label(root, "POLICE CHASE", 64, TextAnchor.UpperLeft, Color.white, "Title");
        LowPolyUI.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -40f), new Vector2(900f, 80f));
        Text sub = LowPolyUI.Label(root, $"REACH THE FINISH BEFORE THE POLICE CATCH YOU   |   COMPLETED {ChaseLevels.Completed} OF {ChaseLevels.Count}   |   STARS {ChaseLevels.TotalStars}",
            22, TextAnchor.UpperLeft, LowPolyUI.Accent, "Subtitle", true);
        LowPolyUI.Place(sub.rectTransform, new Vector2(0f, 1f), new Vector2(52f, -112f), new Vector2(1600f, 30f));

        Color[] themeColors = { TrackTheme.Meadow()["groundA"], TrackTheme.Desert()["groundA"], TrackTheme.Snow()["groundA"], TrackTheme.Coast()["water"], new Color(0.8f, 0.2f, 0.7f) };
        for (int n = 1; n <= ChaseLevels.Count; n++)
        {
            int level = n;
            bool open = ChaseLevels.Unlocked(level);
            int col = (n - 1) % 5, row = (n - 1) / 5;
            Image tile = LowPolyUI.Button(root, "", 10, () =>
            {
                if (!ChaseLevels.Unlocked(level)) return;
                StartChaseFromMenu(level);
            });
            tile.color = open ? LowPolyUI.PanelColor : new Color(0.03f, 0.05f, 0.1f, 0.45f);
            LowPolyUI.Place((RectTransform)tile.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f + col * 236f, -170f - row * 196f), new Vector2(220f, 182f));

            Text num = LowPolyUI.Label(tile.transform, open ? n.ToString() : "LOCKED", open ? 64 : 26, TextAnchor.UpperCenter,
                open ? Color.white : new Color(1f, 1f, 1f, 0.4f), "Number");
            LowPolyUI.Place(num.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, open ? -8f : -40f), new Vector2(210f, 80f));
            Text nm = LowPolyUI.Label(tile.transform, ChaseLevels.Name(n), 16, TextAnchor.UpperCenter, Dim, "Name", true);
            LowPolyUI.Place(nm.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(210f, 24f));
            int stars = ChaseLevels.Stars(n);
            for (int s = 0; s < 3; s++)
            {
                RectTransform st = LowPolyUI.Panel(tile.transform, "Star", s < stars ? LowPolyUI.Accent : new Color(1f, 1f, 1f, 0.15f));
                LowPolyUI.Place(st, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2((s - 1) * 40f, 34f), new Vector2(22f, 22f));
                st.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            RectTransform stripe = LowPolyUI.Panel(tile.transform, "Stripe", themeColors[ChaseLevels.ThemeIndex(n)]);
            LowPolyUI.Place(stripe, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(220f, 8f));
        }

        // Auto a speciální auta (vpravo)
        RectTransform right = LowPolyUI.Panel(root, "ChaseCarPanel", LowPolyUI.PanelColor);
        LowPolyUI.Place(right, new Vector2(1f, 1f), new Vector2(-40f, -170f), new Vector2(560f, 570f));
        Header(right, "YOUR CAR", -16f);
        Image carButton = LowPolyUI.Button(right, LowPolyCarFactory.StyleNames[(int)RaceSetup.carStyle], 28, null, out Text carLabel);
        carButton.GetComponent<Button>().onClick.AddListener(() =>
        {
            int s = (int)RaceSetup.carStyle;
            do { s = (s + 1) % LowPolyCarFactory.StyleNames.Length; } while (!Profile.IsUnlocked((LowPolyCarFactory.BodyStyle)s));
            RaceSetup.carStyle = (LowPolyCarFactory.BodyStyle)s;
            carLabel.text = LowPolyCarFactory.StyleNames[s];
            SpawnPreviewCar();
        });
        LowPolyUI.Place((RectTransform)carButton.transform, new Vector2(0f, 1f), new Vector2(30f, -52f), new Vector2(500f, 60f));
        Text carHint = LowPolyUI.Label(right, "CLICK TO CHANGE   -   COLORS IN QUICK RACE GARAGE", 16, TextAnchor.UpperLeft, Dim, "CarHint", true);
        LowPolyUI.Place(carHint.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -118f), new Vector2(500f, 22f));

        Header(right, "SPECIAL CARS", -170f);
        float y = -206f;
        foreach (var style in new[] { LowPolyCarFactory.BodyStyle.Monster, LowPolyCarFactory.BodyStyle.Police, LowPolyCarFactory.BodyStyle.Rocket })
        {
            int need = ChaseLevels.RequiredFor(style);
            bool has = ChaseLevels.Completed >= need;
            RectTransform card = LowPolyUI.Panel(right, "Special", has ? new Color(0.25f, 0.2f, 0.05f, 0.85f) : new Color(1f, 1f, 1f, 0.06f));
            LowPolyUI.Place(card, new Vector2(0f, 1f), new Vector2(30f, y), new Vector2(500f, 100f));
            Text n = LowPolyUI.Label(card, LowPolyCarFactory.StyleNames[(int)style], 32, TextAnchor.UpperLeft, has ? LowPolyUI.Accent : Color.white, "Name");
            LowPolyUI.Place(n.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -10f), new Vector2(460f, 40f));
            string state = has ? "UNLOCKED" : $"FINISH {need} LEVELS   ({Mathf.Min(ChaseLevels.Completed, need)} OF {need})";
            Text st = LowPolyUI.Label(card, state, 20, TextAnchor.UpperLeft, Dim, "State", true);
            LowPolyUI.Place(st.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -58f), new Vector2(460f, 28f));
            y -= 116f;
        }

        Text keys = LowPolyUI.Label(root, "THROTTLE AND BRAKE TILT THE CAR IN THE AIR   |   R RETURNS YOU TO THE ROAD (+3 S)", 18, TextAnchor.LowerLeft, Dim, "Keys", true);
        LowPolyUI.Place(keys.rectTransform, new Vector2(0f, 0f), new Vector2(300f, 60f), new Vector2(1300f, 30f));
        Image back = LowPolyUI.Button(root, "< BACK", 28, () => ShowScreen(RaceSetup.Screen.Title));
        LowPolyUI.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(220f, 70f));
    }

    private void StartChaseFromMenu(int level)
    {
        RaceSetup.mode = RaceSetup.Mode.Chase;
        RaceSetup.chaseLevel = level;
        RaceSetup.configured = true;
        if (previewCar != null) Destroy(previewCar);
        if (menuCamera != null) Destroy(menuCamera.gameObject);
        if (menuCanvas != null) Destroy(menuCanvas.gameObject);
        StartCoroutine(StartChaseNextFrame());
    }

    private IEnumerator StartChaseNextFrame()
    {
        yield return null;
        StartChase();
    }
}
