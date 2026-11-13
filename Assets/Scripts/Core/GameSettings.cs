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

    /// <summary>Poškození: 0 vypnuto, 1 jen vzhled, 2 vzhled i jízda.</summary>
    public static int Damage { get => PlayerPrefs.GetInt("damage", 2); set => SetInt("damage", value); }
    /// <summary>Asistence řízení: 0 vypnuto, 1 slabá, 2 silná.</summary>
    public static int SteerAssist { get => PlayerPrefs.GetInt("steerAssist", 0); set => SetInt("steerAssist", value); }
    public static bool BrakeAssist { get => PlayerPrefs.GetInt("brakeAssist", 0) == 1; set => SetInt("brakeAssist", value ? 1 : 0); }
    /// <summary>Velikost UI: 0 malé, 1 normální, 2 velké.</summary>
    public static int UiScale { get => PlayerPrefs.GetInt("uiScale", 1); set => SetInt("uiScale", value); }
    public static float UiScaleFactor => UiScale == 0 ? 0.85f : UiScale == 2 ? 1.25f : 1f;
    /// <summary>Dotykové ovládání: 0 automaticky (jen na mobilu), 1 zapnuto, 2 vypnuto.</summary>
    public static int TouchControls { get => PlayerPrefs.GetInt("touch", 0); set => SetInt("touch", value); }
    public static bool UseTouch => TouchControls == 1 || (TouchControls == 0 && Application.isMobilePlatform);
    public static bool Tilt { get => PlayerPrefs.GetInt("tilt", 0) == 1; set => SetInt("tilt", value ? 1 : 0); }
    public static bool TutorialDone { get => PlayerPrefs.GetInt("tutorialDone", 0) == 1; set => SetInt("tutorialDone", value ? 1 : 0); }
    /// <summary>Vysoký kontrast HUD (tmavší podklady, větší rychloměr).</summary>
    public static bool HighContrast { get => PlayerPrefs.GetInt("highContrast", 0) == 1; set => SetInt("highContrast", value ? 1 : 0); }

    private static void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
    }

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
        else if (Application.isMobilePlatform)
            QualitySettings.SetQualityLevel(Mathf.Min(1, QualitySettings.names.Length - 1), true); // mobil: nižší kvalita
    }
}
