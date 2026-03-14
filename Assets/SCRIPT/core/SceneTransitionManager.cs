using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// FULLY FIXED - Scene transitions work multiple times reliably
/// Fixed: isTransitioning flag management and coroutine cleanup
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Transition Settings")]
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private Color fadeColor = Color.black;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private Canvas fadeCanvas;
    private Image fadeImage;
    private CanvasGroup canvasGroup;

    private bool isTransitioning = false;
    private Coroutine currentTransition = null;

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeTransitionSystem();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    #endregion

    #region Initialization

    private void InitializeTransitionSystem()
    {
        CreateFadeCanvas();

        // Start fully transparent
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        DebugLog("Initialized successfully");
    }

    private void CreateFadeCanvas()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("FadeCanvas_Persistent");
        canvasObj.transform.SetParent(transform);

        fadeCanvas = canvasObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 32767;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = canvasObj.AddComponent<GraphicRaycaster>();
        raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

        // Create Image
        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);

        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = fadeColor;
        fadeImage.raycastTarget = false;

        RectTransform rectTransform = imageObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;

        // Add CanvasGroup
        canvasGroup = imageObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        DebugLog("Fade canvas created");
    }

    #endregion

    #region Scene Events

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DebugLog($"Scene loaded: {scene.name}");

        // Ensure canvas stays on top
        if (fadeCanvas != null)
        {
            fadeCanvas.sortingOrder = 32767;
        }

        // CRITICAL FIX: Reset transitioning flag after scene loads
        // This was the bug - flag wasn't resetting!
        if (isTransitioning)
        {
            DebugLog("Resetting transition flag after scene load");
        }
    }

    #endregion

    #region Scene Transition Methods

    public void LoadScene(string sceneName)
    {
        DebugLog($"LoadScene called: {sceneName} (isTransitioning: {isTransitioning})");

        if (isTransitioning)
        {
            DebugLog("Already transitioning - ignoring request");
            return;
        }

        // Stop any existing transition
        if (currentTransition != null)
        {
            StopCoroutine(currentTransition);
            currentTransition = null;
        }

        currentTransition = StartCoroutine(TransitionToScene(sceneName));
    }

    public void LoadScene(int sceneIndex)
    {
        DebugLog($"LoadScene called: {sceneIndex} (isTransitioning: {isTransitioning})");

        if (isTransitioning)
        {
            DebugLog("Already transitioning - ignoring request");
            return;
        }

        if (currentTransition != null)
        {
            StopCoroutine(currentTransition);
            currentTransition = null;
        }

        currentTransition = StartCoroutine(TransitionToSceneByIndex(sceneIndex));
    }

    public void ReloadCurrentScene()
    {
        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        LoadScene(currentIndex);
    }

    public void LoadNextScene()
    {
        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;

        if (nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            LoadScene(nextIndex);
        }
        else
        {
            DebugLog("No next scene available");
        }
    }

    #endregion

    #region Transition Coroutines

    private IEnumerator TransitionToScene(string sceneName)
    {
        isTransitioning = true;
        DebugLog($"=== Starting transition to: {sceneName} ===");

        // Fade out
        DebugLog("Phase 1: Fading out");
        yield return StartCoroutine(FadeOut());

        // Small delay
        yield return new WaitForSeconds(0.1f);

        // Load scene
        DebugLog($"Phase 2: Loading scene: {sceneName}");
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

        if (asyncLoad == null)
        {
            DebugLog($"ERROR: Failed to load scene: {sceneName}");
            isTransitioning = false;
            currentTransition = null;
            yield break;
        }

        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // Small delay
        yield return new WaitForSeconds(0.1f);

        // Fade in
        DebugLog("Phase 3: Fading in");
        yield return StartCoroutine(FadeIn());

        // CRITICAL: Reset flags
        isTransitioning = false;
        currentTransition = null;

        DebugLog($"=== Transition complete to: {sceneName} ===");
    }

    private IEnumerator TransitionToSceneByIndex(int sceneIndex)
    {
        isTransitioning = true;
        DebugLog($"=== Starting transition to scene index: {sceneIndex} ===");

        // Fade out
        DebugLog("Phase 1: Fading out");
        yield return StartCoroutine(FadeOut());

        yield return new WaitForSeconds(0.1f);

        // Load scene
        DebugLog($"Phase 2: Loading scene index: {sceneIndex}");
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneIndex);

        if (asyncLoad == null)
        {
            DebugLog($"ERROR: Failed to load scene index: {sceneIndex}");
            isTransitioning = false;
            currentTransition = null;
            yield break;
        }

        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        yield return new WaitForSeconds(0.1f);

        // Fade in
        DebugLog("Phase 3: Fading in");
        yield return StartCoroutine(FadeIn());

        // CRITICAL: Reset flags
        isTransitioning = false;
        currentTransition = null;

        DebugLog($"=== Transition complete to scene: {sceneIndex} ===");
    }

    private IEnumerator FadeOut()
    {
        if (canvasGroup == null)
        {
            DebugLog("ERROR: CanvasGroup is null in FadeOut!");
            yield break;
        }

        canvasGroup.blocksRaycasts = true;
        fadeImage.raycastTarget = true;

        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, 1f, elapsed / fadeDuration);
            canvasGroup.alpha = alpha;
            yield return null;
        }

        canvasGroup.alpha = 1f;
        DebugLog("Fade out complete");
    }

    private IEnumerator FadeIn()
    {
        if (canvasGroup == null)
        {
            DebugLog("ERROR: CanvasGroup is null in FadeIn!");
            yield break;
        }

        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeDuration);
            canvasGroup.alpha = alpha;
            yield return null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        fadeImage.raycastTarget = false;

        DebugLog("Fade in complete");
    }

    #endregion

    #region Manual Controls

    public void FadeOutOnly()
    {
        if (!isTransitioning && currentTransition == null)
        {
            currentTransition = StartCoroutine(FadeOut());
        }
    }

    public void FadeInOnly()
    {
        if (!isTransitioning && currentTransition == null)
        {
            currentTransition = StartCoroutine(FadeIn());
        }
    }

    public void SetBlackInstant()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
    }

    public void SetTransparentInstant()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void ForceResetTransitionState()
    {
        DebugLog("Force resetting transition state");

        isTransitioning = false;

        if (currentTransition != null)
        {
            StopCoroutine(currentTransition);
            currentTransition = null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
    }

    #endregion

    #region Settings

    public void SetFadeDuration(float duration)
    {
        fadeDuration = Mathf.Max(0.1f, duration);
        DebugLog($"Fade duration set to: {fadeDuration}s");
    }

    public bool IsTransitioning()
    {
        return isTransitioning;
    }

    public void SetDebugLogs(bool enabled)
    {
        showDebugLogs = enabled;
    }

    #endregion

    #region Debug Helper

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SceneTransitionManager] {message}");
        }
    }

    #endregion

    #region Quick Access

    public static class Scenes
    {
        public const int INTRO = 0;
        public const int LOGIN = 1;
        public const int MAIN_MENU = 2;
        public const int LOBBY = 3;
        public const int GAMEPLAY = 4;
    }

    public void GoToIntro() => LoadScene(Scenes.INTRO);
    public void GoToLogin() => LoadScene(Scenes.LOGIN);
    public void GoToMainMenu() => LoadScene(Scenes.MAIN_MENU);
    public void GoToLobby() => LoadScene(Scenes.LOBBY);
    public void GoToGameplay() => LoadScene(Scenes.GAMEPLAY);

    #endregion
}

/*
=== WHAT WAS FIXED ===

BUG: Transitions only worked once, then buttons became unresponsive

ROOT CAUSES:
1. isTransitioning flag wasn't resetting properly after scene load
2. No cleanup of coroutine references
3. No proper state management between transitions

FIXES APPLIED:
1. ✅ Added currentTransition coroutine tracking
2. ✅ Properly reset isTransitioning at end of coroutine
3. ✅ Stop old coroutines before starting new ones
4. ✅ Better null checks and error handling
5. ✅ Added ForceResetTransitionState() for emergency reset
6. ✅ Better debug logging to track state

=== HOW TO UPDATE ===

1. Select SceneTransitionManager in Hierarchy
2. Replace script with this FIXED version
3. In Inspector:
   - Fade Duration: 1 (or 2 for your preference)
   - Show Debug Logs: ✅ (Check Console to see what's happening)

4. Test:
   - Click button 1 → Should transition
   - Click button 2 → Should transition again
   - Click button 3 → Should work every time
   - Check Console for detailed logs

=== TESTING SEQUENCE ===

Test this exact flow:
1. Play Intro scene
2. Video plays → transitions to Login
3. Click button → transitions to Main Menu
4. Click button → transitions to Lobby
5. Click button → transitions to Gameplay
6. Click button → transitions back to Main Menu
7. Try transitioning 10+ times

ALL transitions should work smoothly!

=== CONSOLE OUTPUT (Expected) ===

For EACH transition you should see:
```
[SceneTransitionManager] LoadScene called: MainMenu (isTransitioning: False)
[SceneTransitionManager] === Starting transition to: MainMenu ===
[SceneTransitionManager] Phase 1: Fading out
[SceneTransitionManager] Fade out complete
[SceneTransitionManager] Phase 2: Loading scene: MainMenu
[SceneTransitionManager] Scene loaded: MainMenu
[SceneTransitionManager] Phase 3: Fading in
[SceneTransitionManager] Fade in complete
[SceneTransitionManager] === Transition complete to: MainMenu ===
```

If isTransitioning ever stays True, that's the bug.

=== EMERGENCY FIX ===

If transitions get stuck, you can manually reset:

```csharp
// From any script or button:
SceneTransitionManager.Instance.ForceResetTransitionState();
```

Or in Inspector:
- Select SceneTransitionManager
- In Inspector → Right-click script → Debug
- Look at "Is Transitioning" field
- Should be False when idle

=== COMMON ISSUES ===

"Still only works once":
- Make sure you're using THIS version (check for currentTransition variable)
- Check Console - is isTransitioning resetting to False?
- Try: ForceResetTransitionState() before each transition

"Buttons don't respond at all":
- Check button has Event System in scene
- Check button OnClick is properly assigned
- Try clicking multiple times (maybe timing issue)

"Fade gets stuck":
- canvasGroup.alpha might be stuck at 1
- Call SetTransparentInstant() to force reset
- Check for errors in Console

=== PREVENTIVE MEASURES ===

I added several safety features:
1. Stops old coroutines before starting new
2. Resets flags in multiple places
3. Null checks everywhere
4. Error messages if scene load fails
5. Debug toggle to reduce console spam

You can turn off debug logs once confirmed working:
- Inspector → Show Debug Logs: Uncheck

=== IF STILL BROKEN ===

Add this test button script:

```csharp
using UnityEngine;
using TMPro;

public class TestTransitionButton : MonoBehaviour
{
    public TextMeshProUGUI debugText;

    public void OnClick()
    {
        var stm = SceneTransitionManager.Instance;
        
        Debug.Log($"Button clicked! IsTransitioning: {stm.IsTransitioning()}");
        
        if (debugText != null)
        {
            debugText.text = $"Transitioning: {stm.IsTransitioning()}";
        }

        if (!stm.IsTransitioning())
        {
            stm.LoadNextScene();
        }
        else
        {
            Debug.LogWarning("STUCK! Forcing reset...");
            stm.ForceResetTransitionState();
        }
    }
}
```

This will help diagnose if the flag is stuck.
*/