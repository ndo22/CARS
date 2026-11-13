using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;

    public GameObject pauseMenuUI;

    void Start()
    {
        // Pojistka pro případ, že se scéna načetla znovu během pauzy
        Resume();
    }

    void Update()
    {
        if (GameInput.Pressed(GameInput.Action.Pause))
        {
            if (GameIsPaused)
                Resume();
            else
                Pause();
        }
    }

    public void Resume()
    {
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        GameIsPaused = false;
    }

    void Pause()
    {
        if (pauseMenuUI != null) pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        GameIsPaused = true;
    }

    /// <summary>Spustí závod znovu od začátku. Dá se napojit na tlačítko v pauzovacím menu.</summary>
    public void Restart()
    {
        Resume();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadMenu()
    {
        Resume();
        RaceSetup.configured = false;
        RaceSetup.screen = RaceSetup.Screen.Title;
        SceneManager.LoadScene(0);
    }

    public void Quit()
    {
        Debug.Log("QUIT");
        Application.Quit();
    }
}
