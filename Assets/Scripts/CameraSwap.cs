using UnityEngine;

/// <summary>Klávesa C přepíná mezi kamerou shora a kamerou za autem.</summary>
public class CameraSwap : MonoBehaviour
{
    public Camera cameraOne;
    public Camera cameraTwo;

    public int counter;

    void Start()
    {
        Apply();
    }

    void Update()
    {
        if (GameInput.Pressed(GameInput.Action.Camera) && !PauseMenu.GameIsPaused)
        {
            counter = (counter + 1) % 2;
            Apply();
        }
    }

    // counter 0 = kamera jedna, 1 = kamera dvě (stejné chování jako původně)
    private void Apply()
    {
        bool first = counter == 0;
        if (cameraOne != null) cameraOne.enabled = first;
        if (cameraTwo != null) cameraTwo.enabled = !first;
    }
}
