using UnityEngine;

/// <summary>
/// Kamera za autem: s rychlostí se rozšíří zorné pole a mírně oddálí (pocit rychlosti),
/// při nitru ještě víc; při nárazu se kamera zatřese.
/// </summary>
public class ChaseCameraFX : MonoBehaviour
{
    public CarController target;
    public float baseFov = 62f;
    public float maxFovBonus = 14f;
    public float nitroFovBonus = 6f;

    private Camera cam;
    private float shake;
    private static ChaseCameraFX instance;

    /// <summary>Zatřese kamerou, pokud sleduje dané auto.</summary>
    public static void Shake(Transform car, float amount)
    {
        if (instance != null && instance.target != null && instance.target.transform == car)
            instance.shake = Mathf.Max(instance.shake, amount);
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void LateUpdate()
    {
        if (target == null || cam == null) return;

        float speed01 = Mathf.Clamp01(target.SpeedKmh / Mathf.Max(1f, target.maxSpeedKmh));
        float fov = baseFov + maxFovBonus * speed01 * speed01 + (target.NitroActive ? nitroFovBonus : 0f);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, Time.deltaTime * 3f);

        if (shake > 0.001f)
        {
            transform.position += Random.insideUnitSphere * shake * 0.35f;
            shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 2.5f);
        }
    }
}
