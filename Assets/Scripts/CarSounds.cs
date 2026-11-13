using UnityEngine;

/// <summary>
/// Zvuk motoru: výška tónu podle rychlosti, s jednoduchou simulací řazení.
/// Při pauze se zvuk motoru zastaví.
/// </summary>
public class CarSounds : MonoBehaviour
{
    public float minSpeed;
    public float maxSpeed;

    public float minPitch;
    public float maxPitch;

    [Tooltip("Počet rychlostních stupňů pro simulaci řazení (0 = bez řazení).")]
    public int gears = 5;

    private Rigidbody _carRb;
    private AudioSource _carAudio;
    private bool _pausedByMenu;

    void Start()
    {
        _carAudio = GetComponent<AudioSource>();
        _carRb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (_carAudio == null || _carRb == null)
            return;

        HandlePause();
        EngineSound();
    }

    void HandlePause()
    {
        if (PauseMenu.GameIsPaused && !_pausedByMenu)
        {
            _carAudio.Pause();
            _pausedByMenu = true;
        }
        else if (!PauseMenu.GameIsPaused && _pausedByMenu)
        {
            _carAudio.UnPause();
            _pausedByMenu = false;
        }
    }

    void EngineSound()
    {
        // Pojistka: ve scéně bylo maxPitch = 0, což při vysoké rychlosti motor úplně "umlčelo"
        float topPitch = maxPitch > minPitch ? maxPitch : minPitch + maxSpeed / 60f;

        float speed = _carRb.velocity.magnitude;
        float t = Mathf.InverseLerp(minSpeed, maxSpeed, speed);

        if (gears > 1)
        {
            // V každém stupni otáčky stoupají, po přeřazení klesnou
            float gearT = t * gears;
            int gear = Mathf.Min(Mathf.FloorToInt(gearT), gears - 1);
            float inGear = gearT - gear;
            t = Mathf.Lerp(t, 0.35f + 0.65f * inGear, 0.5f);
        }

        float targetPitch = Mathf.Lerp(minPitch, topPitch, t);
        _carAudio.pitch = Mathf.Lerp(_carAudio.pitch, targetPitch, Time.deltaTime * 10f);
    }
}
