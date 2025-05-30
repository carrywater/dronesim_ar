using UnityEngine;

/// <summary>
/// Utility for configuring AudioSources for 3D spatial audio.
/// Attach this script to any GameObject with an AudioSource, or call the static method from any component.
/// </summary>
public class SpatialAudioHelper : MonoBehaviour
{
    [Header("Spatial Audio Settings")]
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 30f;
    [SerializeField] private float dopplerLevel = 1f;
    [SerializeField] private float spread = 120f;
    [SerializeField] private float reverbZoneMix = 1.0f;

    /// <summary>
    /// Configure the given AudioSource for spatial audio.
    /// </summary>
    public void ConfigureSpatialAudio(AudioSource source)
    {
        if (source == null) return;
        source.spatialBlend = 1.0f; // Full 3D
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.dopplerLevel = dopplerLevel;
        source.spread = spread;
        source.reverbZoneMix = reverbZoneMix;
    }

    /// <summary>
    /// Static helper for one-off configuration.
    /// </summary>
    public static void Configure(AudioSource source, float minDist = 1f, float maxDist = 30f, float doppler = 1f, float spread = 120f, float reverb = 1f)
    {
        if (source == null) return;
        source.spatialBlend = 1.0f;
        source.minDistance = minDist;
        source.maxDistance = maxDist;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.dopplerLevel = doppler;
        source.spread = spread;
        source.reverbZoneMix = reverb;
    }
} 