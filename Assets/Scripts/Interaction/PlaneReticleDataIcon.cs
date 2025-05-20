using UnityEngine;
using Oculus.Interaction.DistanceReticles;

public class PlaneReticleDataIcon : ReticleDataIcon
{
    [Header("Plane Collider for Reticle Clamping")]
    public Collider planeCollider;

    public new Vector3 ProcessHitPoint(Vector3 hitPoint)
    {
        if (planeCollider != null)
        {
            var bounds = planeCollider.bounds;
            hitPoint.x = Mathf.Clamp(hitPoint.x, bounds.min.x, bounds.max.x);
            hitPoint.y = Mathf.Clamp(hitPoint.y, bounds.min.y, bounds.max.y);
            hitPoint.z = Mathf.Clamp(hitPoint.z, bounds.min.z, bounds.max.z);
        }
        return Vector3.Lerp(hitPoint, this.transform.position, Snappiness);
    }
} 