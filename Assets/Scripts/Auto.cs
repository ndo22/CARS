using UnityEngine;

public class Auto : MonoBehaviour
{
    private Rigidbody _rigidbody;
    public Transform centerOfMass;

    public WheelCollider wheelColliderLeftFront;
    public WheelCollider wheelColliderRightFront;
    public WheelCollider wheelColliderLeftBack;
    public WheelCollider wheelColliderRightBack;

    public Transform LeftFront;
    public Transform RightFront;
    public Transform LeftBack;
    public Transform RightBack;

    public float motorTorque = 150f;
    public float maxSteer = 15f;

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.centerOfMass = centerOfMass.localPosition;
    }

    void FixedUpdate()
    {
        wheelColliderLeftBack.motorTorque = Input.GetAxis("Vertical") * motorTorque;
        wheelColliderRightBack.motorTorque = Input.GetAxis("Vertical") * motorTorque;
        wheelColliderLeftFront.steerAngle = Input.GetAxis("Horizontal") * maxSteer;
        wheelColliderRightFront.steerAngle = Input.GetAxis("Horizontal") * maxSteer;
    }

    void Update()
    {
        var pos = Vector3.zero;
        var rot = Quaternion.identity;

        wheelColliderLeftFront.GetWorldPose(out pos, out rot);
        LeftFront.position = pos;
        LeftFront.rotation = rot;

        wheelColliderRightFront.GetWorldPose(out pos, out rot);
        RightFront.position = pos;
        RightFront.rotation = rot * Quaternion.Euler(0, 180, 0);

        wheelColliderLeftBack.GetWorldPose(out pos, out rot);
        LeftBack.position = pos;
        LeftBack.rotation = rot;

        wheelColliderRightBack.GetWorldPose(out pos, out rot);
        RightBack.position = pos;
        RightBack.rotation = rot * Quaternion.Euler(0, 180, 0);
    }
}
