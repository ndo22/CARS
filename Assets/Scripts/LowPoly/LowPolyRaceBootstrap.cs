using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Řídí celou low-poly scénu (je to hlavní scéna hry):
/// titulní menu, výběr závodu, nastavení, samotný závod, výsledky a šampionát.
/// Ve scéně stačí jeden objekt s tímto skriptem - vše ostatní se postaví kódem.
/// Rozděleno do souborů: jádro (tento), .Menu.cs (obrazovky menu), .Race.cs (závod).
/// </summary>
public partial class LowPolyRaceBootstrap : MonoBehaviour
{
    [Header("Assety (volitelné)")]
    [Tooltip("Materiál, ze kterého se kopírují všechny barvy (Standard shader).")]
    public Material baseMaterial;
    public Font uiFont;
    [Tooltip("Písmo pro časy a čísla (musí mít znaky ' \" . ,).")]
    public Font numberFont;
    public TMP_FontAsset tmpFont;
    public AudioClip engineClip;

    [Header("Shadery (volitelné - když chybí, použije se Standard)")]
    public Shader skyShader;
    public Shader waterShader;
    public Shader foliageShader;
    public Shader ghostShader;
    public Shader postFxShader;

    public static readonly Color[] CarColors =
    {
        new Color(0.92f, 0.18f, 0.12f), new Color(0.12f, 0.45f, 0.95f), new Color(0.98f, 0.76f, 0.12f),
        new Color(0.2f, 0.72f, 0.3f), new Color(0.98f, 0.48f, 0.1f), new Color(0.55f, 0.25f, 0.85f),
        new Color(0.94f, 0.94f, 0.94f), new Color(0.1f, 0.74f, 0.74f),
        // Odemykatelné (vítězství v šampionátu): zlatá a chrom
        new Color(0.95f, 0.72f, 0.2f), new Color(0.78f, 0.8f, 0.84f),
    };

    // Trať a prostředí
    private TrackLibrary.TrackDefinition definition;
    private TrackTheme theme;
    /// <summary>Podmínky na konci závodu (liší se od startu, když se mění denní doba nebo počasí).</summary>
    private TrackTheme endTheme;
    private bool Dynamic => RaceSetup.dynamicTime || RaceSetup.dynamicWeather;
    private TrackGenerator.TrackSettings settings;
    private TrackGenerator.TrackResult track;
    private Transform world;
    private Light sun;
    private SkyAndWeather weather;
    private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    private AudioSource music;

    private void Awake()
    {
        LowPolyUI.font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
        LowPolyUI.numberFont = numberFont != null ? numberFont : LowPolyUI.font;
        GameSettings.Apply();
        ProceduralAudio.Prepare();

        // Náhled tratě z editoru (menu CARS) do hry nepatří
        var preview = GameObject.Find(EditorPreviewName);
        if (preview != null)
        {
            foreach (var t in preview.GetComponentsInChildren<Transform>(true))
                if (t.name == "Checkpoints") t.name = "Checkpoints_preview";
            Destroy(preview);
        }

        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        music.playOnAwake = false;
        music.ignoreListenerPause = true;

        if (RaceSetup.mode == RaceSetup.Mode.Championship && Championship.active && RaceSetup.configured)
            RaceSetup.trackIndex = Mathf.Clamp(Championship.round, 0, TrackLibrary.All.Length - 1);
        RaceSetup.trackIndex = Mathf.Clamp(RaceSetup.trackIndex, 0, TrackLibrary.Count - 1);
        if (!Profile.IsUnlocked(RaceSetup.carStyle)) RaceSetup.carStyle = LowPolyCarFactory.BodyStyle.Sport;
        if (!Profile.IsColorUnlocked(RaceSetup.colorIndex)) RaceSetup.colorIndex = 0;

        // Honička s policií má vlastní dráhu - okruh se nestaví
        if (RaceSetup.configured && RaceSetup.mode == RaceSetup.Mode.Chase)
        {
            StartChase();
            return;
        }

        LoadTrack(RaceSetup.trackIndex);

        if (RaceSetup.configured)
            StartRace();
        else
            ShowScreen(RaceSetup.screen);
    }

    private System.Func<AudioClip> wantedMusic;

    private void Update()
    {
        if (music != null)
        {
            music.volume = GameSettings.Music * 0.45f;
            // Hudba se počítá na pozadí - spustit, jakmile je hotová
            if (wantedMusic != null && music.clip == null) PlayMusic(wantedMusic);
        }
        UpdateMenu();
        UpdateRace();
        UpdateChase();
    }

    private void PlayMusic(System.Func<AudioClip> source)
    {
        wantedMusic = source;
        AudioClip clip = source();
        if (clip == null) { music.clip = null; return; }
        if (music.clip == clip && music.isPlaying) return;
        music.clip = clip;
        music.Play();
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

        definition = TrackLibrary.Get(index);
        bool startNight = RaceSetup.night && !RaceSetup.dynamicTime, startRain = RaceSetup.rain && !RaceSetup.dynamicWeather;
        theme = TrackLibrary.Theme(definition, startNight, startRain);
        endTheme = Dynamic ? TrackLibrary.Theme(definition, startNight || RaceSetup.dynamicTime, startRain || RaceSetup.dynamicWeather) : theme;
        settings = TrackLibrary.Settings(definition, theme);
        settings.forceLamps = RaceSetup.dynamicTime;
        materials.Clear();
        LampCuller.Darkness = theme.night ? 1f : 0f;
        SetupEnvironment();

        world = new GameObject("World").transform;
        track = TrackGenerator.Build(world, settings, theme, new Palette(this));
    }

    private void SetupEnvironment()
    {
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
            sun.cookieSize = 400f;
            RenderSettings.sun = sun;
        }
        sun.color = theme.sun;
        sun.intensity = theme.sunIntensity;
        sun.transform.rotation = Quaternion.Euler(theme.sunEuler);
        // Stíny mraků jen ve dne a za hezkého počasí
        sun.cookie = !theme.night && !theme.rain ? SkyAndWeather.CloudCookie(settings.seed) : null;

        bool gradientSky = SkyAndWeather.SetupSky(skyShader, theme, sun);
        if (!gradientSky) RenderSettings.skybox = null;
        foreach (var cam in FindObjectsOfType<Camera>())
            ConfigureCameraSky(cam);

        if (weather == null)
        {
            weather = new GameObject("Weather").AddComponent<SkyAndWeather>();
            weather.sun = sun;
        }
        foreach (Transform child in weather.transform) Destroy(child.gameObject);
        if (theme.rain)
            weather.StartPrecipitation(theme.scenery == TrackTheme.Scenery.Snow,
                Mat("precip", theme.scenery == TrackTheme.Scenery.Snow ? Color.white : new Color(0.75f, 0.8f, 0.9f), 0.6f));
    }

    private void ConfigureCameraSky(Camera cam)
    {
        cam.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        cam.backgroundColor = theme.sky;
    }

    private Camera SetupCamera(GameObject go)
    {
        Camera cam = go.AddComponent<Camera>();
        ConfigureCameraSky(cam);
        cam.fieldOfView = 62f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1600f;
        var fx = go.AddComponent<LowPolyPostFX>();
        fx.shader = postFxShader;
        if (theme.night)
        {
            fx.threshold = 0.6f;
            fx.bloomIntensity = 1.1f;
            fx.tint = new Color(0.92f, 0.95f, 1.08f);
        }
        if (weather != null && weather.followCamera == null) weather.followCamera = go.transform;
        return cam;
    }

    // ================================================================ materiály

    private Material Mat(string cacheKey, Color color, float smoothness = 0.12f, Color? emission = null, Shader shader = null)
    {
        if (materials.TryGetValue(cacheKey, out Material cached))
            return cached;

        Material m;
        if (shader != null && shader.isSupported)
            m = new Material(shader);
        else
            m = baseMaterial != null ? new Material(baseMaterial) : new Material(Shader.Find("Standard"));
        if (m.HasProperty("_MainTex")) m.mainTexture = null;
        if (m.HasProperty("_Color")) m.color = color;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (emission != null && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
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
            float glow = t.night ? 1.6f : 0.8f;
            switch (key)
            {
                case "startLight": return owner.Mat(key, new Color(0.6f, 0.05f, 0.05f), 0.6f, new Color(1f, 0.15f, 0.1f));
                case "lamp": return owner.Mat(key, new Color(1f, 0.95f, 0.7f), 0.5f, new Color(1f, 0.85f, 0.5f) * (t.night ? 2f : 0.6f));
                case "windowA": return owner.Mat(key, new Color(0.3f, 0.27f, 0.15f), 0.6f, new Color(1f, 0.82f, 0.45f) * glow);
                case "windowB": return owner.Mat(key, new Color(0.1f, 0.25f, 0.3f), 0.6f, new Color(0.3f, 0.9f, 1f) * glow);
                case "windowC": return owner.Mat(key, new Color(0.3f, 0.1f, 0.25f), 0.6f, new Color(1f, 0.35f, 0.85f) * glow);
                case "neonA": return owner.Mat(key, new Color(0.1f, 0.4f, 0.45f), 0.6f, new Color(0.2f, 1f, 1f) * 2f);
                case "neonB": return owner.Mat(key, new Color(0.45f, 0.1f, 0.35f), 0.6f, new Color(1f, 0.2f, 0.8f) * 2f);
                case "roof": return owner.Mat(key, new Color(0.25f, 0.26f, 0.3f), 0.1f);
                case "boost": return owner.Mat(key, new Color(0.1f, 0.5f, 0.6f), 0.5f, new Color(0.2f, 0.9f, 1f) * 2f);
                case "water": return owner.WaterMat(key, t["water"]);
                case "waterB": return owner.WaterMat(key, Color.Lerp(t["water"], Color.white, 0.12f));
                case "metal": return owner.Mat(key, t["metal"], 0.45f);
                case "leafA":
                case "leafB":
                case "leafC":
                    return owner.Mat(key, t[key], 0.05f, null, owner.foliageShader);
                case "peak": return owner.Mat(key, t[key], 0.2f);
                default:
                    if (key.StartsWith("asphalt"))
                        return owner.Mat(key, t[key], t.wetRoad ? 0.72f : 0.04f);
                    return owner.Mat(key, t[key], key.StartsWith("ground") ? 0.04f : 0.12f);
            }
        }
    }

    private Material WaterMat(string key, Color color)
    {
        Material m = Mat(key, color, 0.85f, null, waterShader);
        if (m.HasProperty("_DeepColor")) m.SetColor("_DeepColor", color * 0.7f);
        return m;
    }

    private LowPolyCarFactory.Palette CarPalette(Color paint, bool chrome = false)
    {
        return new LowPolyCarFactory.Palette
        {
            paint = Mat("paint" + paint, paint, chrome ? 0.92f : 0.6f),
            glass = Mat("glass", new Color(0.12f, 0.16f, 0.22f), 0.85f),
            dark = Mat("carDark", new Color(0.12f, 0.12f, 0.13f)),
            headlight = Mat("headlight", new Color(1f, 0.97f, 0.85f), 0.5f, new Color(0.9f, 0.85f, 0.6f) * (theme.night ? 2f : 1f)),
            taillight = Mat("taillight", new Color(0.8f, 0.05f, 0.05f), 0.5f, new Color(0.6f, 0f, 0f) * (theme.night ? 2f : 1f)),
            trim = Mat("trim", new Color(0.92f, 0.92f, 0.92f), 0.4f),
            tire = Mat("carTire", new Color(0.1f, 0.1f, 0.1f), 0.05f),
            hub = Mat("hub", new Color(0.72f, 0.74f, 0.78f), 0.55f),
            flame = Mat("flame", new Color(1f, 0.5f, 0.1f), 0.2f, new Color(1f, 0.55f, 0.15f) * 3f),
        };
    }

    private LowPolyCarFactory.Palette GhostPalette()
    {
        Material ghost;
        if (ghostShader != null && ghostShader.isSupported)
        {
            ghost = Mat("ghost", new Color(0.4f, 0.85f, 1f, 0.35f), 0f, null, ghostShader);
        }
        else
        {
            // Bez shaderu: světlý "hologram" s vlastní září (neprůhledný)
            ghost = Mat("ghost", new Color(0.3f, 0.6f, 0.75f), 0.2f, new Color(0.2f, 0.6f, 0.8f));
        }
        return new LowPolyCarFactory.Palette
        {
            paint = ghost, glass = ghost, dark = ghost, headlight = ghost, taillight = ghost, trim = ghost, tire = ghost, hub = ghost,
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

    // ================================================================ náhled v editoru

    public const string EditorPreviewName = "LowPolyTrackPreview (editor only)";

    /// <summary>Postaví trať do scény v editoru (volá ji menu CARS > Low Poly > Preview Track). Při spuštění hry se smaže.</summary>
    public GameObject BuildEditorPreview(int index, bool night, bool rain)
    {
        var old = GameObject.Find(EditorPreviewName);
        if (old != null) DestroyImmediate(old);

        definition = TrackLibrary.Get(Mathf.Clamp(index, 0, TrackLibrary.Count - 1));
        theme = TrackLibrary.Theme(definition, night, rain);
        settings = TrackLibrary.Settings(definition, theme);
        materials.Clear();
        var root = new GameObject(EditorPreviewName);
        track = TrackGenerator.Build(root.transform, settings, theme, new Palette(this));
        RenderSettings.fog = true;
        RenderSettings.fogColor = theme.sky;
        RenderSettings.fogStartDistance = theme.fogStart;
        RenderSettings.fogEndDistance = theme.fogEnd;
        return root;
    }
}
