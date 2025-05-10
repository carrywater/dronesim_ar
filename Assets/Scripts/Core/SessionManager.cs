using UnityEngine;

public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Called to begin the AR session and trigger initial drone spawn logic.
    /// </summary>
    public void StartSession()
    {
        // TODO: Implement session start logic (e.g., enable DroneSpawner, log analytics)
    }

    // TODO: Implement session management logic
} 