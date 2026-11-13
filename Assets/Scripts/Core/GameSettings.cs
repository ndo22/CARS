using UnityEngine;

/// <summary>Uživatelská nastavení (hlasitosti, kvalita grafiky, efekty) uložená v PlayerPrefs.</summary>
public static class GameSettings
{
    // "musicVolume" je původní klíč posuvníku v hlavním menu - ovládá celkovou hlasitost
    public static float Master { get => PlayerPrefs.GetFloat("musicVolume", 0.5f); set => SetFloat("musicVolume", value); }
    public static float Music { get => PlayerPrefs.GetFloat("musicLevel", 0.6f); set => SetFloat("musicLevel", value); }
    public static float Sfx { get => PlayerPrefs.GetFloat("sfxLevel", 0.8f); set => SetFloat("sfxLevel", value); }
    public static bool PostFx { get => PlayerPrefs.GetInt("postFx", 1) == 1; set { PlayerPrefs.SetInt("postFx", value ? 1 : 0); PlayerPrefs.Save(); } }
    public static bool ShowGhost { get => PlayerPrefs.GetInt("ghost", 1) == 1; set { PlayerPrefs.SetInt("ghost", value ? 1 : 0); PlayerPrefs.Save(); } }

    public static int Quality
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("quality", QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
        set
        {
            PlayerPrefs.SetInt("quality", value);
            PlayerPrefs.Save();
            QualitySettings.SetQualityLevel(value, true);
        }
    }

    private static void SetFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));
        PlayerPrefs.Save();
        Apply();
    }

    public static void Apply()
    {
        AudioListener.volume = Master;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        Apply();
        if (PlayerPrefs.HasKey("quality"))
            QualitySettings.SetQualityLevel(Quality, true);
    }
}
