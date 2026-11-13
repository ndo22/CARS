using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Obrazovky menu: titulní, výběr závodu, nastavení.</summary>
public partial class LowPolyRaceBootstrap
{
    private Canvas menuCanvas;
    private GameObject previewCar;
    private Camera menuCamera;
    private LowPolyUI.ChoiceGroup trackChoice;
    private Text statusText;
    private RectTransform[] statBars;
    private GameInput.Action? waitingForKey;
    private Text waitingLabel;
    private static KeyCode[] allKeys;

    private static readonly Color Dim = new Color(1f, 1f, 1f, 0.7f);

    // ================================================================ společné

    private void ShowScreen(RaceSetup.Screen screen)
    {
        RaceSetup.screen = screen;
        if (menuCanvas != null) Destroy(menuCanvas.gameObject);
        waitingForKey = null;
        menuCanvas = LowPolyUI.CreateCanvas("MenuCanvas");

        if (menuCamera == null)
        {
            var camGo = new GameObject("MenuCamera");
            camGo.tag = "MainCamera";
            menuCamera = SetupCamera(camGo);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<OrbitCamera>();
        }
        if (previewCar == null) SpawnPreviewCar();
        PlayMusic(ProceduralAudio.MenuMusic());

        switch (screen)
        {
            case RaceSetup.Screen.Setup: BuildSetupScreen(); break;
            case RaceSetup.Screen.Settings: BuildSettingsScreen(); break;
            default: BuildTitleScreen(); break;
        }
    }

    private Text Title(Transform root, int size, float y)
    {
        Text title = LowPolyUI.Label(root, "LOW POLY", size, TextAnchor.UpperLeft, Color.white, "Title");
        LowPolyUI.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(48f, y), new Vector2(900f, size + 14f));
        Text subtitle = LowPolyUI.Label(root, "RACING", Mathf.RoundToInt(size * 0.46f), TextAnchor.UpperLeft, LowPolyUI.Accent, "Subtitle");
        LowPolyUI.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(54f, y - size - 4f), new Vector2(800f, 60f));
        return title;
    }

    private void Header(RectTransform parent, string text, float y, float x = 30f)
    {
        Text header = LowPolyUI.Label(parent, text, 24, TextAnchor.UpperLeft, Dim, text + "Header");
        LowPolyUI.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(x, y - 6f), new Vector2(400f, 30f));
    }

    private LowPolyUI.ChoiceGroup ChoiceRow(RectTransform parent, float x, float y, string[] labels, int selected, float width, Action<int> onSelect, float height = 54f, int fontSize = 24)
    {
        var group = new LowPolyUI.ChoiceGroup();
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            Image b = LowPolyUI.Button(parent, labels[i], fontSize, () =>
            {
                group.Select(index);
                onSelect(index);
            }, out Text label);
            LowPolyUI.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x + i * (width + 10f), y), new Vector2(width, height));
            group.Add(b, label, LowPolyUI.ButtonColor);
        }
        group.Select(Mathf.Max(0, selected));
        return group;
    }

    private void SpawnPreviewCar()
    {
        if (previewCar != null) Destroy(previewCar);
        GridSlot(0, out Vector3 pos, out Quaternion rot);
        int layer = Mathf.Max(0, LayerMask.NameToLayer("Car2"));
        previewCar = LowPolyCarFactory.Build("PreviewCar", pos, rot, RaceSetup.carStyle,
            CarPalette(CarColors[RaceSetup.colorIndex], RaceSetup.colorIndex == 9), layer, out Car car,
            new LowPolyCarFactory.Options { number = 1, headlights = theme.night });
        car.InputEnabled = false;
        previewCar.SetActive(true);
        if (menuCamera != null) menuCamera.GetComponent<OrbitCamera>().target = previewCar.transform;
    }

    private void ReloadPreview()
    {
        LoadTrack(RaceSetup.trackIndex);
        if (menuCamera != null) ConfigureCameraSky(menuCamera);
        if (weather != null && menuCamera != null) weather.followCamera = menuCamera.transform;
        SpawnPreviewCar();
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
        Title(root, 120, -60f);

        string[] labels = { "QUICK RACE", "CHAMPIONSHIP", "TIME TRIAL", "CLASSIC TRACK", "SETTINGS", "QUIT" };
        Action[] actions =
        {
            () => OpenSetup(RaceSetup.Mode.QuickRace),
            () => OpenSetup(RaceSetup.Mode.Championship),
            () => OpenSetup(RaceSetup.Mode.TimeTrial),
            () => SceneManager.LoadScene("SampleScene"),
            () => ShowScreen(RaceSetup.Screen.Settings),
            () => { Debug.Log("QUIT"); Application.Quit(); },
        };
        for (int i = 0; i < labels.Length; i++)
        {
            Image b = LowPolyUI.Button(root, labels[i], 34, actions[i], out Text label);
            if (i == 0)
            {
                b.color = LowPolyUI.Accent;
                label.color = new Color(0.1f, 0.08f, 0.05f);
            }
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.offsetMin = new Vector2(28f, 0f);
            LowPolyUI.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(54f, -300f - i * 86f), new Vector2(440f, 72f));
        }

        Text profile = LowPolyUI.Label(root, $"WINS {Profile.Wins}   |   CHAMPIONSHIPS {Profile.Championships}", 22,
            TextAnchor.LowerLeft, Dim, "Profile", true);
        LowPolyUI.Place(profile.rectTransform, new Vector2(0f, 0f), new Vector2(56f, 40f), new Vector2(800f, 30f));
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

        // --- tratě (vlevo)
        RectTransform left = LowPolyUI.Panel(root, "TrackPanel", LowPolyUI.PanelColor);
        LowPolyUI.Place(left, new Vector2(0f, 1f), new Vector2(40f, -170f), new Vector2(600f, 700f));
        Header(left, championship ? "ROUNDS" : "TRACK", -16f);
        trackChoice = new LowPolyUI.ChoiceGroup();
        for (int i = 0; i < TrackLibrary.All.Length; i++)
        {
            int index = i;
            TrackLibrary.TrackDefinition def = TrackLibrary.All[i];
            Image card = LowPolyUI.Button(left, "", 10, () =>
            {
                if (championship) return;
                SelectTrack(index);
            });
            LowPolyUI.Place((RectTransform)card.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -62f - i * 126f), new Vector2(560f, 116f));

            string prefix = championship ? $"{i + 1}. " : "";
            Text name = LowPolyUI.Label(card.transform, prefix + def.name, 32, TextAnchor.UpperLeft, Color.white, "Name", championship);
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
            trackChoice.Add(card, null, LowPolyUI.ButtonColor);
        }
        trackChoice.Select(RaceSetup.trackIndex);

        // --- auto a podmínky (vpravo)
        RectTransform right = LowPolyUI.Panel(root, "CarPanel", LowPolyUI.PanelColor);
        LowPolyUI.Place(right, new Vector2(1f, 1f), new Vector2(-40f, -170f), new Vector2(580f, 700f));

        float y = -16f;
        Header(right, "CAR", y);
        var styles = new LowPolyUI.ChoiceGroup();
        for (int i = 0; i < LowPolyCarFactory.StyleNames.Length; i++)
        {
            int index = i;
            var style = (LowPolyCarFactory.BodyStyle)i;
            bool unlocked = Profile.IsUnlocked(style);
            Image b = LowPolyUI.Button(right, unlocked ? LowPolyCarFactory.StyleNames[i] : "LOCKED", 22, () =>
            {
                if (!Profile.IsUnlocked(style))
                {
                    statusText.text = $"{LowPolyCarFactory.StyleNames[index]}  -  {Profile.UnlockHint(style)} TO UNLOCK";
                    return;
                }
                RaceSetup.carStyle = style;
                styles.Select(index);
                statusText.text = "";
                UpdateStats();
                SpawnPreviewCar();
            }, out Text label);
            if (!unlocked) label.color = new Color(1f, 1f, 1f, 0.4f);
            LowPolyUI.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(30f + (i % 3) * 178f, y - 46f - (i / 3) * 58f), new Vector2(168f, 50f));
            styles.Add(b, label, LowPolyUI.ButtonColor);
        }
        styles.Select((int)RaceSetup.carStyle);

        // Vlastnosti auta
        y -= 166f;
        string[] statNames = { "SPEED", "ACCEL", "GRIP", "HANDLING" };
        statBars = new RectTransform[statNames.Length];
        for (int i = 0; i < statNames.Length; i++)
        {
            Text l = LowPolyUI.Label(right, statNames[i], 18, TextAnchor.MiddleLeft, Dim, statNames[i]);
            LowPolyUI.Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(30f + (i % 2) * 262f, y - (i / 2) * 30f), new Vector2(120f, 24f));
            RectTransform bg = LowPolyUI.Panel(right, statNames[i] + "Bar", new Color(1f, 1f, 1f, 0.12f));
            LowPolyUI.Place(bg, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(140f + (i % 2) * 262f, y - 4f - (i / 2) * 30f), new Vector2(110f, 16f));
            RectTransform fill = LowPolyUI.Panel(bg, "Fill", LowPolyUI.Accent);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.5f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            statBars[i] = fill;
        }
        UpdateStats();

        y -= 76f;
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
            LowPolyUI.Place((RectTransform)swatch.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f + i * 52f, y - 40f), new Vector2(44f, 44f));
            colors.Add(swatch, null, unlocked ? CarColors[i] : CarColors[i] * 0.35f);
        }
        colors.Select(RaceSetup.colorIndex);

        y -= 104f;
        if (!timeTrial)
        {
            Header(right, "OPPONENTS", y);
            ChoiceRow(right, 30f, y - 36f, new[] { "1", "3", "5" }, Array.IndexOf(RaceSetup.OpponentOptions, RaceSetup.opponents), 72f,
                i => RaceSetup.opponents = RaceSetup.OpponentOptions[i], 48f);
            Header(right, "DIFFICULTY", y, 290f);
            ChoiceRow(right, 290f, y - 36f, new[] { "EASY", "MID", "HARD" }, RaceSetup.difficulty, 80f, i => RaceSetup.difficulty = i, 48f, 20);
            y -= 100f;
        }

        Header(right, "TIME", y);
        ChoiceRow(right, 30f, y - 36f, new[] { "DAY", "NIGHT" }, RaceSetup.night ? 1 : 0, 112f, i =>
        {
            RaceSetup.night = i == 1;
            ReloadPreview();
        }, 48f, 22);
        Header(right, "WEATHER", y, 290f);
        ChoiceRow(right, 290f, y - 36f, new[] { "DRY", "RAIN" }, RaceSetup.rain ? 1 : 0, 125f, i =>
        {
            RaceSetup.rain = i == 1;
            ReloadPreview();
        }, 48f, 22);

        statusText = LowPolyUI.Label(right, "", 18, TextAnchor.LowerCenter, LowPolyUI.Accent, "Status", true);
        LowPolyUI.Place(statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(540f, 26f));

        // --- start
        RectTransform bottom = LowPolyUI.Panel(root, "StartPanel", LowPolyUI.PanelColor);
        LowPolyUI.Place(bottom, new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(580f, 150f));
        Header(bottom, "LAPS", -14f);
        ChoiceRow(bottom, 30f, -50f, new[] { "1", "3", "5" }, Array.IndexOf(RaceSetup.LapOptions, RaceSetup.laps), 70f,
            i => RaceSetup.laps = RaceSetup.LapOptions[i], 62f, 26);

        Image race = LowPolyUI.Button(bottom, championship ? "START!" : "RACE!", 46, OnRacePressed, out Text raceLabel);
        race.color = LowPolyUI.Accent;
        raceLabel.color = new Color(0.1f, 0.08f, 0.05f);
        LowPolyUI.Place((RectTransform)race.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(270f, 104f));

        Image back = LowPolyUI.Button(root, "< BACK", 28, () => ShowScreen(RaceSetup.Screen.Title));
        LowPolyUI.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(220f, 70f));
    }

    private void UpdateStats()
    {
        if (statBars == null) return;
        LowPolyCarFactory.CarStats s = LowPolyCarFactory.Stats(RaceSetup.carStyle);
        float[] values = { s.Speed, s.Accel, s.Grip, s.Handling };
        for (int i = 0; i < statBars.Length; i++)
            statBars[i].anchorMax = new Vector2(Mathf.Clamp(values[i], 0.08f, 1f), 1f);
    }

    private void SelectTrack(int index)
    {
        if (index == RaceSetup.trackIndex) return;
        RaceSetup.trackIndex = index;
        trackChoice.Select(index);
        ReloadPreview();
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
        StartRace();
    }

    // ================================================================ nastavení

    private void BuildSettingsScreen()
    {
        Transform root = menuCanvas.transform;
        Text title = LowPolyUI.Label(root, "SETTINGS", 64, TextAnchor.UpperLeft, Color.white, "Title");
        LowPolyUI.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -40f), new Vector2(900f, 80f));

        // --- zvuk a grafika
        RectTransform left = LowPolyUI.Panel(root, "AudioPanel", LowPolyUI.PanelColor);
        LowPolyUI.Place(left, new Vector2(0f, 1f), new Vector2(40f, -170f), new Vector2(620f, 560f));
        float y = -20f;
        y = ValueRow(left, "MASTER VOLUME", y, () => GameSettings.Master, v => GameSettings.Master = v);
        y = ValueRow(left, "MUSIC", y, () => GameSettings.Music, v => GameSettings.Music = v);
        y = ValueRow(left, "EFFECTS", y, () => GameSettings.Sfx, v => GameSettings.Sfx = v);

        Header(left, "GRAPHICS", y);
        string[] qualities = QualitySettings.names;
        Text quality = LowPolyUI.Label(left, qualities[GameSettings.Quality].ToUpper(), 24, TextAnchor.MiddleCenter, Color.white, "Quality");
        LowPolyUI.Place(quality.rectTransform, new Vector2(0f, 1f), new Vector2(330f, y - 6f), new Vector2(180f, 40f));
        Image qMinus = LowPolyUI.Button(left, "<", 28, () =>
        {
            GameSettings.Quality = Mathf.Max(0, GameSettings.Quality - 1);
            quality.text = qualities[GameSettings.Quality].ToUpper();
        });
        LowPolyUI.Place((RectTransform)qMinus.transform, new Vector2(0f, 1f), new Vector2(270f, y - 6f), new Vector2(50f, 40f));
        Image qPlus = LowPolyUI.Button(left, ">", 28, () =>
        {
            GameSettings.Quality = Mathf.Min(qualities.Length - 1, GameSettings.Quality + 1);
            quality.text = qualities[GameSettings.Quality].ToUpper();
        });
        LowPolyUI.Place((RectTransform)qPlus.transform, new Vector2(0f, 1f), new Vector2(520f, y - 6f), new Vector2(50f, 40f));
        y -= 70f;

        Header(left, "POST EFFECTS", y);
        ChoiceRow(left, 270f, y, new[] { "ON", "OFF" }, GameSettings.PostFx ? 0 : 1, 140f, i => GameSettings.PostFx = i == 0, 42f, 22);
        y -= 70f;
        Header(left, "GHOST IN TIME TRIAL", y);
        ChoiceRow(left, 270f, y, new[] { "ON", "OFF" }, GameSettings.ShowGhost ? 0 : 1, 140f, i => GameSettings.ShowGhost = i == 0, 42f, 22);

        // --- ovládání
        RectTransform right = LowPolyUI.Panel(root, "ControlsPanel", LowPolyUI.PanelColor);
        LowPolyUI.Place(right, new Vector2(1f, 1f), new Vector2(-40f, -170f), new Vector2(620f, 700f));
        Header(right, "CONTROLS   (CLICK TO CHANGE)", -20f);
        Text padHeader = LowPolyUI.Label(right, "PAD", 22, TextAnchor.UpperRight, Dim, "PadHeader");
        LowPolyUI.Place(padHeader.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -26f), new Vector2(120f, 30f));
        float cy = -66f;
        foreach (GameInput.Action action in GameInput.AllActions)
        {
            GameInput.Action a = action;
            Text name = LowPolyUI.Label(right, action.ToString().ToUpper(), 22, TextAnchor.MiddleLeft, Color.white, "Action");
            LowPolyUI.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(30f, cy), new Vector2(220f, 46f));
            Text keyLabel = null;
            Image keyButton = LowPolyUI.Button(right, GameInput.GetKey(action).ToString().ToUpper(), 22, () =>
            {
                waitingForKey = a;
                waitingLabel = keyLabel;
                keyLabel.text = "PRESS A KEY";
            }, out keyLabel);
            LowPolyUI.Place((RectTransform)keyButton.transform, new Vector2(0f, 1f), new Vector2(250f, cy), new Vector2(220f, 46f));
            Text pad = LowPolyUI.Label(right, GameInput.PadLabel(action), 20, TextAnchor.MiddleRight, Dim, "Pad");
            LowPolyUI.Place(pad.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, cy), new Vector2(110f, 46f));
            cy -= 54f;
        }
        Image reset = LowPolyUI.Button(right, "RESET CONTROLS", 22, () =>
        {
            GameInput.ResetBindings();
            ShowScreen(RaceSetup.Screen.Settings);
        });
        LowPolyUI.Place((RectTransform)reset.transform, new Vector2(0f, 0f), new Vector2(30f, 24f), new Vector2(300f, 50f));

        Image back = LowPolyUI.Button(root, "< BACK", 28, () => ShowScreen(RaceSetup.Screen.Title));
        LowPolyUI.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(220f, 70f));
    }

    private float ValueRow(RectTransform parent, string label, float y, Func<float> get, Action<float> set)
    {
        Header(parent, label, y);
        Text value = LowPolyUI.Label(parent, Mathf.RoundToInt(get() * 100f) + "", 26, TextAnchor.MiddleCenter, Color.white, label + "Value", true);
        LowPolyUI.Place(value.rectTransform, new Vector2(0f, 1f), new Vector2(330f, y - 6f), new Vector2(180f, 40f));
        Image minus = LowPolyUI.Button(parent, "<", 28, () =>
        {
            set(Mathf.Clamp01(get() - 0.1f));
            value.text = Mathf.RoundToInt(get() * 100f) + "";
        });
        LowPolyUI.Place((RectTransform)minus.transform, new Vector2(0f, 1f), new Vector2(270f, y - 6f), new Vector2(50f, 40f));
        Image plus = LowPolyUI.Button(parent, ">", 28, () =>
        {
            set(Mathf.Clamp01(get() + 0.1f));
            value.text = Mathf.RoundToInt(get() * 100f) + "";
        });
        LowPolyUI.Place((RectTransform)plus.transform, new Vector2(0f, 1f), new Vector2(520f, y - 6f), new Vector2(50f, 40f));
        return y - 70f;
    }
}
