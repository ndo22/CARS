using UnityEngine;

/// <summary>Kamera, která pomalu krouží kolem cíle (náhled auta na startovní obrazovce).</summary>
public class OrbitCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 10.5f;
    public float height = 3.2f;
    public float degreesPerSecond = 12f;
    public Vector3 lookOffset = new Vector3(0f, 0.8f, 0f);
    [Tooltip("Posun cíle do strany, aby auto nebylo schované za menu.")]
    public float screenOffset = 0f;

    private float angle = 35f;

    private void LateUpdate()
    {
        if (target == null) return;
        angle += degreesPerSecond * Time.unscaledDeltaTime;
        Quaternion rot = Quaternion.Euler(0f, angle, 0f);
        Vector3 pivot = target.position + lookOffset;
        transform.position = pivot + rot * new Vector3(0f, height, -distance);
        transform.LookAt(pivot, Vector3.up);
        transform.position += transform.right * screenOffset;
    }
}
