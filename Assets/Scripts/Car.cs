using UnityEngine;

/// <summary>
/// Auto ovládané hráčem (klávesnice nebo gamepad, viz <see cref="GameInput"/>).
/// Výchozí klávesy: W/S plyn a brzda (při stání couvání), A/D zatáčení, mezerník ruční brzda (drift), Shift nitro.
/// </summary>
public class Car : CarController
{
    [Tooltip("Jak rychle se kola natáčejí (vyhlazení vstupu z klávesnice).")]
    public float steerSpeed = 4f;

    private float smoothSteer;

    protected override void Update()
    {
        base.Update();

        float gas = GameInput.Throttle();
        float reverse = GameInput.Brake();
        float forwardSpeed = ForwardSpeedKmh;

        throttleInput = 0f;
        brakeInput = 0f;

        // Brzda za jízdy vpřed brzdí, teprve po zastavení couvá (a plyn při couvání brzdí)
        if (reverse > 0.05f && forwardSpeed > 3f)
            brakeInput = reverse;
        else if (gas > 0.05f && forwardSpeed < -3f)
            brakeInput = gas;
        else
            throttleInput = gas - reverse;

        smoothSteer = Mathf.MoveTowards(smoothSteer, GameInput.Steer(), steerSpeed * Time.deltaTime);
        steerInput = smoothSteer;
        handbrakeInput = GameInput.Held(GameInput.Action.Handbrake);
        nitroInput = GameInput.Held(GameInput.Action.Nitro);
    }
}
