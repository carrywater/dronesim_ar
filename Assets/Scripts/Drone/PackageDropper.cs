using UnityEngine;

/// <summary>
/// Handles package drop logic for a drone. Detaches the package and enables physics when dropped.
/// </summary>
public class PackageDropper : MonoBehaviour
{
    [SerializeField] private GameObject _package; // Assign in Inspector

    /// <summary>
    /// Drops the package by detaching it and enabling physics.
    /// </summary>
    public void Drop()
    {
        if (_package == null) return;
        _package.transform.parent = null;
        var rb = _package.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    /// <summary>
    /// Optionally reattaches the package and disables physics.
    /// </summary>
    public void Attach(GameObject newParent)
    {
        _package.transform.parent = newParent.transform;
        var rb = _package.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }
} 