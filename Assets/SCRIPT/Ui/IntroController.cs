using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// FIXED VERSION - Video now displays using RawImage on Canvas
/// More reliable than Camera Near Plane method
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
public class IntroController : MonoBehaviour
{
    [Header("Video Settings")]
    [SerializeField] private VideoClip introVideo;
    [SerializeField] private bool skipEnabled = false;

    [Header("Transition")]
    [SerializeField] private float delayAfterVideo = 0.5f;

    [Header("UI (Auto-Created if null)")]
    [SerializeField] private Canvas videoCanvas;
    [SerializeField] private RawImage videoDisplay;

    private VideoPlayer videoPlayer;
    private RenderTexture renderTexture;
    private bool hasTransitioned = false;

    #region Unity Lifecycle

    private void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();

        if (videoPlayer == null)
        {
            Debug.LogError("[IntroController] VideoPlayer component missing!");
            return;
        }

        // Create UI for video display
        SetupVideoDisplay();

        // Configure video player
        SetupVideoPlayer();
    }

    private void Start()
    {
        StartIntroSequence();
    }

    private void Update()
    {
        if (skipEnabled && Input.anyKeyDown && !hasTransitioned)
        {
            SkipToLogin();
        }
    }

    #endregion

    #region Video Display Setup

    /// <summary>
    /// Create Canvas and RawImage to display video
    /// This is MORE RELIABLE than Camera Near Plane
    /// </summary>
    private void SetupVideoDisplay()
    {
        // Create canvas if not assigned
        if (videoCanvas == null)
        {
            GameObject canvasObj = new GameObject("VideoCanvas");
            videoCanvas = canvasObj.AddComponent<Canvas>();
            videoCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            videoCanvas.sortingOrder = 0; // Below fade canvas

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Create RawImage if not assigned
        if (videoDisplay == null)
        {
            GameObject imageObj = new GameObject("VideoDisplay");
            imageObj.transform.SetParent(videoCanvas.transform, false);

            videoDisplay = imageObj.AddComponent<RawImage>();
            videoDisplay.color = Color.white;

            // Make it fill screen
            RectTransform rect = imageObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        // Create RenderTexture for video output
        renderTexture = new RenderTexture(1920, 1080, 0);
        renderTexture.Create();

        // Assign RenderTexture to RawImage
        videoDisplay.texture = renderTexture;

        Debug.Log("[IntroController] Video display created successfully");
    }

    #endregion

    #region Video Player Setup

    /// <summary>
    /// Configure VideoPlayer to render to RenderTexture
    /// </summary>
    private void SetupVideoPlayer()
    {
        if (introVideo != null)
        {
            videoPlayer.clip = introVideo;
        }
        else
        {
            Debug.LogWarning("[IntroController] Video clip not assigned!");
        }

        // CRITICAL: Render to texture, not camera
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.skipOnDrop = true;

        // Video finished event
        videoPlayer.loopPointReached += OnVideoFinished;

        // Audio setup
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        if (videoPlayer.controlledAudioTrackCount > 0)
        {
            videoPlayer.SetDirectAudioVolume(0, 0.7f); // 70% volume
        }

        Debug.Log("[IntroController] VideoPlayer configured - Render to texture");
    }

    #endregion

    #region Playback Control

    /// <summary>
    /// Start playing the intro video
    /// </summary>
    private void StartIntroSequence()
    {
        if (videoPlayer == null || videoPlayer.clip == null)
        {
            Debug.LogError("[IntroController] Cannot play - missing player or clip!");
            SkipToLogin();
            return;
        }

        Debug.Log("[IntroController] Starting intro video...");

        // Make sure video display is visible
        if (videoDisplay != null)
        {
            videoDisplay.enabled = true;
        }

        // Prepare video (important for RenderTexture mode)
        videoPlayer.Prepare();
        videoPlayer.prepareCompleted += OnVideoPrepared;
    }

    /// <summary>
    /// Called when video is ready to play
    /// </summary>
    private void OnVideoPrepared(VideoPlayer source)
    {
        Debug.Log("[IntroController] Video prepared, starting playback");
        videoPlayer.Play();
    }

    /// <summary>
    /// Called when video finishes
    /// </summary>
    private void OnVideoFinished(VideoPlayer vp)
    {
        Debug.Log("[IntroController] Video finished");

        if (!hasTransitioned)
        {
            StartCoroutine(TransitionToLogin());
        }
    }

    /// <summary>
    /// Transition to login scene
    /// </summary>
    private IEnumerator TransitionToLogin()
    {
        hasTransitioned = true;

        // Hide video display
        if (videoDisplay != null)
        {
            videoDisplay.enabled = false;
        }

        yield return new WaitForSeconds(delayAfterVideo);

        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.GoToLogin();
        }
        else
        {
            Debug.LogError("[IntroController] SceneTransitionManager not found!");
        }
    }

    /// <summary>
    /// Skip video and go to login
    /// </summary>
    private void SkipToLogin()
    {
        if (hasTransitioned) return;

        hasTransitioned = true;

        Debug.Log("[IntroController] Skipping video");

        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }

        if (videoDisplay != null)
        {
            videoDisplay.enabled = false;
        }

        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.GoToLogin();
        }
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        // Clean up events
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.prepareCompleted -= OnVideoPrepared;
        }

        // Release RenderTexture
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }

    #endregion
}

/*
=== WHAT WAS FIXED ===

PROBLEM: Video not visible on screen
ROOT CAUSE: Camera Near Plane mode can be unreliable

FIX: Changed to RenderTexture + RawImage method
- Video renders to RenderTexture
- RawImage displays the texture on Canvas
- Much more reliable, especially on mobile

=== HOW TO UPDATE ===

1. OPEN 00_Intro scene
2. SELECT IntroVideoPlayer GameObject
3. REMOVE old IntroController script
4. ADD this new IntroController script
5. In Inspector:
   - Intro Video: [Drag your MP4 here]
   - Skip Enabled: Unchecked
   - Leave UI fields empty (auto-creates)

6. DELETE any manually created Canvas/RawImage
7. Press Play

=== WHAT YOU SHOULD SEE ===

Console should show:
✅ "Video display created successfully"
✅ "VideoPlayer configured"
✅ "Starting intro video..."
✅ "Video prepared, starting playback"
✅ "Video finished"
✅ Then transition to Login scene

AND MOST IMPORTANTLY:
✅ You should SEE the video playing on screen!

=== COMMON VIDEO ISSUES ===

"Still not showing":
1. Check video file is MP4 format
2. Check it's actually assigned in Inspector
3. Check Console for errors
4. Try re-importing video (select in Project → Reimport)

"Video stutters":
- Select video in Project window
- Inspector → Transcode: Checked
- Codec: H264
- Quality: Medium

"Wrong aspect ratio":
- Video will automatically scale to fit screen
- Black bars if aspect ratios don't match

=== TESTING CHECKLIST ===
□ Video visible on screen
□ Video plays for full 5 seconds
□ Audio plays (if video has audio)
□ After video, fades to next scene
□ No errors in Console
□ Fade canvas appears AFTER video
*/