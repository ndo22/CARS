using UnityEngine;

/// <summary>
/// Hráč. Klávesa R vrátí auto na trať k poslednímu checkpointu.
/// </summary>
public class Player : RacerBase
{
    public Car carController { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        carController = GetComponent<Car>();
    }

    protected override void Update()
    {
        base.Update();

        if (Input.GetKeyDown(KeyCode.R) && !PauseMenu.GameIsPaused)
            Respawn();
    }
}
