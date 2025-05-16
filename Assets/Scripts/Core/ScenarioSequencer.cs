using UnityEngine;

public class ScenarioSequencer : MonoBehaviour
{
    [Tooltip("Participant index (0-2) for determining scenario order in Latin square")]
    [SerializeField] private int _participantIndex = 0;
    
    // 3x3 Latin square design for counterbalancing scenarios
    private static readonly ScenarioType[,] _latinSquare = {
        // Row 0
        { ScenarioType.C0_Abort, ScenarioType.C1_Confirm, ScenarioType.C2_Guidance },
        // Row 1
        { ScenarioType.C1_Confirm, ScenarioType.C2_Guidance, ScenarioType.C0_Abort },
        // Row 2
        { ScenarioType.C2_Guidance, ScenarioType.C0_Abort, ScenarioType.C1_Confirm }
    };
    
    private int _currentStep = 0;
    
    // Get the next scenario based on participant index and current step
    public ScenarioType GetNextScenario()
    {
        // Get scenario from Latin square based on participant and step
        ScenarioType nextScenario = _latinSquare[_participantIndex % 3, _currentStep % 3];
        
        // Increment step for next call
        _currentStep++;
        
        return nextScenario;
    }
    
    // Reset the sequence to start from the beginning
    public void ResetSequence()
    {
        _currentStep = 0;
    }
    
    // For manual testing
    public void SetParticipantIndex(int index)
    {
        _participantIndex = Mathf.Clamp(index, 0, 2);
        ResetSequence();
    }
} 