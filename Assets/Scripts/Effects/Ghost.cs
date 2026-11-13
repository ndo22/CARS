using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Ghost nejlepšího kola: <see cref="GhostRecorder"/> nahrává jízdu hráče, nejlepší kolo uloží do souboru
/// a <see cref="GhostPlayer"/> ho přehrává jako průhledné auto (bez fyziky).
/// </summary>
public static class GhostData
{
    public const float SampleInterval = 0.05f;

    public class Lap
    {
        public float time;
        public int style;
        public readonly List<Vector3> positions = new List<Vector3>();
        public readonly List<Quaternion> rotations = new List<Quaternion>();
    }

    private static string PathFor(string trackId) => System.IO.Path.Combine(Application.persistentDataPath, "ghost_" + trackId + ".bin");

    public static void Save(string trackId, Lap lap)
    {
        try
        {
            using (var w = new BinaryWriter(File.Open(PathFor(trackId), FileMode.Create)))
            {
                w.Write(1); // verze
                w.Write(lap.time);
                w.Write(lap.style);
                w.Write(lap.positions.Count);
                for (int i = 0; i < lap.positions.Count; i++)
                {
                    Vector3 p = lap.positions[i];
                    Quaternion q = lap.rotations[i];
                    w.Write(p.x); w.Write(p.y); w.Write(p.z);
                    w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Ghost se nepodařilo uložit: " + e.Message);
        }
    }

    public static Lap Load(string trackId)
    {
        string path = PathFor(trackId);
        if (!File.Exists(path)) return null;
        try
        {
            using (var r = new BinaryReader(File.OpenRead(path)))
            {
                if (r.ReadInt32() != 1) return null;
                var lap = new Lap { time = r.ReadSingle(), style = r.ReadInt32() };
                int count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    lap.positions.Add(new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()));
                    lap.rotations.Add(new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()));
                }
                return lap;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Ghost se nepodařilo načíst: " + e.Message);
            return null;
        }
    }
}

/// <summary>Nahrává polohu auta během kola; když je kolo nejrychlejší, uloží ho jako ghost.</summary>
public class GhostRecorder : MonoBehaviour
{
    public string trackId;
    public int style;
    /// <summary>Vyvolá se, když vznikne nový nejlepší ghost.</summary>
    public event Action<GhostData.Lap> NewBest;

    private RacerBase racer;
    private GhostData.Lap current;
    private float timer;
    private float bestTime = float.MaxValue;

    private void Start()
    {
        racer = GetComponent<RacerBase>();
        GhostData.Lap saved = GhostData.Load(trackId);
        if (saved != null) bestTime = saved.time;
        racer.LapStarted += () =>
        {
            current = new GhostData.Lap { style = style };
            timer = 0f;
            Sample();
        };
        racer.LapCompleted += time =>
        {
            if (current == null || time >= bestTime || current.positions.Count < 10) return;
            current.time = time;
            bestTime = time;
            GhostData.Save(trackId, current);
            NewBest?.Invoke(current);
        };
    }

    private void FixedUpdate()
    {
        if (current == null) return;
        timer += Time.fixedDeltaTime;
        if (timer >= GhostData.SampleInterval)
        {
            timer -= GhostData.SampleInterval;
            Sample();
        }
    }

    private void Sample()
    {
        current.positions.Add(transform.position);
        current.rotations.Add(transform.rotation);
    }
}

/// <summary>Přehrává uložené kolo. Začne znovu pokaždé, když hráč projede startem.</summary>
public class GhostPlayer : MonoBehaviour
{
    public GhostData.Lap lap;
    private float time = -1f;

    public void Restart() => time = 0f;

    private void Update()
    {
        if (lap == null || lap.positions.Count < 2 || time < 0f) return;
        time += Time.deltaTime;
        float f = time / GhostData.SampleInterval;
        int i = Mathf.FloorToInt(f);
        if (i >= lap.positions.Count - 1)
        {
            gameObject.SetActive(lap.positions.Count > 0);
            transform.SetPositionAndRotation(lap.positions[lap.positions.Count - 1], lap.rotations[lap.rotations.Count - 1]);
            return;
        }
        float t = f - i;
        transform.SetPositionAndRotation(Vector3.Lerp(lap.positions[i], lap.positions[i + 1], t),
            Quaternion.Slerp(lap.rotations[i], lap.rotations[i + 1], t));
    }
}
