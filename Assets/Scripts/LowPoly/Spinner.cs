using UnityEngine;

/// <summary>Otáčí objektem kolem jeho lokální osy (lopatky větrného mlýna, světlo majáku).</summary>
public class Spinner : MonoBehaviour
{
    public Vector3 axis = Vector3.forward;
    public float degreesPerSecond = 40f;

    private void Update()
    {
        transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
