# Human-Drone Interaction Study (Outdoor AR) — Project README

---

## 1. Overview

This project is a research-driven AR experience simulating drone deliveries in public spaces, designed for Meta Quest headsets. The system evaluates human responses to drone behavior under uncertainty, supporting both recipients and bystanders in a synchronized, mixed-reality environment.

---

## 2. Codebase Organization

All scripts are organized by responsibility and modularity:

```
Scripts/
  Core/           # Scenario orchestration and sequencing
  Drone/          # Drone flight, HMI, and physical subsystems
  Interaction/    # User gesture, cue, and AR interaction logic
  Visualization/  # Spline/path visualization
  Utils/          # General utilities (audio, target zones, etc.)
```

---

## 3. Core Components & Responsibilities

### 3.1 Core

| Script                | Responsibility                                                      |
|-----------------------|---------------------------------------------------------------------|
| `ScenarioManager`     | Orchestrates scenario flow (C-0/C-1/C-2), drives drone, HMI, and AR |
| `ScenarioSequencer`   | Provides randomized/counterbalanced scenario order                  |

---

### 3.2 Drone

| Script                  | Responsibility                                                      |
|-------------------------|---------------------------------------------------------------------|
| `DroneController`       | Main flight FSM: take-off, cruise, hover, landing, abort            |
| `DroneHMI`              | LED and audio state machine; visual/auditory feedback               |
| `DroneRotorController`  | Controls rotor animation and state                                  |
| `DroneLandingGear`      | Controls landing gear animation/state                               |
| `DroneManager`          | High-level drone state and coordination                             |
| `PIDController`         | Sway and stabilization for realism                                  |
| `DroneComponents`       | Manages references to drone subsystems                              |
| `DroneHMDTracker`       | Tracks HMD position for AR alignment                                |

> **Note:**  
> `DroneMovementController` is legacy and may be superseded by `DroneController`. Use only if referenced in your scene.

---

### 3.3 Interaction

| Script                   | Responsibility                                                      |
|--------------------------|---------------------------------------------------------------------|
| `InteractionManager`     | Scenario-agnostic manager for cues and gesture interactions         |
| `PointGestureHandler`    | Handles thumbs up/down and pointing gestures                        |
| `ConfirmGestureHandler`  | Handles grab/marker placement interactions                          |
| `PlaneReticleDataIcon`   | Visual reticle for AR marker placement                              |
| `ThumbCueLookAtHMD`      | Ensures thumb cue faces the user's HMD                              |

---

### 3.4 Visualization

| Script                        | Responsibility                                                  |
|-------------------------------|---------------------------------------------------------------|
| `SplineManager`               | Visualizes drone path to active target                         |
| `SplineContainerVisualizer`   | Utility for spline rendering                                   |

---

### 3.5 Utils

| Script                | Responsibility                                                      |
|-----------------------|---------------------------------------------------------------------|
| `TargetPositioner`    | Manages active target and interaction/navigation zones              |
| `SpatialAudioHelper`  | Utility for 3D spatial audio configuration                          |

---

## 4. Scenario Flow & Orchestration

### 4.1 ScenarioManager

- **Central orchestrator**: Drives all scenario logic, referencing only public APIs of subsystems.
- **Scenario types**:  
  - **C-0 (High Autonomy – Abort):** Drone hovers, signals uncertainty, aborts after timeout.
  - **C-1 (Medium – Confirm):** Drone hovers, shows probe, user confirms/rejects with thumbs up/down.
  - **C-2 (High – Guidance):** Drone hovers, shows guidance pad, user points to select landing spot.

- **How to use cues and handlers:**
  - **Show/hide cues** by name:  
    ```csharp
    _interactionManager.ShowCue("thumb up");
    _interactionManager.HideCue("thumb up");
    ```
  - **Start/stop interactions** by type string:  
    ```csharp
    _interactionManager.StartInteraction("point");
    _interactionManager.StopInteraction("point");
    ```
  - **Wait for completion:**  
    ```csharp
    yield return new WaitUntil(() => _interactionManager.IsInteractionComplete);
    ```

- **All scenario logic is centralized here.**  
  Subsystems (drone, cues, handlers) are scenario-agnostic.

---

### 4.2 Example: C-1 Confirm Scenario

```csharp
private IEnumerator RunC1Scenario(ScenarioConfig config)
{
    // Show thumbs up cue
    _interactionManager.ShowCue("thumb up");

    // Start point gesture interaction
    _interactionManager.StartInteraction("point");

    // Wait for user to confirm/reject
    yield return new WaitUntil(() => _interactionManager.IsInteractionComplete);

    // Hide cue and stop interaction
    _interactionManager.HideCue("thumb up");
    _interactionManager.StopInteraction("point");
}
```

---

## 5. Inspector Setup & Best Practices

- **Cues**:  
  - Register all cue GameObjects in the `InteractionManager` inspector.
  - **Naming is critical**: Use unique, descriptive names (e.g., `"thumb up"`, `"ring"`, `"marker"`).

- **Handlers**:  
  - Register all handler scripts in the `InteractionManager` inspector.
  - The type name (e.g., `PointGestureHandler`, `ConfirmGestureHandler`) determines the string used in code (`"point"`, `"confirm"`).

- **Zones**:  
  - Use `TargetPositioner` to manage and randomize target positions within named zones.

- **Visualization**:  
  - Use `SplineManager` to visualize the path from the drone to the active target.

- **Spatial Audio**:  
  - Use `SpatialAudioHelper` for all 3D audio cues.

---

## 6. Deprecated/Unused Scripts

The following scripts are **no longer used** and can be safely deleted or ignored:
- `Spawning/NetworkedFindSpawnPositions.cs`
- `Spawning/SpawnLocator.cs`
- `Spawning/SpawnOffseter.cs`
- Any legacy/duplicate movement or drone scripts not referenced in your scene

---

## 7. Extending the System

- **To add a new scenario:**  
  - Only update `ScenarioManager` and scenario configs.
  - Do **not** add scenario logic to subsystems.

- **To add a new gesture or cue:**  
  - Create a new handler script and register it in the inspector.
  - Reference by type string in `ScenarioManager`.

- **To add a new zone or target:**  
  - Add to `TargetPositioner` and reference by name.

---

## 8. Contributor Guidelines

- **Single responsibility**: Each script should do one thing well.
- **Scenario-agnostic subsystems**: Only `ScenarioManager` knows about scenario flow.
- **Event-driven communication**: Use events and flags for cross-system communication.
- **Document public APIs**: Use XML comments for all public methods and classes.

---

## 9. Quick Reference: Key APIs

| System                | Key API Example                                 |
|-----------------------|-------------------------------------------------|
| Cues                  | `ShowCue("thumb up")`, `HideCue("ring")`    |
| Interactions          | `StartInteraction("point")`, `StopInteraction("confirm")` |
| Drone                 | `TransitionToHover()`, `TransitionToLanding(pos)`|
| HMI                   | `SetStatus(HMIState.Uncertain)`                 |
| Target Positioning    | `SetActiveTargetPosition(Vector3 pos)`          |
| Spline Visualization  | `SetSplineTarget(Transform target)`             |

---

**This README reflects the current, modular, scenario-driven architecture.  
Remove any scripts not listed above, and keep all new features scenario-agnostic!**
