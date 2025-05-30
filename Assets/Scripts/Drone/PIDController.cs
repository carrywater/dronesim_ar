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

    [Header("Rotation Compensation")]
    [SerializeField] private float _maxTiltAngle = 15f; // Maximum tilt angle in degrees
    [SerializeField] private float _rotationDamping = 0.95f; // Damping for rotation
    [SerializeField] private float _rotationSpeed = 2f; // How fast the drone rotates to compensate
    [SerializeField] private Transform _droneBody; // The main drone body to rotate

    private Vector3 _integral;
    private Vector3 _lastError;
    private Vector3 _velocity;
    private Vector3 _rotationVelocity;
    private Quaternion _targetRotation;

    private void Start()
    {
        if (_droneBody == null)
        {
            _droneBody = transform;
        }
        _targetRotation = _droneBody.rotation;
    }

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

        // Calculate rotation compensation based on error and velocity
        Vector3 rotationCompensation = CalculateRotationCompensation(error, _velocity);
        
        // Smoothly interpolate current rotation to target rotation
        _targetRotation = Quaternion.Euler(rotationCompensation);
        _droneBody.rotation = Quaternion.Slerp(
            _droneBody.rotation,
            _targetRotation,
            _rotationSpeed * Time.deltaTime
        );

        // Apply sway
        _swayTransform.localPosition += _velocity * Time.deltaTime;

        // Store last error
        _lastError = error;
    }

    private Vector3 CalculateRotationCompensation(Vector3 error, Vector3 velocity)
    {
        // Calculate tilt angles based on error and velocity
        float tiltX = Mathf.Clamp(-error.z * 2f - velocity.z, -_maxTiltAngle, _maxTiltAngle);
        float tiltZ = Mathf.Clamp(error.x * 2f + velocity.x, -_maxTiltAngle, _maxTiltAngle);
        
        // Add a small random rotation for realism
        float randomTilt = Random.Range(-0.5f, 0.5f);
        
        return new Vector3(tiltX, 0f, tiltZ + randomTilt);
    }

    /// <summary>
    /// Simulate an external influence (e.g., a strong gust) by applying a force to the velocity.
    /// The controller will naturally correct this over time.
    /// </summary>
    public void ApplyExternalInfluence(Vector3 force)
    {
        _velocity += force;
        
        // Also apply a rotation influence based on the force
        Vector3 rotationInfluence = new Vector3(
            -force.z * 0.5f, // Tilt forward/backward based on Z force
            0f,
            force.x * 0.5f  // Tilt left/right based on X force
        );
        _targetRotation *= Quaternion.Euler(rotationInfluence);
    }
} 