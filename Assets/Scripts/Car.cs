using UnityEngine;

public class Car : MonoBehaviour
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
    public float minSpeedForEngineSound = 1f;

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.centerOfMass = centerOfMass.localPosition;
    }

    void FixedUpdate()
    {
        float movementInput = Input.GetAxis("Vertical");

        wheelColliderLeftBack.motorTorque = movementInput * motorTorque;
        wheelColliderRightBack.motorTorque = movementInput * motorTorque;
        wheelColliderLeftFront.steerAngle = Input.GetAxis("Horizontal") * maxSteer;
        wheelColliderRightFront.steerAngle = Input.GetAxis("Horizontal") * maxSteer;

        float currentSpeed = _rigidbody.velocity.magnitude;
        float pitchFromCar = _rigidbody.velocity.magnitude / 60f;
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
