using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Postaví celou low-poly závodní scénu při spuštění: trať, auta, kamery, světlo, HUD a pauzovací menu.
/// Ve scéně stačí jeden objekt s tímto skriptem. Všechno se dá ladit v Inspectoru.
/// </summary>
public class LowPolyRaceBootstrap : MonoBehaviour
{
    [Header("Assety (volitelné)")]
    [Tooltip("Materiál, ze kterého se kopírují všechny barvy (Standard shader).")]
    public Material baseMaterial;
    public Font uiFont;
    public TMP_FontAsset tmpFont;
    public AudioClip engineClip;

    [Header("Trať")]
    public TrackGenerator.TrackSettings track = new TrackGenerator.TrackSettings();

    [Header("Auta")]
    public LowPolyCarFactory.BodyStyle playerStyle = LowPolyCarFactory.BodyStyle.Sport;
    public Color playerColor = new Color(0.92f, 0.18f, 0.12f);
    public LowPolyCarFactory.BodyStyle aiStyle = LowPolyCarFactory.BodyStyle.Muscle;
    public Color aiColor = new Color(0.12f, 0.45f, 0.95f);
    [Range(0.5f, 1.3f)] public float aiSkill = 0.95f;

    [Header("Prostředí")]
    public Color skyColor = new Color(0.62f, 0.8f, 0.95f);
    public Color sunColor = new Color(1f, 0.94f, 0.82f);

    private readonly Dictionary<Color, Material> materialCache = new Dictionary<Color, Material>();

    private void Awake()
    {
        SetupEnvironment();

        var world = new GameObject("World").transform;
        TrackGenerator.TrackResult result = TrackGenerator.Build(world, track, new Palette(this));

        // Startovní rošt kousek za startovní čarou
        const int gridIndex = -3;
        Vector3 spawnCenter = result.Center(gridIndex) + Vector3.up * 0.6f;
        Quaternion spawnRotation = Quaternion.LookRotation(result.Forward(gridIndex), Vector3.up);
        Vector3 right = result.Right(gridIndex);

        Player player = CreatePlayer(spawnCenter - right * 3f, spawnRotation);
        AIPlayer ai = CreateAI(spawnCenter + right * 3f + result.Forward(gridIndex) * -6f, spawnRotation);

        CreateCameras(player.transform);
        CreateUI(player, ai);
    }

    // ---------------------------------------------------------------- prostředí

    private void SetupEnvironment()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.68f, 0.78f, 0.95f);
        RenderSettings.ambientEquatorColor = new Color(0.6f, 0.65f, 0.6f);
        RenderSettings.ambientGroundColor = new Color(0.32f, 0.33f, 0.28f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = skyColor;
        RenderSettings.fogStartDistance = 220f;
        RenderSettings.fogEndDistance = 900f;
        QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, 150f);

        if (FindObjectOfType<Light>() == null)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = sunColor;
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = sun;
        }
    }

    // ---------------------------------------------------------------- materiály

    private Material Mat(Color color, float smoothness = 0.15f, Color? emission = null)
    {
        if (emission == null && materialCache.TryGetValue(color, out Material cached))
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
        else
            materialCache[color] = m;
        return m;
    }

    private class Palette : TrackGenerator.IPalette
    {
        private readonly LowPolyRaceBootstrap owner;
        public Palette(LowPolyRaceBootstrap owner) { this.owner = owner; }

        public Material Get(string key)
        {
            switch (key)
            {
                case "asphaltA": return owner.Mat(new Color(0.27f, 0.28f, 0.31f), 0.05f);
                case "asphaltB": return owner.Mat(new Color(0.3f, 0.31f, 0.34f), 0.05f);
                case "white": return owner.Mat(new Color(0.95f, 0.95f, 0.92f));
                case "red": return owner.Mat(new Color(0.85f, 0.15f, 0.12f));
                case "vergeA": return owner.Mat(new Color(0.45f, 0.7f, 0.3f), 0.05f);
                case "vergeB": return owner.Mat(new Color(0.42f, 0.66f, 0.28f), 0.05f);
                case "barrierA": return owner.Mat(new Color(0.9f, 0.2f, 0.15f));
                case "barrierB": return owner.Mat(new Color(0.95f, 0.95f, 0.95f));
                case "checkerWhite": return owner.Mat(new Color(0.97f, 0.97f, 0.97f));
                case "checkerBlack": return owner.Mat(new Color(0.1f, 0.1f, 0.12f));
                case "metal": return owner.Mat(new Color(0.55f, 0.58f, 0.62f), 0.4f);
                case "grassA": return owner.Mat(new Color(0.49f, 0.74f, 0.33f), 0.05f);
                case "grassB": return owner.Mat(new Color(0.44f, 0.69f, 0.3f), 0.05f);
                case "grassC": return owner.Mat(new Color(0.54f, 0.77f, 0.36f), 0.05f);
                case "rock": return owner.Mat(new Color(0.55f, 0.55f, 0.58f), 0.05f);
                case "snow": return owner.Mat(new Color(0.95f, 0.97f, 1f), 0.1f);
                case "trunk": return owner.Mat(new Color(0.45f, 0.3f, 0.18f), 0.05f);
                case "leafA": return owner.Mat(new Color(0.2f, 0.52f, 0.25f), 0.05f);
                case "leafB": return owner.Mat(new Color(0.36f, 0.62f, 0.2f), 0.05f);
                case "leafC": return owner.Mat(new Color(0.14f, 0.42f, 0.27f), 0.05f);
                case "cloud": return owner.Mat(new Color(1f, 1f, 1f), 0f);
                default: return owner.Mat(Color.magenta);
            }
        }
    }

    private LowPolyCarFactory.Palette CarPalette(Color paint)
    {
        return new LowPolyCarFactory.Palette
        {
            paint = Mat(paint, 0.55f),
            glass = Mat(new Color(0.12f, 0.16f, 0.22f), 0.85f),
            dark = Mat(new Color(0.12f, 0.12f, 0.13f)),
            headlight = Mat(new Color(1f, 0.97f, 0.85f), 0.5f, new Color(0.9f, 0.85f, 0.6f)),
            taillight = Mat(new Color(0.8f, 0.05f, 0.05f), 0.5f, new Color(0.6f, 0f, 0f)),
            trim = Mat(new Color(0.92f, 0.92f, 0.92f), 0.4f),
            tire = Mat(new Color(0.1f, 0.1f, 0.1f), 0.05f),
            hub = Mat(new Color(0.7f, 0.72f, 0.75f), 0.5f),
        };
    }

    // ---------------------------------------------------------------- auta

    private Player CreatePlayer(Vector3 position, Quaternion rotation)
    {
        int layer = Mathf.Max(0, LayerMask.NameToLayer("Car"));
        GameObject go = LowPolyCarFactory.Build("PlayerCar", position, rotation, playerStyle, CarPalette(playerColor), layer, out Car car);
        Player player = go.AddComponent<Player>();
        AddEngineSound(go, 0.6f, false);
        go.SetActive(true);
        return player;
    }

    private AIPlayer CreateAI(Vector3 position, Quaternion rotation)
    {
        int layer = Mathf.Max(0, LayerMask.NameToLayer("Car2"));
        GameObject go = LowPolyCarFactory.Build("AICar", position, rotation, aiStyle, CarPalette(aiColor), layer, out AICar car);
        car.motorTorque = 340f;
        AIPlayer ai = go.AddComponent<AIPlayer>();
        // Checkpointy jsou tu husté (~24 m), proto AI sleduje zatáčky víc dopředu
        ai.straightSpeedKmh = 165f;
        ai.cornerSpeedKmh = 62f;
        ai.cornerAngleForMinSpeed = 40f;
        ai.cornerLookAhead = 5;
        ai.brakingSlope = 1.3f;
        ai.lookAheadDistance = 12f;
        ai.skill = aiSkill;
        AddEngineSound(go, 0.45f, true);
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

    // ---------------------------------------------------------------- kamery

    private void CreateCameras(Transform target)
    {
        foreach (var existing in FindObjectsOfType<Camera>())
            existing.gameObject.SetActive(false);

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
        cam.backgroundColor = skyColor;
        cam.fieldOfView = 62f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1500f;
        return cam;
    }

    // ---------------------------------------------------------------- UI

    private Font LegacyFont => uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("Arial.ttf");

    private void CreateUI(Player player, AIPlayer ai)
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // --- HUD vlevo nahoře
        RectTransform panel = CreatePanel("RacePanel", canvasGo.transform, new Color(0f, 0f, 0f, 0.45f));
        SetRect(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(440f, 230f));

        Text[] lines = new Text[5];
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = CreateText("Line" + i, panel, "", 30, TextAnchor.MiddleLeft, Color.white);
            SetRect(lines[i].rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -14f - i * 41f), new Vector2(-40f, 40f));
        }
        lines[4].color = new Color(0.6f, 0.8f, 1f);

        // --- pauzovací menu
        RectTransform pause = CreatePanel("Pause", canvasGo.transform, new Color(0.02f, 0.04f, 0.08f, 0.72f));
        SetRect(pause, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        pause.gameObject.SetActive(false);

        Text title = CreateText("Title", pause, "PAUSED", 90, TextAnchor.MiddleCenter, Color.white);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(900f, 140f));

        var manager = new GameObject("RaceManager");
        var pauseMenu = manager.AddComponent<PauseMenu>();
        pauseMenu.pauseMenuUI = pause.gameObject;

        CreateButton(pause, "RESUME", 0, pauseMenu.Resume);
        CreateButton(pause, "RESTART", 1, pauseMenu.Restart);
        CreateButton(pause, "MENU", 2, pauseMenu.LoadMenu);
        CreateButton(pause, "QUIT", 3, pauseMenu.Quit);

        // Tabulka nejlepších časů (struktura, kterou očekává Leaderboard)
        RectTransform board = CreatePanel("TopTimesBackground", pause, new Color(1f, 1f, 1f, 0.08f));
        SetRect(board, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(260f, -40f), new Vector2(420f, 300f));
        Text boardTitle = CreateText("TopTimesTitle", board, "TOP TIMES", 34, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.25f));
        SetRect(boardTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(0f, 50f));

        var topTimes = new GameObject("TopTimesPanel", typeof(RectTransform)).GetComponent<RectTransform>();
        topTimes.SetParent(pause, false);
        SetRect(topTimes, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(260f, 50f), new Vector2(380f, 200f));

        var template = new GameObject("LeaderboardTemplate", typeof(RectTransform)).GetComponent<RectTransform>();
        template.SetParent(topTimes, false);
        SetRect(template, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 30f));
        CreateTmp("PositionText", template, TextAlignmentOptions.Left);
        CreateTmp("TimeText", template, TextAlignmentOptions.Right);

        var leaderboard = pause.gameObject.AddComponent<Leaderboard>();
        leaderboard.storageKey = "leaderboard_lowpoly";

        // --- UIController (rychloměr, pořadí a odpočet si vytvoří sám)
        var ui = manager.AddComponent<UIController>();
        ui.UIRacePanel = panel.gameObject;
        ui.UITextCurrentLap = lines[0];
        ui.UITextCurrentTime = lines[1];
        ui.UITextLastLap = lines[2];
        ui.UITextBestLap = lines[3];
        ui.UITextAIBestLap = lines[4];
        ui.UpdateUIForPlayer = player;
        ui.UpdateUIForAIPlayer = ai;
        ui.leaderboard = leaderboard;

        // Pauzovací menu musí být vykresleno nad vším ostatním
        pause.SetAsLastSibling();
    }

    private void CreateButton(RectTransform parent, string label, int index, UnityAction action)
    {
        RectTransform rect = CreatePanel(label + "Button", parent, new Color(1f, 1f, 1f, 0.12f));
        SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
            new Vector2(-260f, 100f - index * 95f), new Vector2(360f, 75f));

        var button = rect.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.85f, 0.3f, 1f);
        colors.pressedColor = new Color(1f, 0.6f, 0.2f, 1f);
        button.colors = colors;
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(() =>
        {
            if (AudioManager.instance != null) AudioManager.instance.Play("Click");
            action();
        });

        Text text = CreateText("Label", rect, label, 36, TextAnchor.MiddleCenter, Color.white);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
    }

    private RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
        return (RectTransform)go.transform;
    }

    private Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = LegacyFont;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        go.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
        return text;
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
        SetRect(tmp.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-30f, 0f));
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
