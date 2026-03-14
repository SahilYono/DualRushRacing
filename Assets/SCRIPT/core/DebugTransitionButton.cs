using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Debug button to test scene transitions and show current state
/// Attach to your test buttons to see what's happening
/// </summary>
[RequireComponent(typeof(Button))]
public class DebugTransitionButton : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string targetSceneName;
    [SerializeField] private int targetSceneIndex = -1; // -1 = use name instead

    [Header("UI (Optional)")]
    [SerializeField] private TextMeshProUGUI debugText;

    private Button button;
    private int clickCount = 0;

    private void Awake()
    {
        button = GetComponent<Button>();

        // Hook up button click
        button.onClick.AddListener(OnButtonClick);
    }

    private void Update()
    {
        // Update debug text every frame
        if (debugText != null && SceneTransitionManager.Instance != null)
        {
            debugText.text = $"Clicks: {clickCount}\n" +
                           $"Transitioning: {SceneTransitionManager.Instance.IsTransitioning()}\n" +
                           $"Current: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}";
        }
    }

    private void OnButtonClick()
    {
        clickCount++;

        Debug.Log("======================");
        Debug.Log($"[DebugButton] BUTTON CLICKED (#{clickCount})");
        Debug.Log($"[DebugButton] Button: {gameObject.name}");
        Debug.Log($"[DebugButton] Current Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");

        // Check if manager exists
        if (SceneTransitionManager.Instance == null)
        {
            Debug.LogError("[DebugButton] SceneTransitionManager.Instance is NULL!");
            return;
        }

        // Check current state
        bool isTransitioning = SceneTransitionManager.Instance.IsTransitioning();
        Debug.Log($"[DebugButton] Is Transitioning: {isTransitioning}");

        if (isTransitioning)
        {
            Debug.LogWarning("[DebugButton] Already transitioning - BLOCKED");
            Debug.LogWarning("[DebugButton] This means the previous transition hasn't finished!");
            Debug.LogWarning("[DebugButton] Attempting force reset...");

            SceneTransitionManager.Instance.ForceResetTransitionState();
            return;
        }

        // Attempt transition
        if (targetSceneIndex >= 0)
        {
            Debug.Log($"[DebugButton] Attempting to load scene index: {targetSceneIndex}");
            SceneTransitionManager.Instance.LoadScene(targetSceneIndex);
        }
        else if (!string.IsNullOrEmpty(targetSceneName))
        {
            Debug.Log($"[DebugButton] Attempting to load scene: {targetSceneName}");
            SceneTransitionManager.Instance.LoadScene(targetSceneName);
        }
        else
        {
            Debug.LogError("[DebugButton] No target scene set! Set either name or index in Inspector.");
        }

        Debug.Log("======================");
    }

    // Public methods for Unity button events
    public void LoadIntro() => LoadByIndex(0);
    public void LoadLogin() => LoadByIndex(1);
    public void LoadMainMenu() => LoadByIndex(2);
    public void LoadLobby() => LoadByIndex(3);
    public void LoadGameplay() => LoadByIndex(4);
    public void LoadNextScene() => SceneTransitionManager.Instance?.LoadNextScene();

    private void LoadByIndex(int index)
    {
        targetSceneIndex = index;
        OnButtonClick();
    }

    private void OnDestroy()
    {
        // Clean up listener
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClick);
        }
    }
}

/*
=== HOW TO USE ===

METHOD 1: Auto Click Handler
1. Add this script to your button GameObject
2. In Inspector:
   - Target Scene Name: "MainMenu" (OR)
   - Target Scene Index: 2
3. Don't hook up anything in Button's OnClick - script handles it
4. Press Play and click button - watch Console for detailed logs

METHOD 2: Manual Button Events  
1. Add script to any GameObject (not the button itself)
2. In Button's OnClick():
   - Drag this GameObject
   - Select DebugTransitionButton → LoadMainMenu (or any method)

METHOD 3: With Debug Text
1. Create TextMeshPro text in scene
2. Assign to Debug Text field
3. Text will show real-time state

=== WHAT YOU'LL SEE ===

Every button click shows:
```
======================
[DebugButton] BUTTON CLICKED (#1)
[DebugButton] Button: GoToMainMenuButton
[DebugButton] Current Scene: 01_Login
[DebugButton] Is Transitioning: False
[DebugButton] Attempting to load scene: MainMenu
======================
```

If button stops working, you'll see:
```
[DebugButton] Is Transitioning: True
[DebugButton] Already transitioning - BLOCKED
[DebugButton] This means the previous transition hasn't finished!
```

This tells you the bug is happening!

=== TESTING PROCEDURE ===

1. Add this script to ALL your test buttons
2. Set target scenes in Inspector
3. Press Play
4. Click button 1 → Watch Console
5. Wait for transition to complete
6. Click button 2 → Watch Console
7. Does second click work?

If NO:
- Console will show "Is Transitioning: True"
- This means flag is stuck
- Script will auto-call ForceResetTransitionState()

=== INTERPRETING RESULTS ===

✅ GOOD: Each click shows "Is Transitioning: False"
✅ GOOD: Transitions complete successfully
✅ GOOD: Multiple clicks all work

❌ BAD: Second click shows "Is Transitioning: True"
❌ BAD: "Already transitioning - BLOCKED" message
❌ BAD: Transition starts but never finishes

If you see BAD results, share the Console output with me!
*/