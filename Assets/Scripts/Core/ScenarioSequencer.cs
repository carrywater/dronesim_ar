using UnityEngine;

/// <summary>
/// Provides Latin-square scenario ordering for counterbalancing
/// </summary>
public class ScenarioSequencer : MonoBehaviour
{
    [Tooltip("Participant index (0-2) for Latin square counterbalancing")]
    [Range(0, 2)]
    [SerializeField] private int _participantIndex = 0;
    
    // Internal step counter to track which scenario to return next
    private int _step = 0;
    
    // Latin square design for 3 scenarios with perfect counterbalancing
    // Each row represents a different participant ordering
    private static readonly ScenarioManager.ScenarioType[,] _latinSquare = {
        // Participant 0 sees: C0 -> C1 -> C2
        { ScenarioManager.ScenarioType.C0_Abort, ScenarioManager.ScenarioType.C1_Confirm, ScenarioManager.ScenarioType.C2_Guidance },
        
        // Participant 1 sees: C1 -> C2 -> C0
        { ScenarioManager.ScenarioType.C1_Confirm, ScenarioManager.ScenarioType.C2_Guidance, ScenarioManager.ScenarioType.C0_Abort },
        
        // Participant 2 sees: C2 -> C0 -> C1
        { ScenarioManager.ScenarioType.C2_Guidance, ScenarioManager.ScenarioType.C0_Abort, ScenarioManager.ScenarioType.C1_Confirm }
    };
    
    /// <summary>
    /// Gets the next scenario in the Latin square sequence for the current participant
    /// </summary>
    /// <returns>The next scenario type to run</returns>
    public ScenarioManager.ScenarioType GetNextScenario()
    {
        // Get the scenario at the current position in the Latin square
        ScenarioManager.ScenarioType nextScenario = _latinSquare[_participantIndex % 3, _step % 3];
        
        // Increment step for next call
        _step++;
        
        // Log which scenario is being returned
        Debug.Log($"Sequencer returning scenario: {nextScenario} (Participant {_participantIndex}, Step {_step-1})");
        
        return nextScenario;
    }
    
    /// <summary>
    /// Reset the sequencer to start from the beginning
    /// </summary>
    public void Reset()
    {
        _step = 0;
        Debug.Log($"Sequencer reset for participant {_participantIndex}");
    }
} 