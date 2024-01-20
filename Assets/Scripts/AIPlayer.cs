using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIPlayer : MonoBehaviour
{

    public float BestLapTime { get; private set; } = Mathf.Infinity;
    public float LastLapTime { get; private set; } = 0;
    public float CurrentLapTime { get; private set; } = 0;
    public int CurrentLap { get; private set; } = 0;

    private float lapTimer;
    private int lastCheckpointPassed = 0;
    private int nextCheckpoint = 1;


    private Transform checkpointsParent;
    private int checkpointCount;
    private int checkpointLayer;

    public AIAuto carController { get; private set; }


    private float currentAngle;
    private float gasInput;

    private bool isBraking = false;



    //private void FixedUpdate()
    //{
    //    //turn
    //    ai.directionSteer = checkpointsParent.GetChild(nextCheckpoint).position - this.transform.position;
    //    ai.rotationSteer = Quaternion.LookRotation(ai.directionSteer);
    //    this.transform.rotation = Quaternion.Lerp(this.transform.rotation, ai.rotationSteer, TurnSpeed);
    //
    //    //move
    //    rb.AddRelativeForce(Vector3.forward * MoveSpeed, ForceMode.VelocityChange);
    //}



    void Update()
    {

        Vector3 dirToMove = (checkpointsParent.GetChild(nextCheckpoint).position - this.transform.position).normalized;


        float forwardAmount = 0f;
        float steerAmout = 0f;

        float dot = Vector3.Dot(transform.forward, dirToMove);

        if (dot > 0)
        {
            forwardAmount = 1f;
        }
        else 
        {
            forwardAmount = -1f;
        }

        float angleToDir = Vector3.SignedAngle(transform.forward, dirToMove, Vector3.up);

        if (angleToDir  > 0)
        {
            steerAmout = 1f;
        }
        else
        {
            steerAmout = -1f;
        }

        if (isBraking)
        {
            forwardAmount *= -1;
        }
        carController.SpeedInput = forwardAmount * 300;
        carController.SteerInput = steerAmout * 10;
    }



    void Awake()
    {
        checkpointsParent = GameObject.Find("Checkpoints").transform;
        checkpointCount = checkpointsParent.childCount;
        checkpointLayer = LayerMask.NameToLayer("Checkpoint");
        carController = GetComponent<AIAuto>();
    }

    void StartLap()
    {
        Debug.Log("Started Lap");
        CurrentLap++;
        lastCheckpointPassed = 1;
        nextCheckpoint = lastCheckpointPassed + 1;
        lapTimer = Time.time;
    }

    void EndLap()
    {
        LastLapTime = Time.time - lapTimer;
        BestLapTime = Mathf.Min(LastLapTime, BestLapTime);
        Debug.Log("Ended Lap - LapTime was " + LastLapTime + "s");
    }


    private void OnTriggerEnter(Collider collider)
    {
        if (collider.gameObject.layer != checkpointLayer)
        {
            return;
        }

        if (collider.gameObject.name == "1")
        {

            if (lastCheckpointPassed == checkpointCount)
            {
                EndLap();
                isBraking = true;
            }

            if (CurrentLap == 0 || lastCheckpointPassed == checkpointCount)
            {
                StartLap();
            }
            return;
        }

        if (collider.gameObject.name == (lastCheckpointPassed + 1).ToString())
        {
            isBraking = false;
            lastCheckpointPassed++;
            if (lastCheckpointPassed == 15)
            {
                nextCheckpoint = 1;
            }
            else
            {
                nextCheckpoint = lastCheckpointPassed + 1;
            }
        }
    }
}
