using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plynulá změna podmínek během závodu: ze dne do noci a/nebo ze sucha do deště.
/// Průběh se řídí tím, kolik závodu hráč ujel (0 = start, 1 = cíl).
/// </summary>
public class DynamicConditions : MonoBehaviour
{
    public TrackTheme start, end;
    public bool changeTime, changeWeather;
    public Light sun;
    public Material skyMaterial;
    public RacerBase leader;
    public int totalLaps = 3;
    public List<Material> asphalt = new List<Material>();
    public SkyAndWeather weather;
    public Material rainMaterial;
    public bool snow;

    private readonly List<CarController> cars = new List<CarController>();
    private readonly Dictionary<Light, float> headlights = new Dictionary<Light, float>();
    private bool rainStarted;
    private float startDry = 1f, endWet = 0.78f;

    private void Start()
    {
        foreach (var c in FindObjectsOfType<CarController>())
        {
            cars.Add(c);
            Transform h = c.transform.Find("Headlights");
            if (h != null)
            {
                var l = h.GetComponent<Light>();
                if (l != null) headlights[l] = l.intensity;
            }
        }
        if (snow) endWet = 0.72f;
    }

    private float Progress()
    {
        if (leader == null || CheckpointSet.Find().Length == 0) return 0f;
        float per = CheckpointSet.Find().Length;
        return Mathf.Clamp01(leader.RaceProgress / Mathf.Max(1f, per * totalLaps));
    }

    private void Update()
    {
        float p = Progress();
        // Stmívání probíhá v druhé čtvrtině až konci závodu, déšť začne v polovině
        float tTime = changeTime ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.85f, p)) : 0f;
        float tRain = changeWeather ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.65f, p)) : 0f;
        float t = Mathf.Max(tTime, tRain);

        RenderSettings.ambientSkyColor = Color.Lerp(start.ambientSky, end.ambientSky, t);
        RenderSettings.ambientEquatorColor = Color.Lerp(start.ambientEquator, end.ambientEquator, t);
        RenderSettings.ambientGroundColor = Color.Lerp(start.ambientGround, end.ambientGround, t);
        RenderSettings.fogColor = Color.Lerp(start.sky, end.sky, t);
        RenderSettings.fogStartDistance = Mathf.Lerp(start.fogStart, end.fogStart, t);
        RenderSettings.fogEndDistance = Mathf.Lerp(start.fogEnd, end.fogEnd, t);
        if (sun != null)
        {
            sun.color = Color.Lerp(start.sun, end.sun, t);
            sun.intensity = Mathf.Lerp(start.sunIntensity, end.sunIntensity, t);
            Quaternion a = Quaternion.Euler(start.sunEuler), b = Quaternion.Euler(end.sunEuler);
            Vector3 pos = sun.transform.position;
            sun.transform.rotation = Quaternion.Slerp(a, b, tTime);
            sun.transform.position = pos;
            if (t > 0.3f) sun.cookie = null;
        }
        if (skyMaterial != null)
        {
            skyMaterial.SetColor("_TopColor", Color.Lerp(start.skyTop, end.skyTop, t));
            skyMaterial.SetColor("_HorizonColor", Color.Lerp(start.sky, end.sky, t));
            skyMaterial.SetColor("_BottomColor", Color.Lerp(start.skyBottom, end.skyBottom, t));
            if (sun != null) skyMaterial.SetVector("_SunDir", -sun.transform.forward);
        }
        foreach (var cam in Camera.allCameras)
            cam.backgroundColor = RenderSettings.fogColor;

        // Lampy a světla aut
        LampCuller.Darkness = changeTime ? tTime : (end.night ? 1f : 0f);
        foreach (var pair in headlights)
            if (pair.Key != null) pair.Key.intensity = pair.Value * Mathf.Clamp01(changeTime ? tTime * 1.4f - 0.2f : 1f);

        // Déšť: mokrá silnice, menší přilnavost, kapky
        if (changeWeather)
        {
            float grip = Mathf.Lerp(startDry, endWet, tRain);
            foreach (var c in cars) if (c != null) c.gripMultiplier = grip;
            foreach (var m in asphalt) if (m != null && m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", Mathf.Lerp(0.04f, 0.72f, tRain));
            if (!rainStarted && tRain > 0.05f && weather != null)
            {
                rainStarted = true;
                weather.StartPrecipitation(snow, rainMaterial);
                foreach (var fx in FindObjectsOfType<CarEffects>()) fx.rain = true;
            }
        }
    }
}
