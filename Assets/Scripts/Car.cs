using UnityEngine;

/// <summary>
/// Auto ovládané hráčem.
/// W/S nebo šipky = plyn / brzda (při stání couvání), A/D = zatáčení, mezerník = ruční brzda.
/// </summary>
public class Car : CarController
{
    protected override void Update()
    {
        base.Update();

        float vertical = Input.GetAxis("Vertical");
        float forwardSpeed = ForwardSpeedKmh;

        throttleInput = 0f;
        brakeInput = 0f;

        // Šipka dolů za jízdy vpřed brzdí, teprve po zastavení couvá (a naopak)
        if (vertical < 0f && forwardSpeed > 3f)
            brakeInput = -vertical;
        else if (vertical > 0f && forwardSpeed < -3f)
            brakeInput = vertical;
        else
            throttleInput = vertical;

        steerInput = Input.GetAxis("Horizontal");
        handbrakeInput = Input.GetKey(KeyCode.Space);
    }
}
