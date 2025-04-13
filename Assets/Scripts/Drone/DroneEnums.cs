namespace DroneSim
{
    /// <summary>
    /// Defines the possible states of the drone's behavior
    /// </summary>
    public enum DroneState
    {
        /// <summary>
        /// Initial state, drone is inactive
        /// </summary>
        Idle,
        
        /// <summary>
        /// Session initialization state
        /// </summary>
        SessionStart,
        
        /// <summary>
        /// Drone is flying towards the recipient
        /// </summary>
        FlightToRecipient,
        
        /// <summary>
        /// Evaluating current scenario to determine landing behavior
        /// </summary>
        EvaluateScenario,
        
        /// <summary>
        /// Autonomous landing with high certainty
        /// </summary>
        Landing_Autonomous,
        
        /// <summary>
        /// Waiting for user gesture confirmation
        /// </summary>
        Landing_Confirmation,
        
        /// <summary>
        /// Waiting for user to point to landing location
        /// </summary>
        Landing_Guidance,
        
        /// <summary>
        /// Landing sequence in progress
        /// </summary>
        LandingInProgress,
        
        /// <summary>
        /// Landing completed successfully
        /// </summary>
        LandingSuccess
    }

    /// <summary>
    /// Defines the different levels of operational disturbance
    /// </summary>
    public enum DeliveryScenario
    {
        /// <summary>
        /// No user input required - drone operates fully autonomously
        /// </summary>
        NoDisturbance,
        
        /// <summary>
        /// Requires user confirmation via gesture
        /// </summary>
        ModerateDisturbance,
        
        /// <summary>
        /// Requires user to point to landing location
        /// </summary>
        MajorDisturbance
    }
} 