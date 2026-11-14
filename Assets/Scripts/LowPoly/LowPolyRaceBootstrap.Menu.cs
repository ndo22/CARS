using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Obrazovky menu: titulní, výběr závodu, nastavení, editor tratí, úspěchy.</summary>
public partial class LowPolyRaceBootstrap
{
    private Canvas menuCanvas;
    private GameObject previewCar;
    private Camera menuCamera;
    private Text statusText;
    private RectTransform[] statBars;
    private GameInput.Action? waitingForKey;
    private Text waitingLabel;
    private static KeyCode[] allKeys;
    private TrackEditor trackEditor;
    private static int trackPage;
    private static int setupTab;
    private const int TracksPerPage = 5;

    private static readonly Color Dim = new Color(1f, 1f, 1f, 0.7f);
    private static readonly Color Dark = new Color(0.1f, 0.08f, 0.05f);

    // ================================================================ společné

    private void ShowScreen(RaceSetup.Screen screen)
    {
        RaceSetup.screen = screen;
        if (menuCanvas != null) Destroy(menuCanvas.gameObject);
        waitingForKey = null;

        if (screen == RaceSetup.Screen.Editor)
        {
            OpenEditor();
            return;
        }

        menuCanvas = LowPolyUI.CreateCanvas("MenuCanvas");
        if (menuCamera == null)
        {
            var camGo = new GameObject("MenuCamera");
            camGo.tag = "MainCamera";
            menuCamera = SetupCamera(camGo);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<OrbitCamera>();
            if (weather != null) weather.followCamera = camGo.transform;
        }
        if (previewCar == null) SpawnPreviewCar();
        PlayMusic(ProceduralAudio.MenuMusic);

        switch (screen)
        {
            case RaceSetup.Screen.Setup: BuildSetupScreen(); break;
            case RaceSetup.Screen.Settings: BuildSettingsScreen(); break;
            case RaceSetup.Screen.Achievements: BuildAchievementsScreen(); break;
            case RaceSetup.Screen.Levels: BuildLevelsScreen(); break;
            default: BuildTitleScreen(); break;
        }
    }

    private void Header(RectTransform parent, string text, float y, float x = 30f)
    {
        Text header = LowPolyUI.Label(parent, text, 22, TextAnchor.UpperLeft, Dim, text + "Header");
        LowPolyUI.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(x, y - 6f), new Vector2(400f, 30f));
    }

    private LowPolyUI.ChoiceGroup ChoiceRow(RectTransform parent, float x, float y, string[] labels, int selected, float width,
        Action<int> onSelect, float height = 48f, int fontSize = 22, Func<int, bool> allowed = null, Func<int, string> lockedHint = null)
    {
        var group = new LowPolyUI.ChoiceGroup();
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            bool ok = allowed == null || allowed(i);
            Image b = LowPolyUI.Button(parent, ok ? labels[i] : "LOCKED", fontSize, () =>
            {
                if (allowed != null && !allowed(index))
                {
                    if (statusText != null && lockedHint != null) statusText.text = $"{labels[index]}  -  {lockedHint(index)} TO UNLOCK";
                    return;
                }
                group.Select(index);
                if (statusText != null) statusText.text = "";
                onSelect(index);
            }, out Text label);
            if (!ok) label.color = new Color(1f, 1f, 1f, 0.4f);
            LowPolyUI.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x + i * (width + 10f), y), new Vector2(width, height));
            group.Add(b, label, LowPolyUI.ButtonColor);
        }
        group.Select(Mathf.Max(0, selected));
        return group;
    }

    private Image AccentButton(Transform parent, string text, int size, Action action)
    {
        Image b = LowPolyUI.Button(parent, text, size, action, out Text label);
        b.color = LowPolyUI.Accent;
        label.color = Dark;
        return b;
    }

    private void SpawnPreviewCar()
    {
        if (previewCar != null) Destroy(previewCar);
        GridSlot(0, out Vector3 pos, out Quaternion rot);
        previewCar = LowPolyCarFactory.Build("PreviewCar", pos, rot, RaceSetup.carStyle, PlayerPalette(), CarLayer, out Car car,
            new LowPolyCarFactory.Options { number = RaceSetup.number, pattern = RaceSetup.pattern, headlights = theme.night || RaceSetup.dynamicTime });
        car.InputEnabled = false;
        previewCar.SetActive(true);
        if (menuCamera != null) menuCamera.GetComponent<OrbitCamera>().target = previewCar.transform;
    }

    private LowPolyCarFactory.Palette PlayerPalette()
    {
        LowPolyCarFactory.Palette p = CarPalette(CarColors[RaceSetup.colorIndex], RaceSetup.colorIndex == 9);
        p.hub = Mat("rim" + RaceSetup.rims, Cosmetics.RimColors[Mathf.Clamp(RaceSetup.rims, 0, 3)], 0.6f);
        return p;
    }

    private void ReloadPreview()
    {
        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        // Ukázat "LOADING" a stavět trať až v dalším snímku, ať menu nezamrzne bez odezvy
        Text loading = null;
        if (menuCanvas != null)
        {
            loading = LowPolyUI.Label(menuCanvas.transform, "LOADING", 40, TextAnchor.MiddleCenter, Color.white, "Loading");
            LowPolyUI.Place(loading.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 80f));
        }
        yield return null;
        LoadTrack(RaceSetup.trackIndex);
        if (menuCamera != null) ConfigureCameraSky(menuCamera);
        if (weather != null && menuCamera != null) weather.followCamera = menuCamera.transform;
        SpawnPreviewCar();
        if (loading != null) Destroy(loading.gameObject);
    }

    private void UpdateMenu()
    {
        if (waitingForKey == null) return;
        if (allKeys == null) allKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));
        foreach (KeyCode key in allKeys)
        {
            if (key == KeyCode.None || (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6) || key >= KeyCode.JoystickButton0) continue;
            if (!Input.GetKeyDown(key)) continue;
            if (key != KeyCode.Escape || waitingForKey == GameInput.Action.Pause)
                GameInput.Rebind(waitingForKey.Value, key);
            if (waitingLabel != null) waitingLabel.text = GameInput.GetKey(waitingForKey.Value).ToString().ToUpper();
            waitingForKey = null;
            break;
        }
    }

    // ================================================================ titulní obrazovka

    private void BuildTitleScreen()
    {
        Transform root = menuCanvas.transform;
        Text title = LowPolyUI.Label(root, "LOW POLY", 120, TextAnchor.UpperLeft, Color.white, "Title");
        LowPolyUI.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -50f), new Vector2(900f, 134f));
        Text subtitle = LowPolyUI.Label(root, "RACING", 55, TextAnchor.UpperLeft, LowPolyUI.Accent, "Subtitle");
        LowPolyUI.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(54f, -174f), new Vector2(800f, 60f));

        string[] labels = { "QUICK RACE", "POLICE CHASE", "CHAMPIONSHIP", "TIME TRIAL", "TRACK EDITOR", "ACHIEVEMENTS", "CLASSIC TRACK", "SETTINGS", "QUIT" };
        Action[] actions =
        {
            () => OpenSetup(RaceSetup.Mode.QuickRace),
            () => ShowScreen(RaceSetup.Screen.Levels),
            () => OpenSetup(RaceSetup.Mode.Championship),
            () => OpenSetup(RaceSetup.Mode.TimeTrial),
            () => ShowScreen(RaceSetup.Screen.Editor),
            () => ShowScreen(RaceSetup.Screen.Achievements),
            () => SceneManager.LoadScene("SampleScene"),
            () => ShowScreen(RaceSetup.Screen.Settings),
            () => { Debug.Log("QUIT"); Application.Quit(); },
        };
        for (int i = 0; i < labels.Length; i++)
        {
            Image b = i == 0 ? AccentButton(root, labels[i], 32, actions[i]) : LowPolyUI.Button(root, labels[i], 32, actions[i]);
            Text label = b.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.offsetMin = new Vector2(28f, 0f);
            LowPolyUI.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(54f, -262f - i * 70f), new Vector2(440f, 60f));
        }

        Text profile = LowPolyUI.Label(root, $"WINS {Profile.Wins}   |   CHAMPIONSHIPS {Profile.Championships}   |   ACHIEVEMENTS {Achievements.Count} OF {Achievements.All.Length}",
            22, TextAnchor.LowerLeft, Dim, "Profile", true);
        LowPolyUI.Place(profile.rectTransform, new Vector2(0f, 0f), new Vector2(56f, 40f), new Vector2(1000f, 30f));
        Text hint = LowPolyUI.Label(root, "WASD DRIVE   SPACE DRIFT   SHIFT NITRO   R RESET   C CAMERA   F1 TUNING", 18,
            TextAnchor.LowerRight, Dim, "Controls");
        LowPolyUI.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(1100f, 30f));
    }

    private void OpenSetup(RaceSetup.Mode mode)
    {
        RaceSetup.mode = mode;
        if (mode == RaceSetup.Mode.Championship && RaceSetup.trackIndex != 0)
        {
            RaceSetup.trackIndex = 0;
            ReloadPreview();
        }
        ShowScreen(RaceSetup.Screen.Setup);
    }

    // ================================================================ výběr závodu

    private void BuildSetupScreen()
    {
        Transform root = menuCanvas.transform;
        bool championship = RaceSetup.mode == RaceSetup.Mode.Championship;
        bool timeTrial = RaceSetup.mode == RaceSetup.Mode.TimeTrial;

        Text title = LowPolyUI.Label(root, championship ? "CHAMPIONSHIP" : timeTrial ? "TIME TRIAL" : "QUICK RACE", 64,
            TextAnchor.UpperLeft, Color.white, "Title");
        LowPolyUI.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -40f), new Vector2(900f, 80f));
        Text sub = LowPolyUI.Label(root, championship ? $"{Championship.Rounds} RACES - POINTS 10 6 4 3 2 1" :
            timeTrial ? "RACE YOUR OWN GHOST" : "PICK A TRACK AND GO", 24, TextAnchor.UpperLeft, LowPolyUI.Accent, "Subtitle");
        LowPolyUI.Place(sub.rectTransform, new Vector2(0f, 1f), new Vector2(52f, -112f), new Vector2(900f, 30f));

        BuildTrackList(root, championship);

        // --- pravý panel se záložkami
        RectTransform right = LowPolyUI.Panel(root, "SetupPanel", LowPolyUI.PanelColor);
        LowPolyUI.Place(right, new Vector2(1f, 1f), new Vector2(-40f, -170f), new Vector2(580f, 700f));
        string[] tabs = { "CAR", "RACE" };
        ChoiceRow(right, 30f, -16f, tabs, setupTab, 255f, i =>
        {
            setupTab = i;
            ShowScreen(RaceSetup.Screen.Setup);
        }, 50f, 24);

        statusText = LowPolyUI.Label(right, "", 18, TextAnchor.LowerCenter, LowPolyUI.Accent, "Status", true);
        LowPolyUI.Place(statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(540f, 26f));

        if (setupTab == 0) BuildCarTab(right);
        else BuildRaceTab(right, timeTrial);

        // --- start
        RectTransform bottom = LowPolyUI.Panel(root, "StartPanel", LowPolyUI.PanelColor);
        LowPolyUI.Place(bottom, new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(580f, 150f));
        Header(bottom, "LAPS", -14f);
        ChoiceRow(bottom, 30f, -50f, new[] { "1", "3", "5" }, Array.IndexOf(RaceSetup.LapOptions, RaceSetup.laps), 70f,
            i => RaceSetup.laps = RaceSetup.LapOptions[i], 62f, 26);
        Image race = AccentButton(bottom, championship ? "START!" : "RACE!", 46, OnRacePressed);
        LowPolyUI.Place((RectTransform)race.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(270f, 104f));

        Image back = LowPolyUI.Button(root, "< BACK", 28, () => ShowScreen(RaceSetup.Screen.Title));
        LowPolyUI.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(220f, 70f));
    }

    private void BuildTrackList(Transform root, bool championship)
    {
        RectTransform left = LowPolyUI.Panel(root, "TrackPanel", LowPolyUI.PanelColor);
        LowPolyUI.Place(left, new Vector2(0f, 1f), new Vector2(40f, -170f), new Vector2(600f, 700f));
        int total = championship ? TrackLibrary.All.Length : TrackLibrary.Count;
        int pages = Mathf.Max(1, Mathf.CeilToInt(total / (float)TracksPerPage));
        trackPage = Mathf.Clamp(trackPage, 0, pages - 1);
        Header(left, championship ? "ROUNDS" : (pages > 1 ? $"TRACK   {trackPage + 1} OF {pages}" : "TRACK"), -16f);
        if (pages > 1)
        {
            Image prev = LowPolyUI.Button(left, "<", 26, () => { trackPage = (trackPage + pages - 1) % pages; ShowScreen(RaceSetup.Screen.Setup); });
            LowPolyUI.Place((RectTransform)prev.transform, new Vector2(1f, 1f), new Vector2(-90f, -10f), new Vector2(50f, 40f));
            Image next = LowPolyUI.Button(left, ">", 26, () => { trackPage = (trackPage + 1) % pages; ShowScreen(RaceSetup.Screen.Setup); });
            LowPolyUI.Place((RectTransform)next.transform, new Vector2(1f, 1f), new Vector2(-30f, -10f), new Vector2(50f, 40f));
        }

        var choice = new LowPolyUI.ChoiceGroup();
        int selectedSlot = -1;
        for (int slot = 0; slot < TracksPerPage; slot++)
        {
            int index = trackPage * TracksPerPage + slot;
            if (index >= total) break;
            TrackLibrary.TrackDefinition def = TrackLibrary.Get(index);
            Image card = LowPolyUI.Button(left, "", 10, () =>
            {
                if (championship || index == RaceSetup.trackIndex) return;
                RaceSetup.trackIndex = index;
                ShowScreen(RaceSetup.Screen.Setup);
                ReloadPreview();
            });
            LowPolyUI.Place((RectTransform)card.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -62f - slot * 126f), new Vector2(560f, 116f));

            string prefix = championship ? $"{index + 1}. " : "";
            Text name = LowPolyUI.Label(card.transform, prefix + def.name.ToUpper(), 32, TextAnchor.UpperLeft, Color.white, "Name", true);
            LowPolyUI.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -12f), new Vector2(520f, 40f));
            Text desc = LowPolyUI.Label(card.transform, def.description.ToUpper(), 18, TextAnchor.UpperLeft, Dim, "Description", true);
            LowPolyUI.Place(desc.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -52f), new Vector2(520f, 24f));
            float length = TrackGenerator.Length(TrackLibrary.Settings(def));
            float best = Leaderboard.BestTime(def.LeaderboardKey);
            string info = $"{(length / 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} KM   |   BEST {TimeFormat.FormatRacing(best)}";
            Text infoText = LowPolyUI.Label(card.transform, info, 20, TextAnchor.UpperLeft, LowPolyUI.Accent, "Info", true);
            LowPolyUI.Place(infoText.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -82f), new Vector2(520f, 26f));
            RectTransform stripe = LowPolyUI.Panel(card.transform, "Stripe", def.theme()["groundA"]);
            LowPolyUI.Place(stripe, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(16f, 86f));
            choice.Add(card, null, LowPolyUI.ButtonColor);
            if (index == RaceSetup.trackIndex) selectedSlot = slot;
        }
        choice.Select(selectedSlot);
    }

    private void BuildCarTab(RectTransform right)
    {
        float y = -84f;
        Header(right, "CAR", y);
        ChoiceRow(right, 30f, y - 34f, new[] { "SPORT", "MUSCLE", "HATCH" }, (int)RaceSetup.carStyle, 168f, i => SetStyle(i), 46f, 20,
            i => Profile.IsUnlocked((LowPolyCarFactory.BodyStyle)i), i => Profile.UnlockHint((LowPolyCarFactory.BodyStyle)i));
        ChoiceRow(right, 30f, y - 86f, new[] { "PICKUP", "BUGGY", "FORMULA" }, (int)RaceSetup.carStyle - 3, 168f, i => SetStyle(i + 3), 46f, 20,
            i => Profile.IsUnlocked((LowPolyCarFactory.BodyStyle)(i + 3)), i => Profile.UnlockHint((LowPolyCarFactory.BodyStyle)(i + 3)));

        ChoiceRow(right, 30f, y - 138f, new[] { "MONSTER", "POLICE", "ROCKET" }, (int)RaceSetup.carStyle - 6, 168f, i => SetStyle(i + 6), 46f, 20,
            i => Profile.IsUnlocked((LowPolyCarFactory.BodyStyle)(i + 6)), i => Profile.UnlockHint((LowPolyCarFactory.BodyStyle)(i + 6)));
        // Vlastnosti auta
        y -= 202f;
        string[] statNames = { "SPEED", "ACCEL", "GRIP", "HANDLING" };
        statBars = new RectTransform[statNames.Length];
        for (int i = 0; i < statNames.Length; i++)
        {
            Text l = LowPolyUI.Label(right, statNames[i], 18, TextAnchor.MiddleLeft, Dim, statNames[i]);
            LowPolyUI.Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(30f + (i % 2) * 262f, y - (i / 2) * 28f), new Vector2(120f, 24f));
            RectTransform bg = LowPolyUI.Panel(right, statNames[i] + "Bar", new Color(1f, 1f, 1f, 0.12f));
            LowPolyUI.Place(bg, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(140f + (i % 2) * 262f, y - 4f - (i / 2) * 28f), new Vector2(110f, 16f));
            RectTransform fill = LowPolyUI.Panel(bg, "Fill", LowPolyUI.Accent);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.5f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            statBars[i] = fill;
        }
        UpdateStats();

        y -= 66f;
        Header(right, "COLOR", y);
        var colors = new LowPolyUI.ChoiceGroup();
        for (int i = 0; i < CarColors.Length; i++)
        {
            int index = i;
            bool unlocked = Profile.IsColorUnlocked(i);
            Image swatch = LowPolyUI.Button(right, unlocked ? "" : "X", 22, () =>
            {
                if (!Profile.IsColorUnlocked(index))
                {
                    statusText.text = "GOLD AND CHROME  -  WIN A CHAMPIONSHIP TO UNLOCK";
                    return;
                }
                RaceSetup.colorIndex = index;
                colors.Select(index);
                statusText.text = "";
                SpawnPreviewCar();
            });
            LowPolyUI.Place((RectTransform)swatch.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f + i * 52f, y - 34f), new Vector2(44f, 44f));
            colors.Add(swatch, null, unlocked ? CarColors[i] : CarColors[i] * 0.35f);
        }
        colors.Select(RaceSetup.colorIndex);

        y -= 88f;
        Header(right, "PATTERN", y);
        ChoiceRow(right, 30f, y - 34f, Cosmetics.Patterns, RaceSetup.pattern, 122f, i => { RaceSetup.pattern = i; SpawnPreviewCar(); }, 44f, 18,
            Cosmetics.PatternUnlocked, Cosmetics.PatternHint);
        y -= 84f;
        Header(right, "RIMS", y);
        ChoiceRow(right, 30f, y - 34f, Cosmetics.Rims, RaceSetup.rims, 122f, i => { RaceSetup.rims = i; SpawnPreviewCar(); }, 44f, 18,
            Cosmetics.RimUnlocked, Cosmetics.RimHint);
        y -= 84f;
        Header(right, "NUMBER", y);
        Text number = LowPolyUI.Label(right, RaceSetup.number.ToString(), 30, TextAnchor.MiddleCenter, Color.white, "Number", true);
        LowPolyUI.Place(number.rectTransform, new Vector2(0f, 1f), new Vector2(230f, y - 2f), new Vector2(100f, 44f));
        Image minus = LowPolyUI.Button(right, "<", 26, () => { RaceSetup.number = (RaceSetup.number + 98) % 99 + 1; number.text = RaceSetup.number.ToString(); SpawnPreviewCar(); });
        LowPolyUI.Place((RectTransform)minus.transform, new Vector2(0f, 1f), new Vector2(170f, y - 2f), new Vector2(50f, 44f));
        Image plus = LowPolyUI.Button(right, ">", 26, () => { RaceSetup.number = RaceSetup.number % 99 + 1; number.text = RaceSetup.number.ToString(); SpawnPreviewCar(); });
        LowPolyUI.Place((RectTransform)plus.transform, new Vector2(0f, 1f), new Vector2(340f, y - 2f), new Vector2(50f, 44f));
    }

    private void SetStyle(int i)
    {
        RaceSetup.carStyle = (LowPolyCarFactory.BodyStyle)i;
        ShowScreen(RaceSetup.Screen.Setup); // obě řady stylů se musí překreslit
        SpawnPreviewCar();
    }

    private void BuildRaceTab(RectTransform right, bool timeTrial)
    {
        float y = -84f;
        if (!timeTrial)
        {
            Header(right, "OPPONENTS", y);
            ChoiceRow(right, 30f, y - 34f, new[] { "1", "3", "5" }, Array.IndexOf(RaceSetup.OpponentOptions, RaceSetup.opponents), 72f,
                i => RaceSetup.opponents = RaceSetup.OpponentOptions[i]);
            Header(right, "DIFFICULTY", y, 290f);
            ChoiceRow(right, 290f, y - 34f, new[] { "EASY", "MID", "HARD" }, RaceSetup.difficulty, 80f, i => RaceSetup.difficulty = i, 48f, 20);
            y -= 100f;
        }

        if (RaceSetup.mode == RaceSetup.Mode.QuickRace)
        {
            Header(right, "PLAYERS   (SPLIT SCREEN)", y);
            ChoiceRow(right, 30f, y - 34f, new[] { "1", "2" }, RaceSetup.players - 1, 72f, i =>
            {
                RaceSetup.players = i + 1;
                ShowScreen(RaceSetup.Screen.Setup);
            });
            if (RaceSetup.players == 2)
            {
                Image p2car = LowPolyUI.Button(right, "P2  " + LowPolyCarFactory.StyleNames[(int)RaceSetup.carStyle2], 20, null, out Text p2label);
                p2car.GetComponent<Button>().onClick.AddListener(() =>
                {
                    int s = (int)RaceSetup.carStyle2;
                    do { s = (s + 1) % LowPolyCarFactory.StyleNames.Length; } while (!Profile.IsUnlocked((LowPolyCarFactory.BodyStyle)s));
                    RaceSetup.carStyle2 = (LowPolyCarFactory.BodyStyle)s;
                    p2label.text = "P2  " + LowPolyCarFactory.StyleNames[s];
                });
                LowPolyUI.Place((RectTransform)p2car.transform, new Vector2(0f, 1f), new Vector2(200f, y - 34f), new Vector2(220f, 48f));
                Image p2color = LowPolyUI.Button(right, "", 10, null);
                p2color.color = CarColors[RaceSetup.carColor2];
                p2color.GetComponent<Button>().onClick.AddListener(() =>
                {
                    RaceSetup.carColor2 = (RaceSetup.carColor2 + 1) % 8;
                    p2color.color = CarColors[RaceSetup.carColor2];
                });
                LowPolyUI.Place((RectTransform)p2color.transform, new Vector2(0f, 1f), new Vector2(430f, y - 34f), new Vector2(48f, 48f));
                Text keys = LowPolyUI.Label(right, "P2 KEYS: ARROWS, RIGHT CTRL DRIFT, RIGHT SHIFT NITRO, ENTER RESET", 16, TextAnchor.UpperLeft, Dim, "P2Keys", true);
                LowPolyUI.Place(keys.rectTransform, new Vector2(0f, 1f), new Vector2(30f, y - 88f), new Vector2(540f, 22f));
            }
            y -= 120f;
        }

        Header(right, "TIME", y);
        int time = RaceSetup.dynamicTime ? 2 : RaceSetup.night ? 1 : 0;
        ChoiceRow(right, 30f, y - 34f, new[] { "DAY", "NIGHT", "DUSK" }, time, 160f, i =>
        {
            RaceSetup.night = i == 1;
            RaceSetup.dynamicTime = i == 2;
            ReloadPreview();
        });
        Text duskHint = LowPolyUI.Label(right, "DUSK  -  THE SUN SETS DURING THE RACE", 16, TextAnchor.UpperLeft, Dim, "DuskHint", true);
        LowPolyUI.Place(duskHint.rectTransform, new Vector2(0f, 1f), new Vector2(30f, y - 88f), new Vector2(540f, 22f));

        y -= 120f;
        Header(right, "WEATHER", y);
        int weatherChoice = RaceSetup.dynamicWeather ? 2 : RaceSetup.rain ? 1 : 0;
        ChoiceRow(right, 30f, y - 34f, new[] { "DRY", "RAIN", "CHANGING" }, weatherChoice, 160f, i =>
        {
            RaceSetup.rain = i == 1;
            RaceSetup.dynamicWeather = i == 2;
            ReloadPreview();
        });
        Text rainHint = LowPolyUI.Label(right, "CHANGING  -  RAIN STARTS MID RACE, THE ROAD GETS SLIPPERY", 16, TextAnchor.UpperLeft, Dim, "RainHint", true);
        LowPolyUI.Place(rainHint.rectTransform, new Vector2(0f, 1f), new Vector2(30f, y - 88f), new Vector2(540f, 22f));
    }

    private void UpdateStats()
    {
        if (statBars == null) return;
        LowPolyCarFactory.CarStats s = LowPolyCarFactory.Stats(RaceSetup.carStyle);
        float[] values = { s.Speed, s.Accel, s.Grip, s.Handling };
        for (int i = 0; i < statBars.Length; i++)
            statBars[i].anchorMax = new Vector2(Mathf.Clamp(values[i], 0.08f, 1f), 1f);
    }

    private void OnRacePressed()
    {
        if (RaceSetup.mode == RaceSetup.Mode.Championship)
        {
            Championship.Start();
            RaceSetup.trackIndex = 0;
        }
        RaceSetup.configured = true;
        if (previewCar != null) Destroy(previewCar);
        if (menuCamera != null) Destroy(menuCamera.gameObject);
        if (menuCanvas != null) Destroy(menuCanvas.gameObject);
        StartCoroutine(StartRaceNextFrame());
    }

    private IEnumerator StartRaceNextFrame()
    {
        // Počkat, než Unity skutečně zničí náhledové auto a menu
        yield return null;
        // Championship začíná první tratí - postavit ji, pokud je v menu jiná
        if (definition != TrackLibrary.Get(RaceSetup.trackIndex)) LoadTrack(RaceSetup.trackIndex);
        StartRace();
    }

    // ================================================================ editor tratí

    private void OpenEditor()
    {
        if (previewCar != null) Destroy(previewCar);
        if (menuCamera != null) Destroy(menuCamera.gameObject);
        if (world != null) world.gameObject.SetActive(false);
        RenderSettings.fog = false;

        int customIndex = RaceSetup.trackIndex - TrackLibrary.All.Length;
        CustomTracks.Track existing = customIndex >= 0 && customIndex < CustomTracks.All.Count ? CustomTracks.All[customIndex] : null;

        trackEditor = new GameObject("TrackEditor").AddComponent<TrackEditor>();
        trackEditor.material = c => Mat("editor" + c, c, 0.1f);
        trackEditor.onBack = CloseEditor;
        trackEditor.onTestDrive = index =>
        {
            RaceSetup.mode = RaceSetup.Mode.QuickRace;
            RaceSetup.players = 1;
            RaceSetup.trackIndex = TrackLibrary.All.Length + index;
            RaceSetup.configured = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        };
        trackEditor.Open(existing);
    }

    private void CloseEditor()
    {
        if (trackEditor != null) Destroy(trackEditor.gameObject);
        RaceSetup.trackIndex = Mathf.Clamp(RaceSetup.trackIndex, 0, TrackLibrary.Count - 1);
        if (world != null) world.gameObject.SetActive(true);
        RenderSettings.fog = true;
        LoadTrack(RaceSetup.trackIndex);
        ShowScreen(RaceSetup.Screen.Title);
    }

    // ================================================================ úspěchy

    private void BuildAchievementsScreen()
    {
        Transform root = menuCanvas.transform;
        Text title = LowPolyUI.Label(root, "ACHIEVEMENTS", 64, TextAnchor.UpperLeft, Color.white, "Title");
        LowPolyUI.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -40f), new Vector2(900f, 80f));
        Text count = LowPolyUI.Label(root, $"{Achievements.Count} OF {Achievements.All.Length} UNLOCKED", 24, TextAnchor.UpperLeft, LowPolyUI.Accent, "Count", true);
        LowPolyUI.Place(count.rectTransform, new Vector2(0f, 1f), new Vector2(52f, -112f), new Vector2(900f, 30f));

        for (int i = 0; i < Achievements.All.Length; i++)
        {
            Achievements.Definition a = Achievements.All[i];
            bool has = Achievements.Has(a.id);
            RectTransform card = LowPolyUI.Panel(root, a.id, has ? new Color(0.25f, 0.2f, 0.05f, 0.85f) : LowPolyUI.PanelColor);
            LowPolyUI.Place(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f + (i % 3) * 620f, -160f - (i / 3) * 124f), new Vector2(600f, 112f));
            Text n = LowPolyUI.Label(card, (has ? "" : "LOCKED   ") + a.name, 28, TextAnchor.UpperLeft, has ? LowPolyUI.Accent : Dim, "Name", true);
            LowPolyUI.Place(n.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -10f), new Vector2(570f, 36f));
            Text d = LowPolyUI.Label(card, a.description, 17, TextAnchor.UpperLeft, Color.white, "Description", true);
            LowPolyUI.Place(d.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -50f), new Vector2(570f, 24f));
            if (!string.IsNullOrEmpty(a.reward))
            {
                Text r = LowPolyUI.Label(card, "REWARD  " + a.reward, 17, TextAnchor.UpperLeft, LowPolyUI.Accent, "Reward", true);
                LowPolyUI.Place(r.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -78f), new Vector2(570f, 24f));
            }
        }

        Image back = LowPolyUI.Button(root, "< BACK", 28, () => ShowScreen(RaceSetup.Screen.Title));
        LowPolyUI.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(220f, 70f));
    }

    // ================================================================ nastavení

    private void BuildSettingsScreen()
    {
        Transform root = menuCanvas.transform;
        Text title = LowPolyUI.Label(root, "SETTINGS", 64, TextAnchor.UpperLeft, Color.white, "Title");
        LowPolyUI.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -40f), new Vector2(900f, 80f));

        // --- zvuk, grafika, jízda
        RectTransform left = LowPolyUI.Panel(root, "GamePanel", LowPolyUI.PanelColor);
        LowPolyUI.Place(left, new Vector2(0f, 1f), new Vector2(40f, -150f), new Vector2(660f, 640f));
        float y = -18f;
        y = ValueRow(left, "MASTER VOLUME", y, () => GameSettings.Master, v => GameSettings.Master = v);
        y = ValueRow(left, "MUSIC", y, () => GameSettings.Music, v => GameSettings.Music = v);
        y = ValueRow(left, "EFFECTS", y, () => GameSettings.Sfx, v => GameSettings.Sfx = v);

        Header(left, "GRAPHICS", y);
        string[] qualities = QualitySettings.names;
        Text quality = LowPolyUI.Label(left, qualities[GameSettings.Quality].ToUpper(), 22, TextAnchor.MiddleCenter, Color.white, "Quality");
        LowPolyUI.Place(quality.rectTransform, new Vector2(0f, 1f), new Vector2(350f, y - 4f), new Vector2(200f, 40f));
        Image qMinus = LowPolyUI.Button(left, "<", 26, () =>
        {
            GameSettings.Quality = Mathf.Max(0, GameSettings.Quality - 1);
            quality.text = qualities[GameSettings.Quality].ToUpper();
        });
        LowPolyUI.Place((RectTransform)qMinus.transform, new Vector2(0f, 1f), new Vector2(290f, y - 4f), new Vector2(50f, 40f));
        Image qPlus = LowPolyUI.Button(left, ">", 26, () =>
        {
            GameSettings.Quality = Mathf.Min(qualities.Length - 1, GameSettings.Quality + 1);
            quality.text = qualities[GameSettings.Quality].ToUpper();
        });
        LowPolyUI.Place((RectTransform)qPlus.transform, new Vector2(0f, 1f), new Vector2(560f, y - 4f), new Vector2(50f, 40f));
        y -= 62f;

        y = OptionRow(left, "POST EFFECTS", y, new[] { "ON", "OFF" }, GameSettings.PostFx ? 0 : 1, i => GameSettings.PostFx = i == 0);
        y = OptionRow(left, "GHOST", y, new[] { "ON", "OFF" }, GameSettings.ShowGhost ? 0 : 1, i => GameSettings.ShowGhost = i == 0);
        y = OptionRow(left, "DAMAGE", y, new[] { "OFF", "LOOKS", "FULL" }, GameSettings.Damage, i => GameSettings.Damage = i);
        y = OptionRow(left, "STEER ASSIST", y, new[] { "OFF", "LOW", "HIGH" }, GameSettings.SteerAssist, i => GameSettings.SteerAssist = i);
        OptionRow(left, "BRAKE ASSIST", y, new[] { "OFF", "ON" }, GameSettings.BrakeAssist ? 1 : 0, i => GameSettings.BrakeAssist = i == 1);

        // --- ovládání a přístupnost
        RectTransform right = LowPolyUI.Panel(root, "ControlsPanel", LowPolyUI.PanelColor);
        LowPolyUI.Place(right, new Vector2(1f, 1f), new Vector2(-40f, -150f), new Vector2(660f, 860f));
        Header(right, "CONTROLS   (CLICK TO CHANGE)", -18f);
        Text padHeader = LowPolyUI.Label(right, "PAD", 20, TextAnchor.UpperRight, Dim, "PadHeader");
        LowPolyUI.Place(padHeader.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(120f, 30f));
        float cy = -56f;
        foreach (GameInput.Action action in GameInput.AllActions)
        {
            GameInput.Action a = action;
            Text name = LowPolyUI.Label(right, action.ToString().ToUpper(), 20, TextAnchor.MiddleLeft, Color.white, "Action");
            LowPolyUI.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(30f, cy), new Vector2(220f, 40f));
            Text keyLabel = null;
            Image keyButton = LowPolyUI.Button(right, GameInput.GetKey(action).ToString().ToUpper(), 20, () =>
            {
                waitingForKey = a;
                waitingLabel = keyLabel;
                keyLabel.text = "PRESS A KEY";
            }, out keyLabel);
            LowPolyUI.Place((RectTransform)keyButton.transform, new Vector2(0f, 1f), new Vector2(260f, cy), new Vector2(240f, 40f));
            Text pad = LowPolyUI.Label(right, GameInput.PadLabel(action), 18, TextAnchor.MiddleRight, Dim, "Pad");
            LowPolyUI.Place(pad.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, cy), new Vector2(110f, 40f));
            cy -= 46f;
        }
        Image reset = LowPolyUI.Button(right, "RESET CONTROLS", 20, () =>
        {
            GameInput.ResetBindings();
            ShowScreen(RaceSetup.Screen.Settings);
        });
        LowPolyUI.Place((RectTransform)reset.transform, new Vector2(0f, 1f), new Vector2(30f, cy - 4f), new Vector2(300f, 44f));
        cy -= 70f;

        cy = OptionRow(right, "TOUCH CONTROLS", cy, new[] { "AUTO", "ON", "OFF" }, GameSettings.TouchControls, i => GameSettings.TouchControls = i);
        cy = OptionRow(right, "TILT STEERING", cy, new[] { "OFF", "ON" }, GameSettings.Tilt ? 1 : 0, i => GameSettings.Tilt = i == 1);
        cy = OptionRow(right, "UI SIZE", cy, new[] { "S", "M", "L" }, GameSettings.UiScale, i => { GameSettings.UiScale = i; ShowScreen(RaceSetup.Screen.Settings); });
        cy = OptionRow(right, "HIGH CONTRAST", cy, new[] { "OFF", "ON" }, GameSettings.HighContrast ? 1 : 0, i => { GameSettings.HighContrast = i == 1; ShowScreen(RaceSetup.Screen.Settings); });
        Image tutorial = LowPolyUI.Button(right, GameSettings.TutorialDone ? "SHOW TUTORIAL AGAIN" : "TUTORIAL WILL SHOW", 20, () => GameSettings.TutorialDone = false);
        LowPolyUI.Place((RectTransform)tutorial.transform, new Vector2(0f, 1f), new Vector2(30f, cy - 4f), new Vector2(360f, 44f));

        Image back = LowPolyUI.Button(root, "< BACK", 28, () => ShowScreen(RaceSetup.Screen.Title));
        LowPolyUI.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(220f, 70f));
    }

    private float OptionRow(RectTransform parent, string label, float y, string[] options, int selected, Action<int> onSelect)
    {
        Header(parent, label, y);
        float width = options.Length == 2 ? 150f : 98f;
        ChoiceRow(parent, 290f, y + 2f, options, selected, width, onSelect, 42f, 18);
        return y - 58f;
    }

    private float ValueRow(RectTransform parent, string label, float y, Func<float> get, Action<float> set)
    {
        Header(parent, label, y);
        Text value = LowPolyUI.Label(parent, Mathf.RoundToInt(get() * 100f) + "", 24, TextAnchor.MiddleCenter, Color.white, label + "Value", true);
        LowPolyUI.Place(value.rectTransform, new Vector2(0f, 1f), new Vector2(350f, y - 4f), new Vector2(200f, 40f));
        Image minus = LowPolyUI.Button(parent, "<", 26, () =>
        {
            set(Mathf.Clamp01(get() - 0.1f));
            value.text = Mathf.RoundToInt(get() * 100f) + "";
        });
        LowPolyUI.Place((RectTransform)minus.transform, new Vector2(0f, 1f), new Vector2(290f, y - 4f), new Vector2(50f, 40f));
        Image plus = LowPolyUI.Button(parent, ">", 26, () =>
        {
            set(Mathf.Clamp01(get() + 0.1f));
            value.text = Mathf.RoundToInt(get() * 100f) + "";
        });
        LowPolyUI.Place((RectTransform)plus.transform, new Vector2(0f, 1f), new Vector2(560f, y - 4f), new Vector2(50f, 40f));
        return y - 62f;
    }
}
