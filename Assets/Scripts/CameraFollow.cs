using UnityEngine;

/// <summary>Kamera shora, plynule sleduje auto. (Návrat na trať klávesou R teď řeší <see cref="Player"/>.)</summary>
public class CameraFollow : MonoBehaviour
{
    public Transform Target;

    public Vector3 offset;
    public Vector3 eulerrotation;
    public float damper;

    void Start()
    {
        transform.eulerAngles = eulerrotation;
        if (Target != null)
            transform.position = Target.position + offset;
    }

    void LateUpdate()
    {
        if (Target == null)
            return;

        transform.position = Vector3.Lerp(transform.position, Target.position + offset, damper * Time.deltaTime);
    }
}
