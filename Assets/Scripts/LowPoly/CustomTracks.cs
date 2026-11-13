using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Vlastní tratě z editoru, uložené jako JSON v PlayerPrefs.</summary>
public static class CustomTracks
{
    public const int MaxTracks = 8;
    private const string Key = "customTracks";

    [Serializable]
    public class Track
    {
        public int id;
        public string name;
        public int theme;
        public Vector3[] points;
    }

    [Serializable]
    private class Store
    {
        public List<Track> tracks = new List<Track>();
    }

    private static Store store;

    public static List<Track> All
    {
        get
        {
            if (store == null) Load();
            return store.tracks;
        }
    }

    private static void Load()
    {
        string json = PlayerPrefs.GetString(Key, "");
        try { store = string.IsNullOrEmpty(json) ? new Store() : JsonUtility.FromJson<Store>(json); }
        catch (Exception) { store = null; }
        if (store == null) store = new Store();
        if (store.tracks == null) store.tracks = new List<Track>();
        store.tracks.RemoveAll(t => t == null || t.points == null || t.points.Length < 4);
    }

    public static void Save()
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(store ?? new Store()));
        PlayerPrefs.Save();
    }

    /// <summary>Uloží trať (nová nebo úprava podle id). Body se posunou tak, aby start byl v počátku.</summary>
    public static int Put(Track track)
    {
        var list = All;
        track.points = Normalize(track.points);
        int existing = list.FindIndex(t => t.id == track.id);
        if (existing >= 0) list[existing] = track;
        else
        {
            if (list.Count >= MaxTracks) list.RemoveAt(0);
            list.Add(track);
            existing = list.Count - 1;
        }
        Save();
        return existing;
    }

    public static void Remove(int id)
    {
        All.RemoveAll(t => t.id == id);
        Save();
    }

    /// <summary>Start je u prvního bodu: posunout trať tak, aby první bod ležel v počátku (generátor začíná u počátku).</summary>
    public static Vector3[] Normalize(Vector3[] points)
    {
        var result = new Vector3[points.Length];
        Vector3 first = new Vector3(points[0].x, 0f, points[0].z);
        for (int i = 0; i < points.Length; i++)
            result[i] = points[i] - first;
        return result;
    }

    /// <summary>Výchozí ovál pro novou trať.</summary>
    public static Vector3[] DefaultPoints()
    {
        return new[]
        {
            new Vector3(0, 0, 0), new Vector3(0, 0, 120), new Vector3(40, 1, 180), new Vector3(120, 2, 190),
            new Vector3(190, 2, 140), new Vector3(200, 1, 40), new Vector3(170, 0, -40), new Vector3(90, 0, -70), new Vector3(20, 0, -60),
        };
    }
}
