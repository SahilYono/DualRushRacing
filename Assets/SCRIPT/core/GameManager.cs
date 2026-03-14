using UnityEngine;
using System;

/// <summary>
/// Master game controller - Manages global game state and data
/// Singleton pattern ensures only one instance exists
/// Persists across all scenes (DontDestroyOnLoad)
/// </summary>
public class GameManager : MonoBehaviour
{
    // Singleton instance - accessible from anywhere via GameManager.Instance
    public static GameManager Instance { get; private set; }

    [Header("Player Data")]
    public string PlayerID { get; private set; }
    public string PlayerName { get; private set; }
    public bool IsAuthenticated { get; private set; }

    [Header("Game State")]
    public bool IsInGame { get; private set; }
    public int CurrentScore { get; private set; }
    public string CurrentRoomCode { get; private set; }

    [Header("Settings")]
    public bool IsMusicEnabled { get; private set; } = true;
    public bool IsSFXEnabled { get; private set; } = true;

    // Events - Other scripts can subscribe to these
    public event Action OnAuthenticationChanged;
    public event Action<int> OnScoreChanged;
    public event Action OnGameStateChanged;

    #region Unity Lifecycle

    private void Awake()
    {
        // Singleton pattern implementation
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Persist across scenes
            InitializeManager();
        }
        else
        {
            // Destroy duplicate instances
            Destroy(gameObject);
        }
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initialize manager and load saved settings
    /// </summary>
    private void InitializeManager()
    {
        LoadSettings();
        Debug.Log("[GameManager] Initialized successfully");
    }

    /// <summary>
    /// Load player settings from PlayerPrefs (persistent storage)
    /// </summary>
    private void LoadSettings()
    {
        // PlayerPrefs stores data even after app closes
        IsMusicEnabled = PlayerPrefs.GetInt("MusicEnabled", 1) == 1;
        IsSFXEnabled = PlayerPrefs.GetInt("SFXEnabled", 1) == 1;

        Debug.Log($"[GameManager] Settings loaded - Music: {IsMusicEnabled}, SFX: {IsSFXEnabled}");
    }

    /// <summary>
    /// Save current settings to PlayerPrefs
    /// </summary>
    private void SaveSettings()
    {
        PlayerPrefs.SetInt("MusicEnabled", IsMusicEnabled ? 1 : 0);
        PlayerPrefs.SetInt("SFXEnabled", IsSFXEnabled ? 1 : 0);
        PlayerPrefs.Save(); // Force save to disk
    }

    #endregion

    #region Authentication Management

    /// <summary>
    /// Set player authentication data after successful login
    /// </summary>
    public void SetAuthenticationData(string playerID, string playerName)
    {
        PlayerID = playerID;
        PlayerName = playerName;
        IsAuthenticated = true;

        OnAuthenticationChanged?.Invoke();
        Debug.Log($"[GameManager] Player authenticated - ID: {playerID}, Name: {playerName}");
    }

    /// <summary>
    /// Clear authentication data on logout
    /// </summary>
    public void ClearAuthenticationData()
    {
        PlayerID = string.Empty;
        PlayerName = string.Empty;
        IsAuthenticated = false;

        OnAuthenticationChanged?.Invoke();
        Debug.Log("[GameManager] Player logged out");
    }

    #endregion

    #region Game State Management

    /// <summary>
    /// Set game active state (in gameplay scene)
    /// </summary>
    public void SetGameState(bool inGame)
    {
        IsInGame = inGame;
        OnGameStateChanged?.Invoke();

        Debug.Log($"[GameManager] Game state changed - InGame: {inGame}");
    }

    /// <summary>
    /// Set current room code for multiplayer
    /// </summary>
    public void SetRoomCode(string roomCode)
    {
        CurrentRoomCode = roomCode;
        Debug.Log($"[GameManager] Room code set: {roomCode}");
    }

    /// <summary>
    /// Clear room code when leaving room
    /// </summary>
    public void ClearRoomCode()
    {
        CurrentRoomCode = string.Empty;
        Debug.Log("[GameManager] Room code cleared");
    }

    #endregion

    #region Score Management

    /// <summary>
    /// Add points to current score
    /// </summary>
    public void AddScore(int points)
    {
        CurrentScore += points;
        OnScoreChanged?.Invoke(CurrentScore);

        Debug.Log($"[GameManager] Score increased by {points} - Total: {CurrentScore}");
    }

    /// <summary>
    /// Reset score to zero (used when starting new game)
    /// </summary>
    public void ResetScore()
    {
        CurrentScore = 0;
        OnScoreChanged?.Invoke(CurrentScore);

        Debug.Log("[GameManager] Score reset");
    }

    #endregion

    #region Audio Settings

    /// <summary>
    /// Toggle background music ON/OFF
    /// </summary>
    public void ToggleMusic()
    {
        IsMusicEnabled = !IsMusicEnabled;
        SaveSettings();

        // Notify AudioManager to update
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicEnabled(IsMusicEnabled);
        }

        Debug.Log($"[GameManager] Music toggled: {IsMusicEnabled}");
    }

    /// <summary>
    /// Toggle sound effects ON/OFF
    /// </summary>
    public void ToggleSFX()
    {
        IsSFXEnabled = !IsSFXEnabled;
        SaveSettings();

        // Notify AudioManager to update
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXEnabled(IsSFXEnabled);
        }

        Debug.Log($"[GameManager] SFX toggled: {IsSFXEnabled}");
    }

    /// <summary>
    /// Set music state directly
    /// </summary>
    public void SetMusicEnabled(bool enabled)
    {
        IsMusicEnabled = enabled;
        SaveSettings();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicEnabled(enabled);
        }
    }

    /// <summary>
    /// Set SFX state directly
    /// </summary>
    public void SetSFXEnabled(bool enabled)
    {
        IsSFXEnabled = enabled;
        SaveSettings();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXEnabled(enabled);
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Reset all game data (useful for testing or new game)
    /// </summary>
    public void ResetAllData()
    {
        ClearAuthenticationData();
        ClearRoomCode();
        ResetScore();
        SetGameState(false);

        Debug.Log("[GameManager] All data reset");
    }

    #endregion
}

/*
=== HOW TO USE THIS SCRIPT ===

1. CREATE EMPTY GAMEOBJECT:
   - In Hierarchy: Right-click → Create Empty
   - Rename to "GameManager"
   - Add this script as component

2. ACCESSING FROM OTHER SCRIPTS:
   
   // Get player name
   string name = GameManager.Instance.PlayerName;
   
   // Add score
   GameManager.Instance.AddScore(10);
   
   // Toggle music
   GameManager.Instance.ToggleMusic();
   
   // Check if authenticated
   if (GameManager.Instance.IsAuthenticated)
   {
       // Player is logged in
   }

3. SUBSCRIBING TO EVENTS:

   private void OnEnable()
   {
       GameManager.Instance.OnScoreChanged += HandleScoreChanged;
   }
   
   private void OnDisable()
   {
       GameManager.Instance.OnScoreChanged -= HandleScoreChanged;
   }
   
   private void HandleScoreChanged(int newScore)
   {
       scoreText.text = $"Score: {newScore}";
   }

4. TESTING:
   - Add this script to GameObject in any scene
   - Press Play
   - Check Console for "[GameManager] Initialized" message

=== KEY CONCEPTS EXPLAINED ===

SINGLETON PATTERN:
- Ensures only ONE instance of GameManager exists
- Accessible from anywhere: GameManager.Instance
- Survives scene changes (DontDestroyOnLoad)

PLAYERPREFS:
- Unity's built-in persistent storage
- Saves data even when app closes
- Perfect for settings and preferences
- PlayerPrefs.SetInt, GetInt, SetString, GetString, Save

EVENTS:
- Allow scripts to "listen" for changes
- Loose coupling = better code organization
- Subscribe with += , Unsubscribe with -=
- Invoke with ?. (null-safe)

=== NEXT STEPS ===
After adding this script:
1. Create AudioManager.cs (handles music/SFX)
2. Create SceneTransitionManager.cs (fade transitions)
3. Test all three managers working together
*/