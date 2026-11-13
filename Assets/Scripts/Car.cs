using UnityEngine;

/// <summary>
/// Auto ovládané hráčem (klávesnice, gamepad nebo dotyk, viz <see cref="GameInput"/>).
/// Výchozí klávesy: W/S plyn a brzda (při stání couvání), A/D zatáčení, mezerník ruční brzda (drift), Shift nitro.
/// Volitelně pomáhá <see cref="DrivingAssist"/> (asistence řízení a brzdění pro začátečníky).
/// </summary>
public class Car : CarController
{
    [Tooltip("Jak rychle se kola natáčejí (vyhlazení vstupu z klávesnice).")]
    public float steerSpeed = 4f;
    [Tooltip("-1 = jeden hráč (všechna zařízení), 0/1 = hráč ve split-screenu.")]
    public int playerIndex = -1;

    private float smoothSteer;
    private DrivingAssist assist;

    protected override void Awake()
    {
        base.Awake();
        assist = GetComponent<DrivingAssist>();
    }

    protected override void Update()
    {
        base.Update();

        float gas = GameInput.Throttle(playerIndex);
        float reverse = GameInput.Brake(playerIndex);
        float steer = GameInput.Steer(playerIndex);
        float forwardSpeed = ForwardSpeedKmh;

        if (assist != null && assist.enabled)
            assist.Apply(ref gas, ref reverse, ref steer);

        throttleInput = 0f;
        brakeInput = 0f;

        // Brzda za jízdy vpřed brzdí, teprve po zastavení couvá (a plyn při couvání brzdí)
        if (reverse > 0.05f && forwardSpeed > 3f)
            brakeInput = reverse;
        else if (gas > 0.05f && forwardSpeed < -3f)
            brakeInput = gas;
        else
            throttleInput = gas - reverse;

        smoothSteer = Mathf.MoveTowards(smoothSteer, steer, steerSpeed * Time.deltaTime);
        steerInput = smoothSteer;
        handbrakeInput = GameInput.Held(GameInput.Action.Handbrake, playerIndex);
        nitroInput = GameInput.Held(GameInput.Action.Nitro, playerIndex);
    }
}
