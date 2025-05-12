using UnityEngine;

public class PIDController : MonoBehaviour
{
    [Header("PID Gains")] 
    [SerializeField] private float _kp = 1f;
    [SerializeField] private float _ki = 0f;
    [SerializeField] private float _kd = 0f;

    [Header("Sway Settings")] 
    [SerializeField] private Transform _swayTransform;  // child transform to sway
    [SerializeField] private Vector3 _swayAmplitude = new Vector3(0.1f, 0.1f, 0.1f);
    [SerializeField] private float _swayFrequency = 1f;

    private Vector3 _integral;
    private Vector3 _lastError;

    private void Update()
    {
        // Generate a dynamic target offset using Perlin noise
        float t = Time.time * _swayFrequency;
        Vector3 targetOffset = new Vector3(
            (Mathf.PerlinNoise(t, 0f) * 2f - 1f) * _swayAmplitude.x,
            (Mathf.PerlinNoise(0f, t) * 2f - 1f) * _swayAmplitude.y,
            (Mathf.PerlinNoise(t, t) * 2f - 1f) * _swayAmplitude.z
        );

        // PID error calculation on local position
        Vector3 currentPos = _swayTransform.localPosition;
        Vector3 error = targetOffset - currentPos;

        // Integral term accumulation
        _integral += error * Time.deltaTime;

        // Derivative term
        Vector3 derivative = (error - _lastError) / Time.deltaTime;

        // PID output
        Vector3 output = _kp * error + _ki * _integral + _kd * derivative;

        // Apply sway
        _swayTransform.localPosition += output * Time.deltaTime;

        // Store last error
        _lastError = error;
    }
} 