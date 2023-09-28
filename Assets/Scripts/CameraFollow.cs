using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform Target;

    private Vector3 _startPos;
    private Quaternion _startRot;

    public Vector3 offset;
    public Vector3 eulerrotation;
    public float damper;


    void Start()
    {
        transform.eulerAngles = eulerrotation;
        _startPos = Target.position;
        _startRot = Target.rotation;
    }


    void Update()
    {
        if (Target == null)
            return;

        if (Input.GetKeyDown(KeyCode.R))
        {
            Target.position = _startPos;
            Target.rotation = _startRot;
        }

        transform.position = Vector3.Lerp(transform.position, Target.position + offset, damper * Time.deltaTime);

    }
}
