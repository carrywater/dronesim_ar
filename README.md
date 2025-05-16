# Drone AR Simulation Framework

This framework simulates drone behavior for human-drone interaction research with three core scenarios:
- **C-0** (High Autonomy/Abort): Drone attempts landing autonomously, but aborts due to uncertainty
- **C-1** (Confirm): User confirms or rejects drone landing spots using thumbs up/down gestures
- **C-2** (Guidance): User provides direct spatial guidance to the drone

## Architecture

### Component Hierarchy

```
drone4 (prefab)
├── Drone Offset (visuals)
│   ├── Rotors
│   ├── Legs
│   ├── HMI
│   └── Other visual components
└── Zone
    ├── NavigationZone
    │   └── c0target
    └── InteractionZone
        ├── c1target
        │   └── c1CueOffset (thumbs up/down UI)
        └── c2target
```

### Component Responsibilities

| Component | Responsibility | Key Features |
|-----------|---------------|--------------|
| **DroneController** | Flight state machine & movement | Idle, Hover, CruiseToTarget, Landing, LandAbort, Abort states |
| **DroneArrivalDetector** | Detects arrival at destinations | Signals when drone has reached cruise target |
| **DroneHMI** | LED animations and audio cues | Status indicator for drone intentions |
| **InteractionManager** | Interaction zones and AR UI | Controls UI cues and handles user interactions |
| **ZoneRandomizer** | Randomizes targets in zones | Multiple zone-target pairs |
| **ScenarioManager** | Orchestrates all scenarios | Sequentially runs C-0, C-1, C-2 |
| **PIDController** | Subtle position sway | Creates realistic drone hovering |

## Setup Instructions

1. **ZoneRandomizer Setup**:
   - Add to the root `Zone` GameObject
   - In the inspector, create Zone-Target pairs:
     - Pair 0: Name: "C1", Zone Transform: InteractionZone, Target: c1target
     - Pair 1: Name: "C2", Zone Transform: InteractionZone, Target: c2target
     - Pair 2: Name: "C0", Zone Transform: NavigationZone, Target: c0target

2. **InteractionManager Setup**:
   - Add to the InteractionZone GameObject
   - Assign references:
     - ZoneRandomizer: reference to the ZoneRandomizer above
     - DroneController: reference to the drone controller
     - C1CueObject: the c1target/c1CueOffset with thumbs up/down UI
     - C2CueObject: the c2target guidance UI
   - Set Zone Indices:
     - C1 Zone Index: 0
     - C2 Zone Index: 1

3. **Connect UI Events**:
   - On the thumbs up button (in c1CueOffset):
     - Add OnClick event calling InteractionManager.HandleConfirm with c1target position
   - On the thumbs down button:
     - Add OnClick event calling InteractionManager.HandleReject

4. **ScenarioManager Setup**:
   - Add to the drone GameObject
   - Assign references:
     - Drone: reference to the DroneController
     - HMI: reference to the DroneHMI
     - InteractionManager: reference to the InteractionManager
     - ZoneRandomizer: reference to the ZoneRandomizer
   - Set Navigation Zone Index: 2

## Changes to Note

The original architecture had separate components for:
- **ARInterfaceManager**: Handled AR UI elements
- **InteractionZoneController**: Managed interaction zones and scenarios

These have been combined into a single **InteractionManager** for simplicity:
- Provides clearer responsibility boundaries
- Simplifies connections between components
- Reduces the need for cross-component event handlers

## Workflow

1. When the scene starts, ScenarioManager runs the C-0 scenario
2. After C-0 completes, it transitions to C-1 (confirm landing spot)
3. When C-1 completes, it transitions to C-2 (guidance)
4. Each scenario uses the InteractionManager to handle UI and user inputs

## Scenario Details

### C-0 (High Autonomy/Abort)
- Drone cruises to random points in NavigationZone
- Attempts landing, signals uncertainty, aborts
- After two attempts, performs full mission abort

### C-1 (Confirm)
- Drone cruises to InteractionZone
- Landing probe appears at random position
- User confirms with thumbs up or rejects with thumbs down
- On rejection, a new random position is selected

### C-2 (Guidance)
- Drone cruises to InteractionZone
- Guidance pad appears
- User provides direct spatial guidance
- Drone follows guidance until timeout/completion

## Development Notes

- The project uses Unity Netcode for GameObjects for networking
- AR functionality is implemented using Meta SDK or AR Foundation
- NavMesh is used for drone navigation
- The project follows a component-based architecture with single-responsibility principle
- All scripts are documented with XML comments
- Each component has a clear, focused responsibility
