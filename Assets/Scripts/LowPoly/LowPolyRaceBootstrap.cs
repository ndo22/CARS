using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Řídí celou low-poly scénu:
/// 1) startovní obrazovka (výběr tratě, auta, barvy, soupeřů, obtížnosti, počtu kol) s živým 3D náhledem,
/// 2) závod (auta, kamery, HUD, minimapa, pauza),
/// 3) výsledky po dojetí posledního kola.
/// Ve scéně stačí jeden objekt s tímto skriptem.
/// </summary>
public class LowPolyRaceBootstrap : MonoBehaviour
{
    [Header("Assety (volitelné)")]
    [Tooltip("Materiál, ze kterého se kopírují všechny barvy (Standard shader).")]
    public Material baseMaterial;
    public Font uiFont;
    [Tooltip("Písmo pro časy a čísla (musí mít znaky ' \" . ,).")]
    public Font numberFont;
    public TMP_FontAsset tmpFont;
    public AudioClip engineClip;

    public static readonly Color[] CarColors =
    {
        new Color(0.92f, 0.18f, 0.12f), new Color(0.12f, 0.45f, 0.95f), new Color(0.98f, 0.76f, 0.12f),
        new Color(0.2f, 0.72f, 0.3f), new Color(0.98f, 0.48f, 0.1f), new Color(0.55f, 0.25f, 0.85f),
        new Color(0.94f, 0.94f, 0.94f), new Color(0.1f, 0.74f, 0.74f),
    };

    private static readonly string[] StyleNames = { "SPORT", "MUSCLE", "HATCH" };

    // Trať
    private TrackLibrary.TrackDefinition definition;
    private TrackTheme theme;
    private TrackGenerator.TrackSettings settings;
    private TrackGenerator.TrackResult track;
    private Transform world;
    private Light sun;
    private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

    // Menu
    private Canvas menuCanvas;
    private GameObject previewCar;
    private Camera menuCamera;
    private readonly LowPolyUI.ChoiceGroup trackChoice = new LowPolyUI.ChoiceGroup();

    // Závod
    private Player player;
    private readonly List<AIPlayer> opponents = new List<AIPlayer>();
    private UIController hud;
    private PauseMenu pauseMenu;
    private Canvas raceCanvas;
    private float playerRaceTime;
    private int opponentsFinished;
    private readonly HashSet<AIPlayer> finishedOpponents = new HashSet<AIPlayer>();
    private bool playerFinished;
    private float recordBefore;

    private void Awake()
    {
        LowPolyUI.font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
        LowPolyUI.numberFont = numberFont != null ? numberFont : LowPolyUI.font;
        RaceSetup.trackIndex = Mathf.Clamp(RaceSetup.trackIndex, 0, TrackLibrary.All.Length - 1);
        LoadTrack(RaceSetup.trackIndex);

        if (RaceSetup.configured)
            StartRace();
        else
            ShowMenu();
    }

    // ================================================================ trať a prostředí

    private void LoadTrack(int index)
    {
        if (world != null)
        {
            // Přejmenovat, ať nové auto nenajde staré checkpointy (Destroy proběhne až na konci snímku)
            Transform old = world.Find("Checkpoints");
            if (old != null) old.name = "Checkpoints_old";
            Destroy(world.gameObject);
        }

        definition = TrackLibrary.All[index];
        theme = definition.theme();
        settings = TrackLibrary.Settings(definition);
        materials.Clear();
        SetupEnvironment();

        world = new GameObject("World").transform;
        track = TrackGenerator.Build(world, settings, theme, new Palette(this));
    }

    private void SetupEnvironment()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = theme.ambientSky;
        RenderSettings.ambientEquatorColor = theme.ambientEquator;
        RenderSettings.ambientGroundColor = theme.ambientGround;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = theme.sky;
        RenderSettings.fogStartDistance = theme.fogStart;
        RenderSettings.fogEndDistance = theme.fogEnd;
        QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, 150f);

        if (sun == null)
        {
            sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            RenderSettings.sun = sun;
        }
        sun.color = theme.sun;
        sun.intensity = theme.sunIntensity;
        sun.transform.rotation = Quaternion.Euler(theme.sunEuler);

        foreach (var cam in FindObjectsOfType<Camera>())
            cam.backgroundColor = theme.sky;
    }

    // ================================================================ materiály

    private Material Mat(string cacheKey, Color color, float smoothness = 0.12f, Color? emission = null)
    {
        if (materials.TryGetValue(cacheKey, out Material cached))
            return cached;

        Material m = baseMaterial != null ? new Material(baseMaterial) : new Material(Shader.Find("Standard"));
        m.mainTexture = null;
        m.color = color;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (emission != null)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
        }
        materials[cacheKey] = m;
        return m;
    }

    private class Palette : TrackGenerator.IPalette
    {
        private readonly LowPolyRaceBootstrap owner;
        public Palette(LowPolyRaceBootstrap owner) { this.owner = owner; }

        public Material Get(string key)
        {
            TrackTheme t = owner.theme;
            switch (key)
            {
                case "startLight": return owner.Mat(key, new Color(0.6f, 0.05f, 0.05f), 0.6f, new Color(1f, 0.15f, 0.1f));
                case "lamp": return owner.Mat(key, new Color(1f, 0.95f, 0.7f), 0.5f, new Color(1f, 0.9f, 0.5f));
                case "water": return owner.Mat(key, t["water"], 0.85f);
                case "waterB": return owner.Mat(key, Color.Lerp(t["water"], Color.white, 0.12f), 0.85f);
                case "metal": return owner.Mat(key, t["metal"], 0.45f);
                case "snow":
                case "peak": return owner.Mat(key, t[key], 0.2f);
                default: return owner.Mat(key, t[key], key.StartsWith("asphalt") || key.StartsWith("ground") ? 0.04f : 0.12f);
            }
        }
    }

    private LowPolyCarFactory.Palette CarPalette(Color paint)
    {
        return new LowPolyCarFactory.Palette
        {
            paint = Mat("paint" + paint, paint, 0.6f),
            glass = Mat("glass", new Color(0.12f, 0.16f, 0.22f), 0.85f),
            dark = Mat("carDark", new Color(0.12f, 0.12f, 0.13f)),
            headlight = Mat("headlight", new Color(1f, 0.97f, 0.85f), 0.5f, new Color(0.9f, 0.85f, 0.6f)),
            taillight = Mat("taillight", new Color(0.8f, 0.05f, 0.05f), 0.5f, new Color(0.6f, 0f, 0f)),
            trim = Mat("trim", new Color(0.92f, 0.92f, 0.92f), 0.4f),
            tire = Mat("carTire", new Color(0.1f, 0.1f, 0.1f), 0.05f),
            hub = Mat("hub", new Color(0.72f, 0.74f, 0.78f), 0.55f),
        };
    }

    // ================================================================ startovní rošt

    /// <summary>Pozice na roštu: 0 = první řada vlevo. Odpovídá bílým značkám, které kreslí TrackGenerator.</summary>
    private void GridSlot(int slot, out Vector3 position, out Quaternion rotation)
    {
        int row = slot / 2;
        int index = -3 - row * 2;
        float side = slot % 2 == 0 ? -3f : 3f;
        position = track.Center(index) + track.Right(index) * side + track.Forward(-3) * ((slot % 2) * -3f) + Vector3.up * 0.6f;
        rotation = Quaternion.LookRotation(track.Forward(index), Vector3.up);
    }

    // ================================================================ MENU

    private void ShowMenu()
    {
        menuCanvas = LowPolyUI.CreateCanvas("MenuCanvas");
        Transform root = menuCanvas.transform;

        // Menu kamera kroužící kolem auta
        var camGo = new GameObject("MenuCamera");
        camGo.tag = "MainCamera";
        menuCamera = SetupCamera(camGo);
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<OrbitCamera>();
        SpawnPreviewCar();

        // --- Titulek
        Text title = LowPolyUI.Label(root, "LOW POLY", 96, TextAnchor.UpperLeft, Color.white, "Title");
        LowPolyUI.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -30f), new Vector2(800f, 110f));
        Text subtitle = LowPolyUI.Label(root, "RACING", 44, TextAnchor.UpperLeft, LowPolyUI.Accent, "Subtitle");
        LowPolyUI.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(54f, -130f), new Vector2(800f, 60f));

        // --- Výběr tratě (vlevo)
        RectTransform left = LowPolyUI.Panel(root, "TrackPanel", LowPolyUI.PanelColor);
        LowPolyUI.Place(left, new Vector2(0f, 1f), new Vector2(40f, -210f), new Vector2(600f, 650f));
        Header(left, "TRACK", -18f);
        for (int i = 0; i < TrackLibrary.All.Length; i++)
        {
            int index = i;
            TrackLibrary.TrackDefinition def = TrackLibrary.All[i];
            Image card = LowPolyUI.Button(left, "", 10, () => SelectTrack(index), out Text cardLabel);
            LowPolyUI.Place((RectTransform)card.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f - i * 140f), new Vector2(560f, 128f));

            Text name = LowPolyUI.Label(card.transform, def.name, 36, TextAnchor.UpperLeft, Color.white, "Name");
            LowPolyUI.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -14f), new Vector2(520f, 44f));
            Text desc = LowPolyUI.Label(card.transform, def.description.ToUpper(), 19, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.75f), "Description", true);
            LowPolyUI.Place(desc.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -58f), new Vector2(520f, 26f));

            float length = TrackGenerator.Length(TrackLibrary.Settings(def));
            float best = Leaderboard.BestTime(def.LeaderboardKey);
            string info = $"{(length / 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} KM   |   BEST {TimeFormat.FormatRacing(best)}";
            Text infoText = LowPolyUI.Label(card.transform, info, 22, TextAnchor.UpperLeft, LowPolyUI.Accent, "Info", true);
            LowPolyUI.Place(infoText.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -90f), new Vector2(520f, 26f));

            // Barevný proužek podle tématu
            RectTransform stripe = LowPolyUI.Panel(card.transform, "Stripe", def.theme()["groundA"]);
            LowPolyUI.Place(stripe, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(16f, 96f));

            trackChoice.Add(card, null, LowPolyUI.ButtonColor);
        }
        trackChoice.Select(RaceSetup.trackIndex);

        // --- Nastavení závodu (vpravo)
        RectTransform right = LowPolyUI.Panel(root, "CarPanel", LowPolyUI.PanelColor);
        LowPolyUI.Place(right, new Vector2(1f, 1f), new Vector2(-40f, -210f), new Vector2(560f, 600f));

        float y = -18f;
        Header(right, "CAR", y);
        ChoiceRow(right, y - 60f, StyleNames, (int)RaceSetup.carStyle, 160f, i =>
        {
            RaceSetup.carStyle = (LowPolyCarFactory.BodyStyle)i;
            SpawnPreviewCar();
        });

        y -= 140f;
        Header(right, "COLOR", y);
        var colors = new LowPolyUI.ChoiceGroup();
        for (int i = 0; i < CarColors.Length; i++)
        {
            int index = i;
            Image swatch = LowPolyUI.Button(right, "", 10, () =>
            {
                RaceSetup.colorIndex = index;
                colors.Select(index);
                SpawnPreviewCar();
            });
            LowPolyUI.Place((RectTransform)swatch.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f + i * 63f, y - 62f), new Vector2(52f, 52f));
            colors.Add(swatch, null, CarColors[i]);
        }
        colors.Select(RaceSetup.colorIndex);

        y -= 140f;
        Header(right, "OPPONENTS", y);
        ChoiceRow(right, y - 60f, new[] { "1", "3", "5" }, System.Array.IndexOf(RaceSetup.OpponentOptions, RaceSetup.opponents), 160f,
            i => RaceSetup.opponents = RaceSetup.OpponentOptions[i]);

        y -= 140f;
        Header(right, "DIFFICULTY", y);
        ChoiceRow(right, y - 60f, RaceSetup.DifficultyNames, RaceSetup.difficulty, 160f, i => RaceSetup.difficulty = i);

        // --- Počet kol a start
        RectTransform bottom = LowPolyUI.Panel(root, "StartPanel", LowPolyUI.PanelColor);
        LowPolyUI.Place(bottom, new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(560f, 150f));
        Text lapsHeader = LowPolyUI.Label(bottom, "LAPS", 26, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.7f), "LapsHeader");
        LowPolyUI.Place(lapsHeader.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -14f), new Vector2(200f, 30f));
        ChoiceRow(bottom, -54f, new[] { "1", "3", "5" }, System.Array.IndexOf(RaceSetup.LapOptions, RaceSetup.laps), 70f,
            i => RaceSetup.laps = RaceSetup.LapOptions[i]);

        Image race = LowPolyUI.Button(bottom, "RACE!", 46, OnRacePressed, out Text raceLabel);
        race.color = LowPolyUI.Accent;
        raceLabel.color = new Color(0.1f, 0.08f, 0.05f);
        LowPolyUI.Place((RectTransform)race.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(270f, 104f));

        Image back = LowPolyUI.Button(root, "< BACK", 28, () => SceneManager.LoadScene(0));
        LowPolyUI.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(220f, 70f));

        Text hint = LowPolyUI.Label(root, "WASD DRIVE   SPACE HANDBRAKE   R RESET   C CAMERA   ESC PAUSE", 18,
            TextAnchor.LowerCenter, new Color(1f, 1f, 1f, 0.8f), "Controls");
        LowPolyUI.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-160f, 50f), new Vector2(860f, 30f));
    }

    private void Header(RectTransform parent, string text, float y)
    {
        Text header = LowPolyUI.Label(parent, text, 28, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.7f), text + "Header");
        LowPolyUI.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(30f, y - 6f), new Vector2(400f, 34f));
    }

    private void ChoiceRow(RectTransform parent, float y, string[] labels, int selected, float width, System.Action<int> onSelect)
    {
        var group = new LowPolyUI.ChoiceGroup();
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            Image b = LowPolyUI.Button(parent, labels[i], 26, () =>
            {
                group.Select(index);
                onSelect(index);
            }, out Text label);
            LowPolyUI.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f + i * (width + 12f), y), new Vector2(width, 62f));
            group.Add(b, label, LowPolyUI.ButtonColor);
        }
        group.Select(Mathf.Max(0, selected));
    }

    private void SelectTrack(int index)
    {
        if (index == RaceSetup.trackIndex) return;
        RaceSetup.trackIndex = index;
        trackChoice.Select(index);
        LoadTrack(index);
        SpawnPreviewCar();
    }

    private void SpawnPreviewCar()
    {
        if (previewCar != null) Destroy(previewCar);
        GridSlot(0, out Vector3 pos, out Quaternion rot);
        int layer = Mathf.Max(0, LayerMask.NameToLayer("Car"));
        previewCar = LowPolyCarFactory.Build("PreviewCar", pos, rot, RaceSetup.carStyle, CarPalette(CarColors[RaceSetup.colorIndex]), layer, out Car car);
        car.InputEnabled = false;
        previewCar.SetActive(true);
        if (menuCamera != null) menuCamera.GetComponent<OrbitCamera>().target = previewCar.transform;
    }

    private void OnRacePressed()
    {
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

    // ================================================================ ZÁVOD

    private void StartRace()
    {
        SkidMarks.Create(Mat("skid", new Color(0.09f, 0.09f, 0.1f), 0.05f));

        int total = RaceSetup.opponents + 1;
        float baseSkill = RaceSetup.DifficultySkill[Mathf.Clamp(RaceSetup.difficulty, 0, 2)];
        var usedColors = new List<int> { RaceSetup.colorIndex };

        // Soupeři startují vepředu, hráč z poslední pozice
        for (int i = 0; i < RaceSetup.opponents; i++)
        {
            GridSlot(i, out Vector3 pos, out Quaternion rot);
            int color = NextColor(usedColors);
            var style = (LowPolyCarFactory.BodyStyle)(((int)RaceSetup.carStyle + 1 + i) % 3);
            opponents.Add(CreateAI(pos, rot, style, CarColors[color], baseSkill * (1f - 0.025f * i)));
        }
        GridSlot(total - 1, out Vector3 playerPos, out Quaternion playerRot);
        player = CreatePlayer(playerPos, playerRot);

        CreateCameras(player.transform);
        CreateHud();

        recordBefore = Leaderboard.BestTime(definition.LeaderboardKey);
        player.LapCompleted += OnPlayerLap;
        foreach (var ai in opponents)
        {
            AIPlayer captured = ai;
            ai.LapCompleted += t => OnOpponentLap(captured);
        }
    }

    private static int NextColor(List<int> used)
    {
        for (int i = 0; i < CarColors.Length; i++)
        {
            if (!used.Contains(i))
            {
                used.Add(i);
                return i;
            }
        }
        return used.Count % CarColors.Length;
    }

    private Player CreatePlayer(Vector3 position, Quaternion rotation)
    {
        int layer = Mathf.Max(0, LayerMask.NameToLayer("Car"));
        GameObject go = LowPolyCarFactory.Build("PlayerCar", position, rotation, RaceSetup.carStyle,
            CarPalette(CarColors[RaceSetup.colorIndex]), layer, out Car car);
        Player p = go.AddComponent<Player>();
        go.AddComponent<CarEffects>();
        AddEngineSound(go, 0.6f, false);
        go.SetActive(true);
        return p;
    }

    private AIPlayer CreateAI(Vector3 position, Quaternion rotation, LowPolyCarFactory.BodyStyle style, Color color, float skill)
    {
        // Všechna auta na vrstvě "Car" - podle nastavení fyziky se navzájem neblokují
        int layer = Mathf.Max(0, LayerMask.NameToLayer("Car"));
        GameObject go = LowPolyCarFactory.Build("AICar", position, rotation, style, CarPalette(color), layer, out AICar car);
        car.motorTorque = 340f;
        AIPlayer ai = go.AddComponent<AIPlayer>();
        // Checkpointy jsou husté (~24 m), proto AI sleduje zatáčky víc dopředu
        ai.straightSpeedKmh = 165f;
        ai.cornerSpeedKmh = 62f;
        ai.cornerAngleForMinSpeed = 40f;
        ai.cornerLookAhead = 5;
        ai.brakingSlope = 1.3f;
        ai.lookAheadDistance = 12f;
        ai.skill = skill;
        go.AddComponent<CarEffects>();
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
        source.volume = volume;
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

    private void CreateCameras(Transform target)
    {
        var chase = new GameObject("ChaseCamera");
        chase.tag = "MainCamera";
        Camera chaseCam = SetupCamera(chase);
        chase.AddComponent<AudioListener>();
        chase.transform.position = target.TransformPoint(0f, 3f, -8f);
        var follow2 = chase.AddComponent<CameraFollow2>();
        follow2.target = target;
        follow2.distance = 7.5f;
        follow2.height = 2.8f;
        follow2.damping = 6f;
        follow2.rotationDamping = 8f;

        var top = new GameObject("TopCamera");
        Camera topCam = SetupCamera(top);
        var follow = top.AddComponent<CameraFollow>();
        follow.Target = target;
        follow.offset = new Vector3(0f, 45f, -28f);
        follow.eulerrotation = new Vector3(58f, 0f, 0f);
        follow.damper = 3f;

        var swap = new GameObject("CameraSwap").AddComponent<CameraSwap>();
        swap.cameraOne = chaseCam;
        swap.cameraTwo = topCam;
    }

    private Camera SetupCamera(GameObject go)
    {
        Camera cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = theme.sky;
        cam.fieldOfView = 62f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1600f;
        return cam;
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
        Text trackName = LowPolyUI.Label(pause, definition.name, 30, TextAnchor.MiddleCenter, LowPolyUI.Accent, "Track");
        LowPolyUI.Place(trackName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(900f, 40f));

        var manager = new GameObject("RaceManager");
        pauseMenu = manager.AddComponent<PauseMenu>();
        pauseMenu.pauseMenuUI = pause.gameObject;

        string[] labels = { "RESUME", "RESTART", "CHANGE TRACK", "MAIN MENU" };
        System.Action[] actions =
        {
            () => pauseMenu.Resume(),
            () => pauseMenu.Restart(),
            () => { RaceSetup.configured = false; pauseMenu.Restart(); },
            () => pauseMenu.LoadMenu(),
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

    // ================================================================ cíl a výsledky

    private void OnPlayerLap(float lapTime)
    {
        if (playerFinished) return;
        playerRaceTime += lapTime;

        int lapsDone = player.CurrentLap; // událost přichází před započetím dalšího kola
        if (lapsDone == RaceSetup.laps - 1 && RaceSetup.laps > 1)
            hud.ShowMessage("FINAL LAP!", 2f, LowPolyUI.Accent);

        if (lapsDone < RaceSetup.laps) return;

        playerFinished = true;
        int position = opponentsFinished + 1;
        player.Controller.InputEnabled = false;
        hud.ShowMessage("FINISH!", 2f, Color.white);
        StartCoroutine(ShowResults(position));
    }

    private void OnOpponentLap(AIPlayer ai)
    {
        if (finishedOpponents.Contains(ai)) return;
        if (ai.CurrentLap >= RaceSetup.laps)
        {
            finishedOpponents.Add(ai);
            if (!playerFinished) opponentsFinished++;
        }
    }

    private IEnumerator ShowResults(int position)
    {
        yield return new WaitForSeconds(1.8f);
        if (pauseMenu != null)
        {
            pauseMenu.Resume();
            pauseMenu.enabled = false;
        }

        Transform root = raceCanvas.transform;
        RectTransform panel = LowPolyUI.Panel(root, "Results", new Color(0.03f, 0.05f, 0.1f, 0.88f));
        LowPolyUI.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 640f));

        Text title = LowPolyUI.Label(panel, "RACE FINISHED", 40, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.8f), "Title");
        LowPolyUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(700f, 50f));

        Color[] medal = { new Color(1f, 0.82f, 0.2f), new Color(0.8f, 0.84f, 0.9f), new Color(0.85f, 0.55f, 0.3f) };
        Text pos = LowPolyUI.Label(panel, Ordinal(position), 150, TextAnchor.MiddleCenter,
            position <= 3 ? medal[position - 1] : Color.white, "Position");
        LowPolyUI.Place(pos.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(700f, 170f));
        Text of = LowPolyUI.Label(panel, $"OF {RaceSetup.opponents + 1}   |   {definition.name}", 26, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.7f), "Of", true);
        LowPolyUI.Place(of.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -262f), new Vector2(700f, 36f));

        ResultRow(panel, "TOTAL TIME", TimeFormat.FormatRacing(playerRaceTime), -320f);
        ResultRow(panel, "BEST LAP", TimeFormat.FormatRacing(player.BestLapTime), -366f);
        if (player.BestLapTime < recordBefore)
        {
            Text record = LowPolyUI.Label(panel, "NEW TRACK RECORD!", 30, TextAnchor.MiddleCenter, LowPolyUI.Accent, "Record");
            LowPolyUI.Place(record.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(700f, 40f));
        }

        string[] labels = { "RACE AGAIN", "CHANGE", "MENU" };
        System.Action[] actions =
        {
            () => pauseMenu.Restart(),
            () => { RaceSetup.configured = false; pauseMenu.Restart(); },
            () => pauseMenu.LoadMenu(),
        };
        for (int i = 0; i < labels.Length; i++)
        {
            Image b = LowPolyUI.Button(panel, labels[i], 28, actions[i], out Text label);
            if (i == 0)
            {
                b.color = LowPolyUI.Accent;
                label.color = new Color(0.1f, 0.08f, 0.05f);
            }
            LowPolyUI.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2((i - 1) * 236f, 36f), new Vector2(220f, 76f));
        }
    }

    private static void ResultRow(RectTransform panel, string label, string value, float y)
    {
        Text l = LowPolyUI.Label(panel, label, 28, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.75f), label);
        LowPolyUI.Place(l.rectTransform, new Vector2(0.5f, 1f), new Vector2(-150f, y), new Vector2(300f, 40f));
        Text v = LowPolyUI.Label(panel, value, 32, TextAnchor.MiddleRight, Color.white, label + "Value", true);
        LowPolyUI.Place(v.rectTransform, new Vector2(0.5f, 1f), new Vector2(150f, y), new Vector2(300f, 40f));
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
