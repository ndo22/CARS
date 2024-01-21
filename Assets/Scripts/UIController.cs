using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    public GameObject UIRacePanel;

    public Text UITextCurrentLap;
    public Text UITextCurrentTime;
    public Text UITextLastLap;
    public Text UITextBestLap;
    public Text UITextAIBestLap;

    public Player UpdateUIForPlayer;
    public AIPlayer UpdateUIForAIPlayer;

    private int currentLap = -1;
    private float currentTime;
    private float lastLapTime;
    private float bestLapTime;
    private float bestAILapTime;

    void Update()
    {
        if(UpdateUIForPlayer == null && UpdateUIForAIPlayer == null)
        {
            return;
        }

        if(UpdateUIForPlayer.CurrentLap != currentLap)
        {
            currentLap = UpdateUIForPlayer.CurrentLap;
            UITextCurrentLap.text = $"Lap: {currentLap}";
        }

        if (UpdateUIForPlayer.CurrentLapTime != currentTime)
        {
            currentTime = UpdateUIForPlayer.CurrentLapTime;
            UITextCurrentTime.text = $"Time: {(int)currentTime / 60}:{(currentTime) % 60:00.000}";
        }

        if (UpdateUIForPlayer.LastLapTime != lastLapTime)
        {
            lastLapTime = UpdateUIForPlayer.LastLapTime;        
            UITextLastLap.text = $"Last: {(int)lastLapTime / 60}:{(lastLapTime) % 60:00.000}";
        }

        if (UpdateUIForPlayer.BestLapTime != bestLapTime)
        {
            bestLapTime = UpdateUIForPlayer.BestLapTime;
            UITextBestLap.text = bestLapTime < 1000000 ? $"Best: {(int)bestLapTime / 60}:{(bestLapTime) % 60:00.000}" : "Best: NONE";
        }

        if (UpdateUIForAIPlayer.BestLapTime != bestAILapTime)
        {
            bestAILapTime = UpdateUIForAIPlayer.BestLapTime;
            UITextAIBestLap.text = bestAILapTime < 1000000 ? $"CPU Best: {(int)bestAILapTime / 60}:{(bestAILapTime) % 60:00.000}" : "CPU Best: NONE";
        }
    }
}
