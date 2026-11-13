using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Záznam celého závodu (všechna auta) a jeho přehrání po cíli:
/// TV kamery u trati, kamera za vedoucím autem, kroužící kamera; zpomalení, zrychlení, pauza.
/// Klávesy: 1/2/3 kamera, mezerník pauza, šipky rychlost a výběr auta, Esc konec.
/// </summary>
public class ReplaySystem : MonoBehaviour
{
    private const float Interval = 0.05f;

    private class Track
    {
        public CarController car;
        public string name;
        public readonly List<Vector3> pos = new List<Vector3>();
        public readonly List<Quaternion> rot = new List<Quaternion>();
    }

    private readonly List<Track> tracks = new List<Track>();
    private float timer;
    private bool recording = true;
    private bool playing;
    private float time;
    private float speed = 1f;
    private bool paused;
    private int focus;
    private int cameraMode;
    private Camera cam;
    private Text label;
    private Canvas canvas;
    private Vector3[] tvSpots;
    private System.Action onExit;

    public bool Playing => playing;

    public void Add(CarController car, string displayName)
    {
        tracks.Add(new Track { car = car, name = displayName });
    }

    /// <summary>Místa pro TV kamery - podél trati, kousek za svodidly.</summary>
    public void SetTvSpots(Vector3[] spots) => tvSpots = spots;

    private void FixedUpdate()
    {
        if (!recording) return;
        timer += Time.fixedDeltaTime;
        if (timer < Interval) return;
        timer -= Interval;
        foreach (var t in tracks)
        {
            if (t.car == null) continue;
            t.pos.Add(t.car.transform.position);
            t.rot.Add(t.car.transform.rotation);
        }
    }

    private int Frames => tracks.Count > 0 ? tracks[0].pos.Count : 0;

    public void Play(System.Action exit)
    {
        if (Frames < 2) return;
        onExit = exit;
        recording = false;
        playing = true;
        time = 0f;
        speed = 1f;
        paused = false;

        // Auta převezme záznam: vypnout fyziku a řízení
        foreach (var t in tracks)
        {
            if (t.car == null) continue;
            t.car.Body.isKinematic = true;
            foreach (var mb in t.car.GetComponents<MonoBehaviour>())
                if (mb is RacerBase || mb is CarEffects || mb is DrivingAssist) mb.enabled = false;
        }

        foreach (var c in FindObjectsOfType<Camera>()) c.enabled = false;
        var camGo = new GameObject("ReplayCamera");
        cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.farClipPlane = 1600f;
        cam.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        cam.backgroundColor = RenderSettings.fogColor;
        var fx = camGo.AddComponent<LowPolyPostFX>();
        var other = FindObjectOfType<LowPolyPostFX>();
        if (other != null) fx.shader = other.shader;

        canvas = LowPolyUI.CreateCanvas("ReplayCanvas", 50);
        label = LowPolyUI.Label(canvas.transform, "", 26, TextAnchor.LowerCenter, Color.white, "ReplayLabel", true);
        LowPolyUI.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1600f, 80f));
        Text rec = LowPolyUI.Label(canvas.transform, "REPLAY", 48, TextAnchor.UpperLeft, new Color(1f, 0.3f, 0.25f), "Rec");
        LowPolyUI.Place(rec.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(400f, 60f));
        Image exit2 = LowPolyUI.Button(canvas.transform, "EXIT", 26, Stop);
        LowPolyUI.Place((RectTransform)exit2.transform, new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(180f, 60f));
    }

    public void Stop()
    {
        playing = false;
        if (cam != null) Destroy(cam.gameObject);
        if (canvas != null) Destroy(canvas.gameObject);
        foreach (var c in FindObjectsOfType<Camera>()) c.enabled = true;
        onExit?.Invoke();
    }

    private void Update()
    {
        if (!playing) return;
        float dt = Time.unscaledDeltaTime;

        if (Input.GetKeyDown(KeyCode.Alpha1)) cameraMode = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2)) cameraMode = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) cameraMode = 2;
        if (Input.GetKeyDown(KeyCode.Space)) paused = !paused;
        if (Input.GetKeyDown(KeyCode.RightArrow)) speed = Mathf.Min(4f, speed * 2f);
        if (Input.GetKeyDown(KeyCode.LeftArrow)) speed = Mathf.Max(0.25f, speed * 0.5f);
        if (Input.GetKeyDown(KeyCode.UpArrow)) focus = (focus + 1) % tracks.Count;
        if (Input.GetKeyDown(KeyCode.DownArrow)) focus = (focus + tracks.Count - 1) % tracks.Count;
        if (Input.GetKeyDown(KeyCode.Escape)) { Stop(); return; }

        if (!paused) time += dt * speed;
        float duration = (Frames - 1) * Interval;
        if (time > duration) time = 0f;

        float f = time / Interval;
        int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, Frames - 2);
        float k = f - i;
        foreach (var t in tracks)
        {
            if (t.car == null || t.pos.Count <= i + 1) continue;
            t.car.transform.SetPositionAndRotation(Vector3.Lerp(t.pos[i], t.pos[i + 1], k), Quaternion.Slerp(t.rot[i], t.rot[i + 1], k));
        }

        UpdateCamera(dt);
        string[] modes = { "TV", "CHASE", "ORBIT" };
        string who = tracks[focus].name;
        label.text = $"{TimeFormat.FormatRacing(time)}   |   {who}   |   CAMERA {modes[cameraMode]}   |   {speed:0.##}X{(paused ? "   PAUSED" : "")}\n" +
                     "1 2 3 CAMERA     SPACE PAUSE     LEFT RIGHT SPEED     UP DOWN CAR     ESC EXIT";
        label.text = label.text.Replace(',', '.');
    }

    private void UpdateCamera(float dt)
    {
        Transform target = tracks[focus].car != null ? tracks[focus].car.transform : null;
        if (target == null || cam == null) return;
        Vector3 look = target.position + Vector3.up * 0.8f;
        switch (cameraMode)
        {
            case 0: // TV: nejbližší kamera u trati
            {
                Vector3 best = target.position + Vector3.up * 8f;
                float bestD = float.MaxValue;
                if (tvSpots != null)
                    foreach (var s in tvSpots)
                    {
                        float d = (s - target.position).sqrMagnitude;
                        if (d < bestD) { bestD = d; best = s; }
                    }
                cam.transform.position = best;
                cam.transform.LookAt(look);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Mathf.Clamp(2400f / Mathf.Max(10f, Mathf.Sqrt(bestD)), 12f, 55f), dt * 3f);
                break;
            }
            case 1: // za autem
            {
                Vector3 wanted = target.TransformPoint(0f, 2.6f, -7f);
                cam.transform.position = Vector3.Lerp(cam.transform.position, wanted, dt * 5f);
                cam.transform.LookAt(look);
                cam.fieldOfView = 60f;
                break;
            }
            default: // kolem auta
            {
                float a = Time.unscaledTime * 0.4f;
                cam.transform.position = look + new Vector3(Mathf.Sin(a) * 9f, 2.5f, Mathf.Cos(a) * 9f);
                cam.transform.LookAt(look);
                cam.fieldOfView = 55f;
                break;
            }
        }
    }
}
