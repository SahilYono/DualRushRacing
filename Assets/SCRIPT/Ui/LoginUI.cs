using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Complete login UI with multiple authentication methods:
/// - Email/Password (Sign Up & Sign In)
/// - Persistent Guest
/// - Facebook
/// - Google
/// </summary>
public class LoginUI : MonoBehaviour
{
    [Header("Main Login Panel")]
    [SerializeField] private GameObject mainLoginPanel;
    [SerializeField] private Button emailLoginButton;
    [SerializeField] private Button guestLoginButton;
    [SerializeField] private Button facebookLoginButton;
    [SerializeField] private Button googleLoginButton;
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("Email Login Panel")]
    [SerializeField] private GameObject emailLoginPanel;
    [SerializeField] private TMP_InputField emailInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private Button signInButton;
    [SerializeField] private Button signUpButton;
    [SerializeField] private Button backFromEmailButton;
    [SerializeField] private Toggle showPasswordToggle;
    [SerializeField] private TextMeshProUGUI emailStatusText;

    [Header("Loading & Status")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Settings")]
    [SerializeField] private bool enableSocialLogin = false; // Set true when SDKs installed

    private bool isLoggingIn = false;

    #region Unity Lifecycle

    private void Start()
    {
        InitializeUI();
        SetupButtonListeners();
        CheckAutoLogin();
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
    }

    #endregion

    #region Initialization

    private void InitializeUI()
    {
        // Hide all panels except main
        ShowPanel(mainLoginPanel);
        HidePanel(emailLoginPanel);
        HidePanel(loadingPanel);

        // Configure social buttons visibility
        if (!enableSocialLogin)
        {
            if (facebookLoginButton != null)
                facebookLoginButton.gameObject.SetActive(false);
            if (googleLoginButton != null)
                googleLoginButton.gameObject.SetActive(false);
        }

        // Set initial status
        UpdateStatusText("Welcome to Dual Rush Racing!");

        // Configure password field
        if (passwordInputField != null)
        {
            passwordInputField.contentType = TMP_InputField.ContentType.Password;
        }

        Debug.Log("[LoginUI] Initialized");
    }

    private void SetupButtonListeners()
    {
        // Main panel buttons
        if (emailLoginButton != null)
            emailLoginButton.onClick.AddListener(OnEmailLoginButtonClicked);

        if (guestLoginButton != null)
            guestLoginButton.onClick.AddListener(OnGuestLoginClicked);

        if (facebookLoginButton != null)
            facebookLoginButton.onClick.AddListener(OnFacebookLoginClicked);

        if (googleLoginButton != null)
            googleLoginButton.onClick.AddListener(OnGoogleLoginClicked);

        // Email panel buttons
        if (signInButton != null)
            signInButton.onClick.AddListener(OnSignInClicked);

        if (signUpButton != null)
            signUpButton.onClick.AddListener(OnSignUpClicked);

        if (backFromEmailButton != null)
            backFromEmailButton.onClick.AddListener(OnBackFromEmailClicked);

        // Show/hide password toggle
        if (showPasswordToggle != null)
            showPasswordToggle.onValueChanged.AddListener(OnShowPasswordToggled);
    }

    private void RemoveButtonListeners()
    {
        if (emailLoginButton != null) emailLoginButton.onClick.RemoveAllListeners();
        if (guestLoginButton != null) guestLoginButton.onClick.RemoveAllListeners();
        if (facebookLoginButton != null) facebookLoginButton.onClick.RemoveAllListeners();
        if (googleLoginButton != null) googleLoginButton.onClick.RemoveAllListeners();
        if (signInButton != null) signInButton.onClick.RemoveAllListeners();
        if (signUpButton != null) signUpButton.onClick.RemoveAllListeners();
        if (backFromEmailButton != null) backFromEmailButton.onClick.RemoveAllListeners();
        if (showPasswordToggle != null) showPasswordToggle.onValueChanged.RemoveAllListeners();
    }

    #endregion

    #region Auto Login Check

    private void CheckAutoLogin()
    {
        StartCoroutine(CheckAutoLoginCoroutine());
    }

    private IEnumerator CheckAutoLoginCoroutine()
    {
        // Wait for AuthenticationManager
        float timeout = 5f;
        float elapsed = 0f;

        while (AuthenticationManager.Instance == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (AuthenticationManager.Instance == null)
        {
            UpdateStatusText("Error: Authentication system not found!");
            yield break;
        }

        // Wait for Unity Services
        yield return new WaitForSeconds(1f);

        // Check if already authenticated
        if (AuthenticationManager.Instance.IsAuthenticated)
        {
            Debug.Log("[LoginUI] Player already authenticated, proceeding to main menu...");
            UpdateStatusText("Welcome back!");
            yield return new WaitForSeconds(1f);
            ProceedToMainMenu();
        }
    }

    #endregion

    #region Main Panel Button Handlers

    /// <summary>
    /// Open email login panel
    /// </summary>
    private void OnEmailLoginButtonClicked()
    {
        PlayButtonSound();
        Debug.Log("[LoginUI] Email login button clicked");

        HidePanel(mainLoginPanel);
        ShowPanel(emailLoginPanel);

        // Clear previous input
        if (emailInputField != null) emailInputField.text = "";
        if (passwordInputField != null) passwordInputField.text = "";
        if (emailStatusText != null) emailStatusText.text = "";
    }

    /// <summary>
    /// Guest login with persistent device ID
    /// </summary>
    private async void OnGuestLoginClicked()
    {
        if (isLoggingIn) return;

        PlayButtonSound();
        Debug.Log("[LoginUI] Guest login clicked");

        isLoggingIn = true;
        ShowLoadingState(true, "Signing in as guest...");

        await System.Threading.Tasks.Task.Yield();

        try
        {
            bool success = await AuthenticationManager.Instance.SignInAsPersistentGuest();

            if (success)
            {
                Debug.Log("[LoginUI] Guest login successful!");
                UpdateStatusText("Login successful!");

                await System.Threading.Tasks.Task.Delay(1000);
                ProceedToMainMenu();
            }
            else
            {
                Debug.LogError("[LoginUI] Guest login failed!");
                UpdateStatusText("Login failed. Please try again.");
                ShowLoadingState(false);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LoginUI] Login error: {ex.Message}");
            UpdateStatusText("An error occurred. Please try again.");
            ShowLoadingState(false);
        }
        finally
        {
            isLoggingIn = false;
        }
    }

    /// <summary>
    /// Facebook login
    /// </summary>
    private async void OnFacebookLoginClicked()
    {
        if (isLoggingIn) return;

        PlayButtonSound();
        Debug.Log("[LoginUI] Facebook login clicked");

        isLoggingIn = true;
        ShowLoadingState(true, "Connecting to Facebook...");

        await System.Threading.Tasks.Task.Yield();

        try
        {
            bool success = await AuthenticationManager.Instance.SignInWithFacebook();

            if (success)
            {
                Debug.Log("[LoginUI] Facebook login successful!");
                UpdateStatusText("Facebook login successful!");

                await System.Threading.Tasks.Task.Delay(1000);
                ProceedToMainMenu();
            }
            else
            {
                Debug.LogError("[LoginUI] Facebook login failed!");
                UpdateStatusText("Facebook login failed.");
                ShowLoadingState(false);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LoginUI] Facebook error: {ex.Message}");
            UpdateStatusText($"Error: {ex.Message}");
            ShowLoadingState(false);
        }
        finally
        {
            isLoggingIn = false;
        }
    }

    /// <summary>
    /// Google login
    /// </summary>
    private async void OnGoogleLoginClicked()
    {
        if (isLoggingIn) return;

        PlayButtonSound();
        Debug.Log("[LoginUI] Google login clicked");

        isLoggingIn = true;
        ShowLoadingState(true, "Connecting to Google...");

        await System.Threading.Tasks.Task.Yield();

        try
        {
            bool success = await AuthenticationManager.Instance.SignInWithGoogle();

            if (success)
            {
                Debug.Log("[LoginUI] Google login successful!");
                UpdateStatusText("Google login successful!");

                await System.Threading.Tasks.Task.Delay(1000);
                ProceedToMainMenu();
            }
            else
            {
                Debug.LogError("[LoginUI] Google login failed!");
                UpdateStatusText("Google login failed.");
                ShowLoadingState(false);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LoginUI] Google error: {ex.Message}");
            UpdateStatusText($"Error: {ex.Message}");
            ShowLoadingState(false);
        }
        finally
        {
            isLoggingIn = false;
        }
    }

    #endregion

    #region Email Panel Button Handlers

    /// <summary>
    /// Sign in with existing account
    /// </summary>
    private async void OnSignInClicked()
    {
        if (isLoggingIn) return;

        PlayButtonSound();

        string email = emailInputField.text.Trim();
        string password = passwordInputField.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            UpdateEmailStatus("Please fill in all fields!", Color.red);
            return;
        }

        Debug.Log($"[LoginUI] Signing in with email: {email}");

        isLoggingIn = true;
        ShowLoadingState(true, "Signing in...");

        await System.Threading.Tasks.Task.Yield();

        try
        {
            bool success = await AuthenticationManager.Instance.SignInWithEmail(email, password);

            if (success)
            {
                Debug.Log("[LoginUI] Email sign in successful!");
                UpdateStatusText("Sign in successful!");

                await System.Threading.Tasks.Task.Delay(1000);
                ProceedToMainMenu();
            }
            else
            {
                Debug.LogError("[LoginUI] Email sign in failed!");
                UpdateEmailStatus("Invalid email or password!", Color.red);
                ShowLoadingState(false);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LoginUI] Sign in error: {ex.Message}");
            UpdateEmailStatus(ex.Message, Color.red);
            ShowLoadingState(false);
        }
        finally
        {
            isLoggingIn = false;
        }
    }

    /// <summary>
    /// Sign up new account
    /// </summary>
    private async void OnSignUpClicked()
    {
        if (isLoggingIn) return;

        PlayButtonSound();

        string email = emailInputField.text.Trim();
        string password = passwordInputField.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            UpdateEmailStatus("Please fill in all fields!", Color.red);
            return;
        }

        Debug.Log($"[LoginUI] Signing up with email: {email}");

        isLoggingIn = true;
        ShowLoadingState(true, "Creating account...");

        await System.Threading.Tasks.Task.Yield();

        try
        {
            bool success = await AuthenticationManager.Instance.SignUpWithEmail(email, password);

            if (success)
            {
                Debug.Log("[LoginUI] Email sign up successful!");
                UpdateStatusText("Account created successfully!");

                await System.Threading.Tasks.Task.Delay(1000);
                ProceedToMainMenu();
            }
            else
            {
                Debug.LogError("[LoginUI] Email sign up failed!");
                UpdateEmailStatus("Failed to create account. Try different email.", Color.red);
                ShowLoadingState(false);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LoginUI] Sign up error: {ex.Message}");
            UpdateEmailStatus(ex.Message, Color.red);
            ShowLoadingState(false);
        }
        finally
        {
            isLoggingIn = false;
        }
    }

    /// <summary>
    /// Back to main login screen
    /// </summary>
    private void OnBackFromEmailClicked()
    {
        PlayButtonSound();
        Debug.Log("[LoginUI] Back from email clicked");

        HidePanel(emailLoginPanel);
        ShowPanel(mainLoginPanel);
    }

    /// <summary>
    /// Toggle password visibility
    /// </summary>
    private void OnShowPasswordToggled(bool show)
    {
        if (passwordInputField != null)
        {
            passwordInputField.contentType = show ?
                TMP_InputField.ContentType.Standard :
                TMP_InputField.ContentType.Password;
            passwordInputField.ForceLabelUpdate();
        }
    }

    #endregion

    #region UI Updates

    private void ShowLoadingState(bool show, string message = "Loading...")
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(show);
        }

        if (show && loadingText != null)
        {
            loadingText.text = message;
        }

        SetButtonsInteractable(!show);
    }

    private void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        Debug.Log($"[LoginUI] Status: {message}");
    }

    private void UpdateEmailStatus(string message, Color color)
    {
        if (emailStatusText != null)
        {
            emailStatusText.text = message;
            emailStatusText.color = color;
        }

        Debug.Log($"[LoginUI] Email Status: {message}");
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (emailLoginButton != null) emailLoginButton.interactable = interactable;
        if (guestLoginButton != null) guestLoginButton.interactable = interactable;
        if (facebookLoginButton != null) facebookLoginButton.interactable = interactable;
        if (googleLoginButton != null) googleLoginButton.interactable = interactable;
        if (signInButton != null) signInButton.interactable = interactable;
        if (signUpButton != null) signUpButton.interactable = interactable;
    }

    private void ShowPanel(GameObject panel)
    {
        if (panel != null) panel.SetActive(true);
    }

    private void HidePanel(GameObject panel)
    {
        if (panel != null) panel.SetActive(false);
    }

    #endregion

    #region Navigation

    private void ProceedToMainMenu()
    {
        Debug.Log("[LoginUI] Transitioning to main menu...");

        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.GoToMainMenu();
        }
        else
        {
            Debug.LogError("[LoginUI] SceneTransitionManager not found!");
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMainMenuMusic();
        }
    }

    #endregion

    #region Utility

    private void PlayButtonSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }
    }

    #endregion
}

/*
=== COMPLETE LOGIN UI SETUP ===

1. OPEN 01_Login SCENE

2. CREATE MAIN LOGIN PANEL:
   Canvas → Panel → "MainLoginPanel"
   
   Inside MainLoginPanel:
   
   A. TITLE:
      - Text: "DUAL RUSH RACING"
      - Font Size: 70
      - Top center
   
   B. SUBTITLE:
      - Text: "Choose Login Method"
      - Font Size: 30
      - Below title
   
   C. EMAIL LOGIN BUTTON:
      - Button: "EmailLoginButton"
      - Text: "LOGIN WITH EMAIL"
      - Size: 500x80
      - Color: Blue (#2196F3)
      - Icon: Email icon (optional)
   
   D. GUEST LOGIN BUTTON:
      - Button: "GuestLoginButton"
      - Text: "CONTINUE AS GUEST"
      - Size: 500x80
      - Color: Green (#4CAF50)
      - Below email button
   
   E. FACEBOOK LOGIN BUTTON:
      - Button: "FacebookLoginButton"
      - Text: "LOGIN WITH FACEBOOK"
      - Size: 500x80
      - Color: Facebook Blue (#3B5998)
      - Facebook icon
   
   F. GOOGLE LOGIN BUTTON:
      - Button: "GoogleLoginButton"
      - Text: "LOGIN WITH GOOGLE"
      - Size: 500x80
      - Color: Google Red (#DB4437)
      - Google icon
   
   G. STATUS TEXT:
      - Text: "StatusText"
      - Font Size: 25
      - Bottom center

3. CREATE EMAIL LOGIN PANEL:
   Canvas → Panel → "EmailLoginPanel"
   
   Inside EmailLoginPanel:
   
   A. TITLE:
      - Text: "EMAIL LOGIN"
      - Font Size: 50
   
   B. EMAIL INPUT FIELD:
      - InputField: "EmailInputField"
      - Placeholder: "Enter your email"
      - Content Type: Email Address
      - Font Size: 30
   
   C. PASSWORD INPUT FIELD:
      - InputField: "PasswordInputField"
      - Placeholder: "Enter password"
      - Content Type: Password
      - Font Size: 30
   
   D. SHOW PASSWORD TOGGLE:
      - Toggle: "ShowPasswordToggle"
      - Label: "Show Password"
   
   E. SIGN IN BUTTON:
      - Button: "SignInButton"
      - Text: "SIGN IN"
      - Size: 350x70
      - Color: Blue
   
   F. SIGN UP BUTTON:
      - Button: "SignUpButton"
      - Text: "CREATE ACCOUNT"
      - Size: 350x70
      - Color: Green
   
   G. BACK BUTTON:
      - Button: "BackFromEmailButton"
      - Text: "← BACK"
      - Size: 200x60
      - Bottom-left
   
   H. EMAIL STATUS TEXT:
      - Text: "EmailStatusText"
      - Font Size: 25
      - Color: Red/Green (changes dynamically)

4. CREATE LOADING PANEL:
   Canvas → Panel → "LoadingPanel"
   - Full screen overlay
   - Semi-transparent black
   - Text: "LoadingText"
   - Loading spinner (optional)

5. ADD SCRIPT:
   - Create Empty: "LoginController"
   - Add LoginUI script
   - Assign ALL references!

6. CONFIGURE SETTINGS:
   - Enable Social Login: ✅ FALSE (for now)
   - Set to TRUE when Facebook/Google SDKs installed

=== LAYOUT DIAGRAM ===

Main Login Screen:
┌─────────────────────────────────────┐
│      DUAL RUSH RACING               │
│      Choose Login Method            │
│                                     │
│   ┌─────────────────────┐          │
│   │ LOGIN WITH EMAIL    │          │
│   └─────────────────────┘          │
│                                     │
│   ┌─────────────────────┐          │
│   │ CONTINUE AS GUEST   │          │
│   └─────────────────────┘          │
│                                     │
│   ┌─────────────────────┐          │
│   │ LOGIN WITH FACEBOOK │          │
│   └─────────────────────┘          │
│                                     │
│   ┌─────────────────────┐          │
│   │ LOGIN WITH GOOGLE   │          │
│   └─────────────────────┘          │
│                                     │
│        Welcome message!             │
└─────────────────────────────────────┘

Email Login Panel:
┌─────────────────────────────────────┐
│         EMAIL LOGIN                 │
│                                     │
│   ┌─────────────────────┐          │
│   │ Enter your email... │          │
│   └─────────────────────┘          │
│                                     │
│   ┌─────────────────────┐          │
│   │ Enter password...   │          │
│   └─────────────────────┘          │
│                                     │
│   ☐ Show Password                  │
│                                     │
│   ┌────────┐  ┌────────┐          │
│   │ SIGN IN│  │SIGN UP │          │
│   └────────┘  └────────┘          │
│                                     │
│   Status message here               │
│                                     │
│ [← BACK]                           │
└─────────────────────────────────────┘

=== TESTING WORKFLOW ===

1. GUEST LOGIN TEST:
   - Click "CONTINUE AS GUEST"
   - Should login automatically
   - Note Player ID in console
   - Close game
   - UNINSTALL app
   - REINSTALL app
   - Click "CONTINUE AS GUEST" again
   - Should have SAME Player ID! ✅

2. EMAIL SIGN UP TEST:
   - Click "LOGIN WITH EMAIL"
   - Enter: test@example.com
   - Enter password: password123
   - Click "CREATE ACCOUNT"
   - Should succeed!
   - Logout
   - Sign in with same email
   - Should work! ✅

3. FACEBOOK/GOOGLE TEST (After SDK setup):
   - Click Facebook/Google button
   - Authorize in popup
   - Should auto-login ✅

=== WITHOUT SOCIAL SDKs (For Now) ===

Set "Enable Social Login" = FALSE
- Facebook/Google buttons will be hidden
- Email + Guest fully functional
- Add social later when ready

=== PASSWORD REQUIREMENTS ===
- Minimum 8 characters
- Validation in AuthenticationManager
- Shows error if too short

=== EMAIL VALIDATION ===
- Checks for valid format
- Must contain @ and domain
- Shows error if invalid

=== PERSISTENT GUEST EXPLAINED ===

How it works:
1. Gets device unique identifier
2. Combines with timestamp hash
3. Saves to PlayerPrefs as backup
4. Uses this as custom auth ID
5. Unity Auth creates account with this ID
6. Same ID = same account forever!

Even if user:
- Uninstalls app
- Clears cache
- Resets phone
They get SAME account back! 🎉

=== READY TO TEST! ===
All authentication methods ready:
✅ Email/Password
✅ Persistent Guest
⏳ Facebook (needs SDK)
⏳ Google (needs SDK)
*/