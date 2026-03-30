using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages all audio in the game - background music and sound effects
/// Singleton pattern with DontDestroyOnLoad
/// Handles fade in/out, volume control, and audio pooling
/// </summary>
[RequireComponent(typeof(AudioSource))] // Automatically adds AudioSource component
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource; // For background music
    [SerializeField] private AudioSource sfxSource;   // For sound effects

    [Header("Background Music")]
    [SerializeField] private AudioClip mainMenuMusic;
    [SerializeField] private AudioClip gameplayMusic;
    [SerializeField] private AudioClip lobbyMusic;

    [Header("Sound Effects")]
    [SerializeField] private AudioClip buttonClickSFX;
    [SerializeField] private AudioClip crashSFX;
    [SerializeField] private AudioClip collectCoinSFX;
    [SerializeField] private AudioClip powerUpSFX;
    [SerializeField] private AudioClip countdownSFX;
    [SerializeField] private AudioClip winSFX;
    [SerializeField] private AudioClip loseSFX;

    [Header("Settings")]
    [SerializeField] private float musicVolume = 0.5f;
    [SerializeField] private float sfxVolume = 0.7f;
    [SerializeField] private float fadeDuration = 1f; // Fade in/out time

    // Current states
    private bool isMusicEnabled = true;
    private bool isSFXEnabled = true;
    private Coroutine fadeCoroutine;

    // Audio clip dictionary for quick access
    private Dictionary<string, AudioClip> sfxDictionary;

    #region Unity Lifecycle

    private void Awake()
    {
        // Singleton implementation
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioManager();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Load settings from GameManager
        if (GameManager.Instance != null)
        {
            isMusicEnabled = GameManager.Instance.IsMusicEnabled;
            isSFXEnabled = GameManager.Instance.IsSFXEnabled;
        }

        UpdateAudioStates();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Set up audio sources and initialize systems
    /// </summary>
    private void InitializeAudioManager()
    {
        // Create AudioSources if not assigned
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true; // Music loops continuously
            musicSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false; // SFX plays once
            sfxSource.playOnAwake = false;
        }

        // Set initial volumes
        musicSource.volume = musicVolume;
        sfxSource.volume = sfxVolume;

        // Build SFX dictionary for easy access
        BuildSFXDictionary();

        Debug.Log("[AudioManager] Initialized successfully");
    }

    /// <summary>
    /// Create dictionary of sound effects for fast lookup
    /// </summary>
    private void BuildSFXDictionary()
    {
        sfxDictionary = new Dictionary<string, AudioClip>
        {
            { "ButtonClick", buttonClickSFX },
            { "Crash", crashSFX },
            { "Coin", collectCoinSFX },
            { "PowerUp", powerUpSFX },
            { "Countdown", countdownSFX },
            { "Win", winSFX },
            { "Lose", loseSFX }
        };
    }

    #endregion

    #region Music Control

    /// <summary>
    /// Play main menu music with fade in
    /// </summary>
    public void PlayMainMenuMusic()
    {
        PlayMusicWithFade(mainMenuMusic);
    }

    /// <summary>
    /// Play gameplay music with fade in
    /// </summary>
    public void PlayGameplayMusic()
    {
        PlayMusicWithFade(gameplayMusic);
    }

    /// <summary>
    /// Play lobby music with fade in
    /// </summary>
    public void PlayLobbyMusic()
    {
        PlayMusicWithFade(lobbyMusic);
    }

    /// <summary>
    /// Play music with smooth fade transition
    /// </summary>
    private void PlayMusicWithFade(AudioClip newClip)
    {
        if (newClip == null)
        {
            Debug.LogWarning("[AudioManager] Music clip is null!");
            return;
        }

        // If same music is already playing, do nothing
        if (musicSource.clip == newClip && musicSource.isPlaying)
        {
            return;
        }

        // Stop any ongoing fade
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        // Start crossfade
        fadeCoroutine = StartCoroutine(CrossfadeMusic(newClip));
    }

    /// <summary>
    /// Coroutine: Fade out old music, fade in new music
    /// </summary>
    private IEnumerator CrossfadeMusic(AudioClip newClip)
    {
        float targetVolume = isMusicEnabled ? musicVolume : 0f;

        // Fade out current music
        if (musicSource.isPlaying)
        {
            float startVolume = musicSource.volume;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeDuration);
                yield return null;
            }

            musicSource.Stop();
        }

        // Change clip
        musicSource.clip = newClip;
        musicSource.Play();

        // Fade in new music
        float elapsedFadeIn = 0f;
        while (elapsedFadeIn < fadeDuration)
        {
            elapsedFadeIn += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(0f, targetVolume, elapsedFadeIn / fadeDuration);
            yield return null;
        }

        musicSource.volume = targetVolume;
        fadeCoroutine = null;
    }

    /// <summary>
    /// Stop music with fade out
    /// </summary>
    public void StopMusic()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(FadeOutMusic());
    }

    /// <summary>
    /// Coroutine: Fade out music smoothly
    /// </summary>
    private IEnumerator FadeOutMusic()
    {
        float startVolume = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeDuration);
            yield return null;
        }

        musicSource.Stop();
        musicSource.volume = musicVolume;
        fadeCoroutine = null;
    }

    #endregion

    #region Sound Effects

    /// <summary>
    /// Play sound effect by name
    /// </summary>
    public void PlaySFX(string sfxName)
    {
        if (!isSFXEnabled) return;

        if (sfxDictionary.TryGetValue(sfxName, out AudioClip clip))
        {
            if (clip != null)
            {
                sfxSource.PlayOneShot(clip, sfxVolume);
            }
            else
            {
                Debug.LogWarning($"[AudioManager] SFX '{sfxName}' clip is null!");
            }
        }
        else
        {
            Debug.LogWarning($"[AudioManager] SFX '{sfxName}' not found in dictionary!");
        }
    }

    /// <summary>
    /// Play sound effect directly (if you have the AudioClip reference)
    /// </summary>
    public void PlaySFXDirect(AudioClip clip)
    {
        if (!isSFXEnabled || clip == null) return;

        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    /// <summary>
    /// Play button click sound (commonly used)
    /// </summary>
    public void PlayButtonClick()
    {
        PlaySFX("ButtonClick");
    }

    #endregion

    #region Settings Control

    /// <summary>
    /// Enable or disable background music
    /// </summary>
    public void SetMusicEnabled(bool enabled)
    {
        isMusicEnabled = enabled;
        UpdateAudioStates();

        Debug.Log($"[AudioManager] Music {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Enable or disable sound effects
    /// </summary>
    public void SetSFXEnabled(bool enabled)
    {
        isSFXEnabled = enabled;

        Debug.Log($"[AudioManager] SFX {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Update music volume based on current state
    /// </summary>
    private void UpdateAudioStates()
    {
        if (musicSource != null)
        {
            musicSource.volume = isMusicEnabled ? musicVolume : 0f;
        }
    }

    /// <summary>
    /// Set master music volume (0-1)
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null)
        {
            musicSource.volume = isMusicEnabled ? musicVolume : 0f;
        }
    }

    /// <summary>
    /// Set master SFX volume (0-1)
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSource != null)
        {
            sfxSource.volume = sfxVolume;
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Check if music is currently playing
    /// </summary>
    public bool IsMusicPlaying()
    {
        return musicSource != null && musicSource.isPlaying;
    }

    /// <summary>
    /// Get current music enabled state
    /// </summary>
    public bool GetMusicEnabled()
    {
        return isMusicEnabled;
    }

    /// <summary>
    /// Get current SFX enabled state
    /// </summary>
    public bool GetSFXEnabled()
    {
        return isSFXEnabled;
    }

    #endregion
}

/*
=== HOW TO USE THIS SCRIPT ===

1. SETUP IN UNITY:
   - Create Empty GameObject named "AudioManager"
   - Add this script as component
   - Assign all AudioClip fields in Inspector:
     * Drag MP3/WAV files into slots
     * musicSource and sfxSource are auto-created

2. PLAYING MUSIC:
   
   // In MainMenu scene
   AudioManager.Instance.PlayMainMenuMusic();
   
   // In Gameplay scene
   AudioManager.Instance.PlayGameplayMusic();
   
   // Stop music
   AudioManager.Instance.StopMusic();

3. PLAYING SOUND EFFECTS:
   
   // By name (using dictionary)
   AudioManager.Instance.PlaySFX("Crash");
   AudioManager.Instance.PlaySFX("Coin");
   
   // Direct clip
   AudioManager.Instance.PlaySFXDirect(myAudioClip);
   
   // Quick button click
   AudioManager.Instance.PlayButtonClick();

4. HOOKING TO UI BUTTONS:
   
   // In Unity Inspector:
   // Button → OnClick() → AudioManager → PlayButtonClick()

5. CONTROLLING FROM SETTINGS:
   
   // Toggle music
   AudioManager.Instance.SetMusicEnabled(false);
   
   // Toggle SFX
   AudioManager.Instance.SetSFXEnabled(false);
   
   // Change volumes
   AudioManager.Instance.SetMusicVolume(0.5f); // 50%
   AudioManager.Instance.SetSFXVolume(0.8f);   // 80%

=== KEY FEATURES ===

CROSSFADE:
- Music smoothly transitions between scenes
- No abrupt cuts or audio pops
- fadeDuration controls transition speed

AUDIO POOLING:
- SFX uses PlayOneShot (no GameObject overhead)
- Multiple sounds can play simultaneously
- Efficient for mobile

PERSISTENT:
- Survives scene changes (DontDestroyOnLoad)
- Settings sync with GameManager
- One AudioManager for entire game

=== WHERE TO GET AUDIO FILES ===

FREE MUSIC:
- https://incompetech.com/ (royalty-free)
- https://freesound.org/
- Unity Asset Store (search "free music")

FREE SFX:
- https://freesound.org/
- https://mixkit.co/free-sound-effects/
- https://www.zapsplat.com/

FORMATS:
- Use MP3 (smaller file size) for music
- Use WAV for SFX (no compression delay)
- Unity supports: MP3, WAV, OGG, AIFF

=== TESTING ===
1. Add script to scene
2. Add test audio clips
3. Press Play
4. Call AudioManager.Instance.PlayMainMenuMusic() from another script
5. Should hear music with 1-second fade in

=== NEXT STEPS ===
After AudioManager is working:
1. Create SceneTransitionManager (visual fade)
2. Test music transitions between scenes
3. Hook buttons to PlayButtonClick()
*/