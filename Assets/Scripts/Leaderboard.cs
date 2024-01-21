using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;

public class Leaderboard : MonoBehaviour
{
    private Transform topTimesPanel;
    private Transform leaderboardTemplate;
    private List<Transform> leaderboardEntryTransformList;

    private const string leaderboardKey = "leaderboardTable";

    private void Awake()
    {
        topTimesPanel = transform.Find("TopTimesPanel");
        leaderboardTemplate = topTimesPanel.Find("LeaderboardTemplate");

        if (topTimesPanel == null)
        {
            Debug.LogError("TopTimesPanel not found!");
            return;
        }

        if (leaderboardTemplate == null)
        {
            Debug.LogError("LeaderboardTemplate not found!");
            return;
        }

        leaderboardTemplate.gameObject.SetActive(false);

        string jsonString = PlayerPrefs.GetString(leaderboardKey);
        LeaderboardList leaderboardList = JsonUtility.FromJson<LeaderboardList>(jsonString);

        leaderboardList.leaderboardEntryList = leaderboardList.leaderboardEntryList.OrderBy(entry => entry.time).ToList();

        leaderboardEntryTransformList = new List<Transform>();

        foreach (LeaderboardEntry leaderboardEntry in leaderboardList.leaderboardEntryList.Take(5))
        {
            CreateLeaderboardEntryTransform(leaderboardEntry, topTimesPanel, leaderboardEntryTransformList);
        }
    }

    private void CreateLeaderboardEntryTransform(LeaderboardEntry leaderboardEntry, Transform container, List<Transform> transformList)
    {
        float templateHeight = 30f;
        Transform entryTransform = Instantiate(leaderboardTemplate, container);
        RectTransform entryRectTransform = entryTransform.GetComponent<RectTransform>();
        entryRectTransform.anchoredPosition = new Vector2(0, -templateHeight * transformList.Count);
        entryRectTransform.gameObject.SetActive(true);

        string rankString = transformList.Count + 1 + ".";

        entryTransform.Find("PositionText").GetComponent<TextMeshProUGUI>().text = rankString;

        entryTransform.Find("TimeText").GetComponent<TextMeshProUGUI>().text = leaderboardEntry.time;

        transformList.Add(entryTransform);
    }

    public void AddLeaderboardEntry(float time)
    {
        string jsonString = PlayerPrefs.GetString(leaderboardKey);
        LeaderboardList leaderboardList = JsonUtility.FromJson<LeaderboardList>(jsonString);

        if (leaderboardList == null)
        {
            leaderboardList = new LeaderboardList();
            leaderboardList.leaderboardEntryList = new List<LeaderboardEntry>();
            Debug.Log("new leadeboard list created");
        }

        LeaderboardEntry leaderboardEntry = new LeaderboardEntry { time = $"{(int)time/ 60}:{(time) % 60:00.000}" };

        leaderboardList.leaderboardEntryList.Add(leaderboardEntry);

        string json = JsonUtility.ToJson(leaderboardList);
        PlayerPrefs.SetString(leaderboardKey, json);
        PlayerPrefs.Save();

        Awake();
    }

    private void ResetLeaderboard()
    {
        PlayerPrefs.DeleteKey(leaderboardKey);
        PlayerPrefs.Save();
        Debug.Log("Leaderboard reset!");
    }

    private class LeaderboardList
    {
        public List<LeaderboardEntry> leaderboardEntryList;
    }

    [System.Serializable]
    private class LeaderboardEntry
    {
        public string time;
    }
}
