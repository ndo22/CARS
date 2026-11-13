using UnityEngine;

/// <summary>
/// Auto řízené počítačem. Vstupy mu nastavuje <see cref="AIPlayer"/>.
/// </summary>
public class AICar : CarController
{
    /// <summary>-1 (couvání) .. 1 (plný plyn)</summary>
    public float Throttle { get => throttleInput; set => throttleInput = Mathf.Clamp(value, -1f, 1f); }
    /// <summary>-1 (doleva) .. 1 (doprava)</summary>
    public float Steer { get => steerInput; set => steerInput = Mathf.Clamp(value, -1f, 1f); }
    /// <summary>0 .. 1</summary>
    public float Brake { get => brakeInput; set => brakeInput = Mathf.Clamp01(value); }
    public bool UseNitro { get => nitroInput; set => nitroInput = value; }
}
