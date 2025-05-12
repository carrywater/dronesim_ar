using UnityEngine;

public class DroneController : MonoBehaviour
{
    // TODO: Implement drone control logic

    [Header("References")]
    [SerializeField] private DroneNavigation _navigation;       // for pathing & arrival callbacks
    [SerializeField] private DroneHMI _hmi;                     // for LED & audio feedback
    [SerializeField] private ARInterfaceManager _arInterface;   // for AR probe & pad UI

    [Header("Propellers")]
    [SerializeField] private Transform[] _propellers;  // assign 8 propellers here

    [Header("Sway Controller")]
    [SerializeField] private PIDController _pidController;  // controls subtle hover sway

    [Header("Cruise Target")]
    [SerializeField] private Transform _cruiseTarget;  // target to cruise toward
} 