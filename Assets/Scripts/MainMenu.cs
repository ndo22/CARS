using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void PlayGame()
    {
        SceneManager.LoadScene(1);
    }

    /// <summary>Spustí novou procedurální low-poly trať.</summary>
    public void PlayLowPoly()
    {
        SceneManager.LoadScene("LowPolyTrack");
    }


    public void QuitGame()
    {
        Debug.Log("QUIT");
        Application.Quit();
    
    }


}
