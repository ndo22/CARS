using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pouliční lampy: rozsvítí jen několik světel nejblíž kameře (výkon) a jejich jas řídí podle toho,
/// jak je tma (<see cref="Darkness"/> - mění se při plynulém stmívání).
/// </summary>
public class LampCuller : MonoBehaviour
{
    public static float Darkness = 1f;
    public int maxActive = 10;
    public float baseIntensity = 1.5f;

    private Light[] lights;
    private float timer;
    private readonly List<KeyValuePair<float, Light>> sorted = new List<KeyValuePair<float, Light>>();

    private void Start()
    {
        lights = GetComponentsInChildren<Light>(true);
    }

    private void Update()
    {
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f || lights == null) return;
        timer = 0.25f;

        Camera cam = Camera.main;
        Vector3 p = cam != null ? cam.transform.position : Vector3.zero;
        sorted.Clear();
        foreach (var l in lights)
            if (l != null) sorted.Add(new KeyValuePair<float, Light>((l.transform.position - p).sqrMagnitude, l));
        sorted.Sort((a, b) => a.Key.CompareTo(b.Key));

        bool on = Darkness > 0.35f;
        for (int i = 0; i < sorted.Count; i++)
        {
            Light l = sorted[i].Value;
            l.enabled = on && i < maxActive;
            l.intensity = baseIntensity * Mathf.Clamp01((Darkness - 0.35f) / 0.4f);
        }
    }
}
