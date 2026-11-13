using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vizuální téma tratě: barvy všech povrchů, světlo, mlha, typ krajiny a dekorací.
/// </summary>
public class TrackTheme
{
    public enum Scenery { Meadow, Desert, Snow, Coast, City }

    public string name;
    public Scenery scenery;

    // Obloha a světlo
    public Color sky;
    public float fogStart = 220f, fogEnd = 900f;
    public Color sun;
    public float sunIntensity = 1.15f;
    public Vector3 sunEuler = new Vector3(48f, -35f, 0f);
    public Color ambientSky, ambientEquator, ambientGround;

    // Krajina
    public float hillAmplitude = 40f;
    public float mountainHeight = 55f;
    public bool hasWater;
    [Tooltip("Moře na východní straně (+x) - terén tam klesá pod hladinu.")]
    public bool seaEast;
    public float rockLine = 30f, snowLine = 55f;
    public int treeCount = 600, rockCount = 70, cloudCount = 18;
    [Tooltip("Rovný terén bez kopců (město).")]
    public bool flatGround;

    // Denní doba a počasí (nastaví ApplyNight / ApplyRain)
    public bool night;
    public bool forceNight;
    public bool rain;
    public bool wetRoad;
    /// <summary>Barvy oblohy pro gradient (nahoře / u obzoru / dole).</summary>
    public Color skyTop, skyBottom;

    /// <summary>Barvy podle klíčů, na které se ptá generátor tratě.</summary>
    public readonly Dictionary<string, Color> colors = new Dictionary<string, Color>();

    public Color this[string key] => colors.TryGetValue(key, out Color c) ? c : Color.magenta;

    private static Color C(int hex)
    {
        return new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);
    }

    private TrackTheme Set(string key, int hex)
    {
        colors[key] = C(hex);
        return this;
    }

    /// <summary>Společné barvy (silnice, šachovnice, auta v publiku...), téma si je může přepsat.</summary>
    private static TrackTheme Base()
    {
        return new TrackTheme()
            .Set("asphaltA", 0x45474F).Set("asphaltB", 0x4C4E57).Set("line", 0xF2F2EB)
            .Set("curbA", 0xD9261E).Set("curbB", 0xF2F2F2)
            .Set("barrierA", 0xE6332A).Set("barrierB", 0xF2F2F2)
            .Set("checkerWhite", 0xF7F7F7).Set("checkerBlack", 0x1A1A1F).Set("metal", 0x8C949E)
            .Set("rock", 0x8C8C94).Set("cloud", 0xFFFFFF).Set("trunk", 0x734D2E)
            .Set("tire", 0x1C1C1F).Set("seat", 0x2E6FD9).Set("roof", 0xF2F2F2)
            .Set("crowdA", 0xF2C21F).Set("crowdB", 0xE64D4D).Set("crowdC", 0x4DB3E6).Set("crowdD", 0xF2F2F2)
            .Set("flagA", 0xF2C21F).Set("flagB", 0x2E6FD9).Set("water", 0x2E8CC7).Set("low", 0xE8D49B);
    }

    /// <summary>Doplní výchozí barvy gradientu oblohy podle barvy obzoru.</summary>
    public TrackTheme Finish()
    {
        if (skyTop.a == 0f) skyTop = Color.Lerp(sky, new Color(0.25f, 0.45f, 0.85f), 0.55f);
        if (skyBottom.a == 0f) skyBottom = Color.Lerp(sky, ambientGround, 0.5f);
        if (forceNight && !night) ApplyNight();
        return this;
    }

    /// <summary>Noc: tmavá obloha, měsíc místo slunce, rozsvícené lampy a světla aut.</summary>
    public TrackTheme ApplyNight()
    {
        night = true;
        Color navy = new Color(0.04f, 0.06f, 0.14f);
        sky = Color.Lerp(sky, navy, 0.88f);
        skyTop = new Color(0.01f, 0.02f, 0.06f);
        skyBottom = navy * 0.6f;
        fogEnd *= 0.7f;
        fogStart *= 0.5f;
        sun = new Color(0.55f, 0.65f, 1f);
        sunIntensity = 0.3f;
        sunEuler = new Vector3(42f, 150f, 0f);
        ambientSky = Color.Lerp(ambientSky, navy, 0.75f) * 0.6f;
        ambientEquator = Color.Lerp(ambientEquator, navy, 0.75f) * 0.5f;
        ambientGround = ambientGround * 0.2f;
        colors["cloud"] = new Color(0.22f, 0.25f, 0.35f);
        return this;
    }

    /// <summary>Déšť (na sněhu sněžení): šedá obloha, mokrá lesklá silnice, méně světla.</summary>
    public TrackTheme ApplyRain()
    {
        rain = true;
        wetRoad = scenery != Scenery.Snow;
        Color gray = new Color(0.5f, 0.54f, 0.6f);
        if (!night)
        {
            sky = Color.Lerp(sky, gray, 0.75f);
            skyTop = Color.Lerp(sky, new Color(0.35f, 0.38f, 0.45f), 0.6f);
            skyBottom = sky * 0.8f;
            ambientSky = Color.Lerp(ambientSky, gray, 0.6f);
        }
        fogStart *= 0.45f;
        fogEnd *= 0.6f;
        sunIntensity *= 0.55f;
        colors["cloud"] = colors["cloud"] * 0.65f;
        if (wetRoad)
        {
            colors["asphaltA"] = colors["asphaltA"] * 0.7f;
            colors["asphaltB"] = colors["asphaltB"] * 0.7f;
        }
        cloudCount = Mathf.Max(cloudCount, 30);
        return this;
    }

    public static TrackTheme City()
    {
        var t = Base()
            .Set("asphaltA", 0x34363D).Set("asphaltB", 0x393B43)
            .Set("curbA", 0xE6332A).Set("barrierA", 0xF2C21F).Set("barrierB", 0x8C8F96)
            .Set("vergeA", 0x7A7D85).Set("vergeB", 0x73767E)
            .Set("groundA", 0x5A5D64).Set("groundB", 0x60636B).Set("groundC", 0x4E8548)
            .Set("high", 0x5A5D64).Set("peak", 0x5A5D64)
            .Set("leafA", 0x2E7040).Set("leafB", 0x3D8547).Set("leafC", 0x255C38)
            .Set("accentA", 0x6B7280).Set("accentB", 0x3B4252).Set("accentC", 0x8C6E5A)
            .Set("seat", 0xD926A6).Set("flagA", 0x26D9D9).Set("flagB", 0xD926A6);
        t.name = "City";
        t.scenery = Scenery.City;
        t.sky = C(0x8FA8C7);
        t.sun = C(0xFFF0D1);
        t.ambientSky = C(0xA3B3CC); t.ambientEquator = C(0x8C94A0); t.ambientGround = C(0x4D4F57);
        t.hillAmplitude = 0f;
        t.mountainHeight = 0f;
        t.flatGround = true;
        t.rockLine = 999f; t.snowLine = 999f;
        t.treeCount = 90; t.rockCount = 0; t.cloudCount = 10;
        t.forceNight = true;
        return t;
    }

    public static TrackTheme Meadow()
    {
        var t = Base()
            .Set("vergeA", 0x73B34D).Set("vergeB", 0x6BA847)
            .Set("groundA", 0x7DBD54).Set("groundB", 0x70B04D).Set("groundC", 0x8AC45C)
            .Set("high", 0x8C8C94).Set("peak", 0xF2F7FF)
            .Set("leafA", 0x33853F).Set("leafB", 0x5C9E33).Set("leafC", 0x246B45)
            .Set("accentA", 0xF2E6CC).Set("accentB", 0xB34033).Set("accentC", 0xE6C94D);
        t.name = "Meadow";
        t.scenery = Scenery.Meadow;
        t.sky = C(0x9ECCF2);
        t.sun = C(0xFFF0D1);
        t.ambientSky = C(0xADC7F2); t.ambientEquator = C(0x99A699); t.ambientGround = C(0x525447);
        return t;
    }

    public static TrackTheme Desert()
    {
        var t = Base()
            .Set("asphaltA", 0x55504C).Set("asphaltB", 0x5C5752)
            .Set("curbA", 0xE67A1F).Set("barrierA", 0xE67A1F)
            .Set("vergeA", 0xD9A86B).Set("vergeB", 0xD19E61)
            .Set("groundA", 0xE0B375).Set("groundB", 0xD6A66B).Set("groundC", 0xE8BF82)
            .Set("high", 0xB8613D).Set("peak", 0x9E4A30).Set("rock", 0xA65C3D)
            .Set("leafA", 0x5C8C3D).Set("leafB", 0x6E9E45).Set("leafC", 0x8C7A4D)
            .Set("accentA", 0xC76B42).Set("accentB", 0xA85233).Set("accentC", 0xE69966)
            .Set("seat", 0xE67A1F).Set("flagA", 0xE63333).Set("flagB", 0xF2F2F2);
        t.name = "Desert";
        t.scenery = Scenery.Desert;
        t.sky = C(0xF2CFA0);
        t.fogStart = 260f; t.fogEnd = 1100f;
        t.sun = C(0xFFE3B3);
        t.sunIntensity = 1.3f;
        t.sunEuler = new Vector3(38f, -60f, 0f);
        t.ambientSky = C(0xF2D1A8); t.ambientEquator = C(0xC79E78); t.ambientGround = C(0x6B4D38);
        t.hillAmplitude = 22f;
        t.mountainHeight = 45f;
        t.rockLine = 14f; t.snowLine = 999f;
        t.treeCount = 260; t.rockCount = 120; t.cloudCount = 5;
        return t;
    }

    public static TrackTheme Snow()
    {
        var t = Base()
            .Set("asphaltA", 0x3D4049).Set("asphaltB", 0x434650)
            .Set("curbA", 0x2E6FD9).Set("barrierA", 0x2E6FD9)
            .Set("vergeA", 0xE6EDF5).Set("vergeB", 0xDEE6F0)
            .Set("groundA", 0xEDF2FA).Set("groundB", 0xE0E8F2).Set("groundC", 0xF5F7FC)
            .Set("high", 0x7A808C).Set("peak", 0xFFFFFF)
            .Set("leafA", 0x1F5C40).Set("leafB", 0x2B6B4A).Set("leafC", 0xF5F7FC)
            .Set("accentA", 0xFFFFFF).Set("accentB", 0xE6731F).Set("accentC", 0x262626)
            .Set("seat", 0xD9261E).Set("flagA", 0x2E6FD9).Set("flagB", 0xFFFFFF).Set("cloud", 0xE6EBF2);
        t.name = "Snow";
        t.scenery = Scenery.Snow;
        t.sky = C(0xC7D6E6);
        t.fogStart = 150f; t.fogEnd = 700f;
        t.sun = C(0xFFF5EB);
        t.sunIntensity = 1.0f;
        t.sunEuler = new Vector3(28f, 20f, 0f);
        t.ambientSky = C(0xC7D6EB); t.ambientEquator = C(0xB3BDC7); t.ambientGround = C(0x8C94A3);
        t.hillAmplitude = 55f;
        t.mountainHeight = 85f;
        t.rockLine = 45f; t.snowLine = 999f;
        t.treeCount = 750; t.rockCount = 60; t.cloudCount = 26;
        return t;
    }

    public static TrackTheme Coast()
    {
        var t = Base()
            .Set("curbA", 0xF2C21F).Set("curbB", 0x2E6FD9).Set("barrierA", 0x2EA6D9)
            .Set("vergeA", 0x8CBF57).Set("vergeB", 0x85B852)
            .Set("groundA", 0x94C75E).Set("groundB", 0x87BA54).Set("groundC", 0x9ECF66)
            .Set("high", 0x9E948A).Set("peak", 0xC7BDB3).Set("low", 0xF0DBA3)
            .Set("leafA", 0x3D9E45).Set("leafB", 0x59B33D).Set("leafC", 0x2E8547)
            .Set("accentA", 0xFFFFFF).Set("accentB", 0xE6332A).Set("accentC", 0xF2C21F)
            .Set("water", 0x2EA6C7).Set("seat", 0x2EA6D9).Set("cloud", 0xFFE6E0)
            .Set("flagA", 0xF2C21F).Set("flagB", 0xE6332A);
        t.name = "Coast";
        t.scenery = Scenery.Coast;
        t.sky = C(0xF7BFA0);
        t.fogStart = 250f; t.fogEnd = 1000f;
        t.sun = C(0xFFC799);
        t.sunIntensity = 1.2f;
        t.sunEuler = new Vector3(18f, 70f, 0f);
        t.ambientSky = C(0xF2B8A8); t.ambientEquator = C(0xC7A399); t.ambientGround = C(0x6B5C52);
        t.hillAmplitude = 30f;
        t.mountainHeight = 50f;
        t.hasWater = true;
        t.seaEast = true;
        t.rockLine = 26f; t.snowLine = 999f;
        t.treeCount = 420; t.rockCount = 90; t.cloudCount = 14;
        return t;
    }
}
