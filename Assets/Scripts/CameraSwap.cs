using UnityEngine;

public class CameraSwap : MonoBehaviour
{
    public Camera cameraOne;
    public Camera cameraTwo;

    public int counter;


    void Start()
    {
        ShowCameraOne();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            counter++;
            if (counter > 1)
            {
                counter = 0;
            }
        
        }

        if (counter==0)
        {
            ShowCameraTwo();
        }
        else if (counter==1)
        {
            ShowCameraOne();
        }
    }

    public void ShowCameraOne()
    {
        cameraOne.enabled = false;
        cameraTwo.enabled = true;
    }

    public void ShowCameraTwo()
    {
        cameraOne.enabled = true;
        cameraTwo.enabled = false;
    }
}


