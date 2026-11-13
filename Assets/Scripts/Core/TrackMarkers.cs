using System.Collections.Generic;
using UnityEngine;

/// <summary>Checkpoint s číslem (1 = start/cíl). Generované tratě ho přidávají, staré scény používají jméno objektu.</summary>
public class Checkpoint : MonoBehaviour
{
    public int number;

    /// <summary>Číslo checkpointu - z komponenty, nebo z názvu objektu (původní scéna).</summary>
    public static int NumberOf(GameObject go)
    {
        var cp = go.GetComponent<Checkpoint>();
        if (cp != null) return cp.number;
        return int.TryParse(go.name, out int n) ? n : -1;
    }
}

/// <summary>
/// Sada checkpointů aktuální tratě. Generátor ji registruje, takže závodníci nemusí hledat objekt podle jména
/// (a nenajdou omylem checkpointy staré tratě, která se zrovna maže).
/// </summary>
public class CheckpointSet : MonoBehaviour
{
    public static CheckpointSet Active { get; private set; }

    public Transform[] checkpoints = new Transform[0];

    private void Awake() { Active = this; }
    private void OnEnable() { Active = this; }

    private void OnDestroy()
    {
        if (Active == this) Active = null;
    }

    public void Register(Transform[] points)
    {
        checkpoints = points;
        Active = this;
    }

    /// <summary>Checkpointy aktuální tratě: registrovaná sada, jinak objekt "Checkpoints" (původní scéna).</summary>
    public static Transform[] Find()
    {
        if (Active != null && Active.checkpoints.Length > 0)
            return Active.checkpoints;

        var parent = GameObject.Find("Checkpoints");
        if (parent == null) return new Transform[0];
        var list = new List<Transform>();
        foreach (Transform child in parent.transform)
            if (Checkpoint.NumberOf(child.gameObject) > 0) list.Add(child);
        list.Sort((a, b) => Checkpoint.NumberOf(a.gameObject).CompareTo(Checkpoint.NumberOf(b.gameObject)));
        return list.ToArray();
    }
}

/// <summary>Typ povrchu pod koly (silnice / tráva / písek) - podle něj se kreslí prach a auto zpomaluje.</summary>
public class SurfaceType : MonoBehaviour
{
    public enum Kind { Road, Grass }
    public Kind kind;

    private static readonly Dictionary<Collider, Kind> cache = new Dictionary<Collider, Kind>();

    public static Kind Of(Collider collider)
    {
        if (collider == null) return Kind.Road;
        if (cache.TryGetValue(collider, out Kind k)) return k;
        var s = collider.GetComponent<SurfaceType>();
        k = s != null ? s.kind : Kind.Road;
        if (cache.Count > 256) cache.Clear();
        cache[collider] = k;
        return k;
    }
}
