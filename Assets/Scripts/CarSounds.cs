using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarSounds : MonoBehaviour
{
    public float minSpeed;
    public float maxSpeed;
    private float _currentSpeed;

    private Rigidbody _carRb;
    private AudioSource _carAudio;

    public float minPitch;
    public float maxPitch;
    private float pitchFromCar;

    void Start()
    {
        _carAudio = GetComponent<AudioSource>();
        _carRb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        EngineSound();
    }

    void EngineSound()
    {
        _currentSpeed = _carRb.velocity.magnitude;
        pitchFromCar = _carRb.velocity.magnitude / 60f;

        if (_currentSpeed < minSpeed)
        {
            _carAudio.pitch = minPitch;
        }

        if (_currentSpeed > minSpeed && _currentSpeed < maxSpeed)
        {
            _carAudio.pitch = minPitch + pitchFromCar;
        }

        if (_currentSpeed > maxSpeed)
        {
            _carAudio.pitch = maxPitch;
        }
    }
}