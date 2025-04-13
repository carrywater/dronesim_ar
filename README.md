# Drone Delivery AR Simulation

A research-driven interactive AR experience that simulates drone deliveries in public spaces. The goal is to evaluate how human recipients respond to operational uncertainty in drone behavior, with both recipients and bystanders participating simultaneously in a synchronized mixed-reality environment.

## Project Architecture

### Core Components

1. **Interaction System**
   - **Recipient and Bystander Objects** (`CubeInteraction.cs`)
     - Interactive cubes that players pick up
     - Change color when picked up
     - Trigger scenario selection when both are picked up simultaneously
     - Uses NetworkVariable for synchronized pickup state

2. **Scenario Management System**
   - **Scenario Manager** (`ScenarioManager.cs`)
     - Manages the logic for different disruption levels
     - Randomly selects a scenario when both cubes are picked up
     - Handles scenario transitions
     - Communicates with the Drone Manager

3. **Drone System**
   - **Drone Spawner** (`DroneSpawner.cs`)
     - Solely responsible for spawning the drone in the environment
     - Uses AR Surface Detection to find suitable spawn locations
     - Does not handle navigation or behavior

   - **Drone Navigator** (`DroneNavigator.cs`)
     - Handles all navigation-related functionality
     - Uses NavMesh to navigate the drone to the recipient's position
     - Communicates with the Drone Manager for state changes

   - **Drone Manager** (`DroneManager.cs`)
     - Controls the drone's state and behaviors based on the scenario
     - Handles state transitions during flight
     - Implements scenario-specific landing behavior
     - Acts as the central coordinator for drone behavior

4. **Gesture System**
   - **Gesture Recognizer** (`GestureRecognizer.cs`)
     - Detects and interprets user gestures
     - Communicates with the Drone Manager for gesture-based interactions

### Scenarios

1. **No Disturbance (Low Involvement)**
   - Drone completes delivery fully autonomously
   - No user input required

2. **Moderate Disturbance (Moderate Involvement)**
   - Drone pauses to request confirmation via gesture
   - User must acknowledge and approve landing

3. **Major Disturbance (High Involvement)**
   - Drone requires recipient to point to a safe landing zone
   - User uses hand tracking to point and raycast target location

## Component Responsibilities

### Interaction System
- **CubeInteraction**: Handles user interaction with cubes, color changes, and triggering scenario selection
- **NetworkManager**: Manages network connections and synchronization

### Scenario Management System
- **ScenarioManager**: Selects and manages scenarios, communicates with DroneManager
- **ScenarioData**: Contains data structures for different scenarios

### Drone System
- **DroneSpawner**: Only responsible for spawning the drone at the appropriate location
- **DroneNavigator**: Handles all navigation logic using NavMesh
- **DroneManager**: Controls drone state and behavior based on the current scenario
- **DroneVisuals**: Manages visual feedback (lights, animations) based on drone state

### Gesture System
- **GestureRecognizer**: Detects and interprets user gestures
- **GestureHandler**: Processes recognized gestures and communicates with other systems

## Setup Instructions

1. **Scene Setup**
   - Create an empty scene
   - Add a plane or other surface for the drone to land on
   - Create two cubes for the Recipient and Bystander
   - Add the `CubeInteraction` script to both cubes
   - Set the `objectType` field to "Recipient" and "Bystander" respectively
   - Add a NavMesh to the scene for drone navigation

2. **Drone Setup**
   - Create a drone prefab with the following components:
     - `DroneManager` script (central coordinator)
     - `DroneNavigator` script (handles navigation)
     - `DroneVisuals` script (handles visual feedback)
     - `NavMeshAgent` component (used by DroneNavigator)
     - Mesh renderer and collider
   - Add the drone prefab to the `DroneSpawner` script

3. **Manager Setup**
   - Add the `ScenarioManager` script to an empty GameObject
   - Add the `DroneSpawner` script to an empty GameObject
   - Set the `dronePrefab` field in the `DroneSpawner` script

4. **Gesture System Setup**
   - Add the `GestureRecognizer` script to an empty GameObject
   - Configure gesture detection settings

5. **Networking Setup**
   - Ensure the scene is set up for networking with Unity Netcode for GameObjects
   - Set up the host and client connections

## Usage

1. **Starting the Simulation**
   - Start the scene as a host
   - Connect clients as Recipient and Bystander
   - Both players pick up their respective cubes
   - The scenario is randomly selected and the drone spawns

2. **Interacting with the Drone**
   - The drone flies to the recipient's position
   - Based on the scenario, the drone will:
     - Land autonomously (No Disturbance)
     - Request confirmation (Moderate Disturbance)
     - Request guidance (Major Disturbance)

3. **Completing the Delivery**
   - The drone lands at the specified location
   - The delivery is complete when the drone reaches the landing position

## Development Notes

- The project uses Unity Netcode for GameObjects for networking
- AR functionality is implemented using Meta SDK or AR Foundation
- NavMesh is used for drone navigation
- The project follows a component-based architecture with single-responsibility principle
- All scripts are documented with XML comments
- Each component has a clear, focused responsibility
