using UnityEngine;
using UnityEngine.UI;

/// <summary>Posuvník hlasitosti v nastavení. Hlasitost se ukládá a použije hned po spuštění.</summary>
public class SoundController : MonoBehaviour
{
    private const string VolumeKey = "musicVolume";

    [SerializeField] Slider volumeSlider;

    void Start()
    {
        Load();
    }

    public void ChangeVolume()
    {
        AudioListener.volume = volumeSlider.value;
        Save();
    }

    private void Load()
    {
        float volume = PlayerPrefs.GetFloat(VolumeKey, 0.5f);
        AudioListener.volume = volume;
        if (volumeSlider != null)
            volumeSlider.SetValueWithoutNotify(volume);
    }

    private void Save()
    {
        PlayerPrefs.SetFloat(VolumeKey, volumeSlider.value);
        PlayerPrefs.Save();
    }

    // Hlasitost platí i při spuštění rovnou závodní scény (bez hlavního menu)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedVolume()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 0.5f);
    }
}
