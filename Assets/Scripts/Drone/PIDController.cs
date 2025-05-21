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
    [SerializeField] private float _damping = 0.98f; // Added for realism
    [SerializeField] private float _outputNoise = 0.001f; // Small noise for realism

    private Vector3 _integral;
    private Vector3 _lastError;
    private Vector3 _velocity;

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

        // Add small output noise for realism
        output += Random.insideUnitSphere * _outputNoise;

        // Integrate velocity (inertia)
        _velocity += output * Time.deltaTime;
        _velocity *= _damping; // Damping

        // Apply sway
        _swayTransform.localPosition += _velocity * Time.deltaTime;

        // Store last error
        _lastError = error;
    }

    /// <summary>
    /// Simulate an external influence (e.g., a strong gust) by applying a force to the velocity.
    /// The controller will naturally correct this over time.
    /// </summary>
    public void ApplyExternalInfluence(Vector3 force)
    {
        _velocity += force;
    }
} 