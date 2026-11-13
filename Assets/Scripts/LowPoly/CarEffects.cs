using UnityEngine;

/// <summary>Když zadní kola prokluzují nebo smýkají, kreslí stopy smyku.</summary>
public class CarEffects : MonoBehaviour
{
    public float sidewaysSlipThreshold = 0.35f;
    public float forwardSlipThreshold = 0.7f;

    private CarController car;
    private WheelCollider[] wheels;
    private Vector3[] lastPoint;
    private bool[] hadMark;

    private void Start()
    {
        car = GetComponent<CarController>();
        wheels = new[] { car.wheelColliderLeftBack, car.wheelColliderRightBack, car.wheelColliderLeftFront, car.wheelColliderRightFront };
        lastPoint = new Vector3[wheels.Length];
        hadMark = new bool[wheels.Length];
    }

    private void FixedUpdate()
    {
        if (SkidMarks.Instance == null || car == null) return;

        for (int i = 0; i < wheels.Length; i++)
        {
            WheelCollider wheel = wheels[i];
            bool skid = false;
            if (wheel != null && wheel.GetGroundHit(out WheelHit hit) && car.SpeedKmh > 8f)
            {
                skid = Mathf.Abs(hit.sidewaysSlip) > sidewaysSlipThreshold || Mathf.Abs(hit.forwardSlip) > forwardSlipThreshold;
                if (skid)
                {
                    Vector3 point = hit.point;
                    if (hadMark[i] && (point - lastPoint[i]).sqrMagnitude > 0.09f)
                    {
                        SkidMarks.Instance.AddSegment(lastPoint[i], point, hit.normal);
                        lastPoint[i] = point;
                    }
                    else if (!hadMark[i])
                        lastPoint[i] = point;
                }
            }
            hadMark[i] = skid;
        }
    }
}
