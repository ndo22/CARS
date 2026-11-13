using UnityEngine;

/// <summary>Kamera za autem.</summary>
public class CameraFollow2 : MonoBehaviour
{
	public Transform target;

	public float distance = 2.0f;
	public float height = 3.0f;

	public float damping = 5.0f;
	public bool smoothRotation = true;
	public float rotationDamping = 10.0f;

	void LateUpdate()
	{
		if (target == null)
			return;

		Vector3 wantedPosition = target.TransformPoint(0, height, -distance);
		transform.position = Vector3.Lerp(transform.position, wantedPosition, Time.deltaTime * damping);

		if (smoothRotation)
		{
			Quaternion wantedRotation = Quaternion.LookRotation(target.position - transform.position, Vector3.up);
			transform.rotation = Quaternion.Slerp(transform.rotation, wantedRotation, Time.deltaTime * rotationDamping);
		}
		else
			transform.LookAt(target, Vector3.up);
	}
}
