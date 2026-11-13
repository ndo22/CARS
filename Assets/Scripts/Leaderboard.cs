using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// Tabulka nejlepších časů kol uložená v PlayerPrefs. Zobrazuje se v pauzovacím menu.
/// </summary>
public class Leaderboard : MonoBehaviour
{
    [Tooltip("Klíč v PlayerPrefs - každá trať může mít vlastní tabulku.")]
    public string storageKey = "leaderboardTable";
    [Tooltip("Zobrazit časy jako 1'05\"123 místo 1:05.123 (pro písma bez dvojtečky).")]
    public bool racingTimeFormat;

    private const int ShownEntries = 5;
    private const int StoredEntries = 20;
    private const float TemplateHeight = 30f;

    private Transform topTimesPanel;
    private Transform leaderboardTemplate;
    private readonly List<Transform> entryTransforms = new List<Transform>();

    private void Awake()
    {
        topTimesPanel = transform.Find("TopTimesPanel");
        if (topTimesPanel == null)
        {
            Debug.LogError("TopTimesPanel not found!");
            return;
        }

        leaderboardTemplate = topTimesPanel.Find("LeaderboardTemplate");
        if (leaderboardTemplate == null)
        {
            Debug.LogError("LeaderboardTemplate not found!");
            return;
        }

        leaderboardTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        Refresh();
    }

    /// <summary>Znovu vykreslí tabulku (staré řádky smaže, takže se nic nezdvojí).</summary>
    public void Refresh()
    {
        if (topTimesPanel == null || leaderboardTemplate == null)
            return;

        foreach (Transform t in entryTransforms)
        {
            if (t != null) Destroy(t.gameObject);
        }
        entryTransforms.Clear();

        foreach (LeaderboardEntry entry in Load().entries.Take(ShownEntries))
            CreateEntryTransform(entry);
    }

    private void CreateEntryTransform(LeaderboardEntry entry)
    {
        Transform entryTransform = Instantiate(leaderboardTemplate, topTimesPanel);
        RectTransform rect = entryTransform.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(0, -TemplateHeight * entryTransforms.Count);
        entryTransform.gameObject.SetActive(true);

        SetText(entryTransform, "PositionText", $"{entryTransforms.Count + 1}.");
        SetText(entryTransform, "TimeText", racingTimeFormat ? TimeFormat.FormatRacing(entry.seconds) : TimeFormat.Format(entry.seconds));

        entryTransforms.Add(entryTransform);
    }

    private static void SetText(Transform parent, string childName, string value)
    {
        Transform child = parent.Find(childName);
        if (child == null) return;
        var text = child.GetComponent<TextMeshProUGUI>();
        if (text != null) text.text = value;
    }

    public void AddLeaderboardEntry(float time)
    {
        if (time <= 0f || float.IsInfinity(time))
            return;

        LeaderboardList list = Load();
        list.entries.Add(new LeaderboardEntry { seconds = time, time = TimeFormat.Format(time) });
        Save(list);

        if (isActiveAndEnabled)
            Refresh();
    }

    /// <summary>Nejlepší uložený čas pro daný klíč (Infinity, pokud žádný není).</summary>
    public static float BestTime(string key)
    {
        LeaderboardList list = Load(key);
        return list.entries.Count > 0 ? list.entries[0].seconds : Mathf.Infinity;
    }

    /// <summary>Smaže všechny uložené časy. Dá se napojit na tlačítko v inspektoru.</summary>
    public void ResetLeaderboard()
    {
        PlayerPrefs.DeleteKey(storageKey);
        PlayerPrefs.Save();
        Refresh();
    }

    private LeaderboardList Load() => Load(storageKey);

    private static LeaderboardList Load(string key)
    {
        LeaderboardList list = null;
        string json = PlayerPrefs.GetString(key, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            try { list = JsonUtility.FromJson<LeaderboardList>(json); }
            catch (Exception e) { Debug.LogWarning("Leaderboard data are corrupted, starting fresh: " + e.Message); }
        }

        if (list == null) list = new LeaderboardList();
        if (list.entries == null) list.entries = new List<LeaderboardEntry>();

        // Převod ze staré verze uložení, kde byl čas jen jako text
        if (list.leaderboardEntryList != null && list.leaderboardEntryList.Count > 0)
        {
            list.entries.AddRange(list.leaderboardEntryList);
            list.leaderboardEntryList = null;
        }
        foreach (var e in list.entries)
        {
            if (e.seconds <= 0f && TimeFormat.TryParse(e.time, out float parsed))
                e.seconds = parsed;
        }

        list.entries = list.entries.Where(e => e.seconds > 0f).OrderBy(e => e.seconds).ToList();
        return list;
    }

    private void Save(LeaderboardList list)
    {
        list.entries = list.entries.OrderBy(e => e.seconds).Take(StoredEntries).ToList();
        PlayerPrefs.SetString(storageKey, JsonUtility.ToJson(list));
        PlayerPrefs.Save();
    }

    [Serializable]
    private class LeaderboardList
    {
        public List<LeaderboardEntry> entries;
        // Pouze kvůli načtení starých uložených dat
        public List<LeaderboardEntry> leaderboardEntryList;
    }

    [Serializable]
    private class LeaderboardEntry
    {
        public float seconds;
        public string time;
    }
}
