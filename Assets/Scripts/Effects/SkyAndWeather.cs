using UnityEngine;

/// <summary>
/// Obloha (gradientový skybox), stíny mraků (cookie na slunci, která se posouvá)
/// a déšť / sněžení kolem kamery.
/// </summary>
public class SkyAndWeather : MonoBehaviour
{
    public Light sun;
    public Transform followCamera;
    public Vector2 wind = new Vector2(4f, 1.5f);

    private ParticleSystem precipitation;

    /// <summary>Gradientová obloha. Když shader chybí nebo nejde, vrátí false a scéna použije jednobarevné pozadí.</summary>
    public static bool SetupSky(Shader skyShader, TrackTheme theme, Light sun)
    {
        if (skyShader == null || !skyShader.isSupported) return false;
        var mat = new Material(skyShader);
        mat.SetColor("_TopColor", theme.skyTop);
        mat.SetColor("_HorizonColor", theme.sky);
        mat.SetColor("_BottomColor", theme.skyBottom);
        Color sunColor = theme.night ? new Color(0.85f, 0.9f, 1f) : theme.sun;
        if (theme.rain && !theme.night) sunColor *= 0.15f;
        mat.SetColor("_SunColor", sunColor);
        mat.SetVector("_SunDir", sun != null ? -sun.transform.forward : Vector3.up);
        mat.SetFloat("_SunSize", theme.night ? 0.9992f : 0.9985f);
        RenderSettings.skybox = mat;
        return true;
    }

    /// <summary>Textura stínů mraků (alfa kanál) pro cookie směrového světla.</summary>
    public static Texture2D CloudCookie(int seed)
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.Alpha8, false) { wrapMode = TextureWrapMode.Repeat };
        var pixels = new Color32[size * size];
        float ox = seed % 100;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Dlaždicovatelný šum: průměr ze čtyř posunutých vzorků
                float u = x / (float)size, v = y / (float)size;
                float n = Tile(u, v, ox, 4f) * 0.65f + Tile(u, v, ox + 30f, 8f) * 0.35f;
                float a = Mathf.Lerp(1f, 0.55f, Mathf.SmoothStep(0.45f, 0.65f, n));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static float Tile(float u, float v, float offset, float scale)
    {
        float a = Mathf.PerlinNoise(u * scale + offset, v * scale + offset);
        float b = Mathf.PerlinNoise((u - 1f) * scale + offset, v * scale + offset);
        float c = Mathf.PerlinNoise(u * scale + offset, (v - 1f) * scale + offset);
        float d = Mathf.PerlinNoise((u - 1f) * scale + offset, (v - 1f) * scale + offset);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    /// <summary>Déšť (rychlé tenké kapky) nebo sníh (pomalé vločky) kolem kamery.</summary>
    public void StartPrecipitation(bool snow, Material material)
    {
        var go = new GameObject(snow ? "Snowfall" : "Rain");
        go.transform.SetParent(transform, false);
        precipitation = go.AddComponent<ParticleSystem>();
        precipitation.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = precipitation.main;
        main.loop = true;
        main.startLifetime = snow ? 6f : 1.2f;
        main.startSpeed = snow ? 3f : 28f;
        main.maxParticles = snow ? 2500 : 4000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSize3D = true;
        main.startSizeX = snow ? 0.09f : 0.025f;
        main.startSizeY = snow ? 0.09f : 0.7f;
        main.startSizeZ = snow ? 0.09f : 0.025f;
        main.gravityModifier = 0f;

        var emission = precipitation.emission;
        emission.rateOverTime = snow ? 500f : 2200f;

        var shape = precipitation.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        // Box otočený o 90° kolem X: jeho lokální Z (směr emise) míří dolů, lokální Z rozměr je tloušťka
        shape.rotation = new Vector3(90f, 0f, 0f);
        shape.scale = new Vector3(50f, 50f, 1f);

        if (snow)
        {
            var noise = precipitation.noise;
            noise.enabled = true;
            noise.strength = 0.8f;
            noise.frequency = 0.3f;
        }

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        var mb = new MeshBuilder(1);
        mb.Box(0, Vector3.zero, Vector3.one);
        renderer.mesh = mb.ToMesh("Drop");
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        precipitation.Play();
    }

    private void LateUpdate()
    {
        if (sun != null)
        {
            // Posun světla posouvá cookie - stíny mraků putují po krajině
            sun.transform.position += new Vector3(wind.x, 0f, wind.y) * Time.deltaTime;
        }
        if (followCamera != null && precipitation != null)
        {
            Vector3 p = followCamera.position;
            Quaternion yaw = Quaternion.Euler(0f, followCamera.eulerAngles.y, 0f);
            precipitation.transform.SetPositionAndRotation(p + Vector3.up * 16f + yaw * Vector3.forward * 10f, yaw);
        }
    }
}
