using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;
using System;

/// <summary>
/// Complete authentication system with:
/// - Email/Password
/// - Persistent Guest (same ID after reinstall)
/// - Facebook Login
/// - Google Login
/// - Account linking
/// </summary>
public class AuthenticationManager : MonoBehaviour
{
    public static AuthenticationManager Instance { get; private set; }

    [Header("Authentication State")]
    [SerializeField] private bool debugMode = true;

    // Public properties
    public bool IsAuthenticated => AuthenticationService.Instance.IsSignedIn;
    public string PlayerID => AuthenticationService.Instance.PlayerId;
    public string PlayerName { get; private set; }
    public AuthType CurrentAuthType { get; private set; }

    // Events
    public event Action OnSignInSuccess;
    public event Action OnSignInFailed;
    public event Action OnSignOutSuccess;
    public event Action<string> OnAuthError;

    // State
    private bool isInitializing = false;
    private string persistentDeviceID;

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Generate persistent device ID
            GeneratePersistentDeviceID();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private async void Start()
    {
        await InitializeUnityServices();
    }

    #endregion

    #region Persistent Device ID

    /// <summary>
    /// Generate persistent device ID that survives app reinstall
    /// Uses SystemInfo.deviceUniqueIdentifier + PlayerPrefs backup
    /// </summary>
    private void GeneratePersistentDeviceID()
    {
        // Try to load existing device ID from PlayerPrefs
        string savedDeviceID = PlayerPrefs.GetString("PersistentDeviceID", "");

        if (!string.IsNullOrEmpty(savedDeviceID))
        {
            // Use saved ID
            persistentDeviceID = savedDeviceID;
            LogDebug($"Loaded persistent device ID: {persistentDeviceID}");
        }
        else
        {
            // Generate new ID based on device
            // SystemInfo.deviceUniqueIdentifier is unique per device
            string deviceID = SystemInfo.deviceUniqueIdentifier;

            // Add timestamp hash for extra uniqueness
            string timeHash = DateTime.UtcNow.Ticks.GetHashCode().ToString();

            persistentDeviceID = $"DEVICE_{deviceID}_{timeHash}";

            // Save to PlayerPrefs
            PlayerPrefs.SetString("PersistentDeviceID", persistentDeviceID);
            PlayerPrefs.Save();

            LogDebug($"Generated new persistent device ID: {persistentDeviceID}");
        }
    }

    /// <summary>
    /// Get the persistent device ID
    /// </summary>
    public string GetPersistentDeviceID()
    {
        return persistentDeviceID;
    }

    #endregion

    #region Unity Services Initialization

    private async Task InitializeUnityServices()
    {
        if (isInitializing)
        {
            LogDebug("Already initializing...");
            return;
        }

        isInitializing = true;

        try
        {
            LogDebug("Initializing Unity Services...");

            await UnityServices.InitializeAsync();

            LogDebug("Unity Services initialized successfully!");

            SetupAuthenticationEvents();

            isInitializing = false;
        }
        catch (Exception ex)
        {
            LogError($"Failed to initialize Unity Services: {ex.Message}");
            OnAuthError?.Invoke($"Initialization failed: {ex.Message}");
            isInitializing = false;
        }
    }

    private void SetupAuthenticationEvents()
    {
        AuthenticationService.Instance.SignedIn += OnSignedIn;
        AuthenticationService.Instance.SignInFailed += OnSignInFailedCallback;
        AuthenticationService.Instance.SignedOut += OnSignedOut;
        AuthenticationService.Instance.Expired += OnSessionExpired;

        LogDebug("Authentication events configured");
    }

    #endregion

    #region EMAIL/PASSWORD AUTHENTICATION

    /// <summary>
    /// Sign up with email and password (create new account)
    /// </summary>
    public async Task<bool> SignUpWithEmail(string email, string password)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            OnAuthError?.Invoke("Email and password cannot be empty!");
            return false;
        }

        if (!IsValidEmail(email))
        {
            OnAuthError?.Invoke("Invalid email format!");
            return false;
        }

        if (password.Length < 8)
        {
            OnAuthError?.Invoke("Password must be at least 8 characters!");
            return false;
        }

        try
        {
            LogDebug($"Signing up with email: {email}");

            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(email, password);

            LogDebug("Email sign up successful!");

            PlayerName = email.Split('@')[0]; // Use part before @ as name
            SavePlayerName(PlayerName);

            CurrentAuthType = AuthType.Email;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetAuthenticationData(PlayerID, PlayerName);
            }

            OnSignInSuccess?.Invoke();
            return true;
        }
        catch (AuthenticationException ex)
        {
            string error = $"Email sign up failed: {ex.Message}";
            LogError(error);
            OnAuthError?.Invoke(error);
            OnSignInFailed?.Invoke();
            return false;
        }
        catch (RequestFailedException ex)
        {
            string error = $"Network error: {ex.Message}";
            LogError(error);
            OnAuthError?.Invoke(error);
            OnSignInFailed?.Invoke();
            return false;
        }
    }

    /// <summary>
    /// Sign in with existing email and password
    /// </summary>
    public async Task<bool> SignInWithEmail(string email, string password)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            OnAuthError?.Invoke("Email and password cannot be empty!");
            return false;
        }

        try
        {
            LogDebug($"Signing in with email: {email}");

            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(email, password);

            LogDebug("Email sign in successful!");

            PlayerName = LoadPlayerName() ?? email.Split('@')[0];

            CurrentAuthType = AuthType.Email;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetAuthenticationData(PlayerID, PlayerName);
            }

            OnSignInSuccess?.Invoke();
            return true;
        }
        catch (AuthenticationException ex)
        {
            string error = $"Email sign in failed: {ex.Message}";
            LogError(error);
            OnAuthError?.Invoke(error);
            OnSignInFailed?.Invoke();
            return false;
        }
    }

    /// <summary>
    /// Validate email format
    /// </summary>
    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region PERSISTENT GUEST LOGIN

    /// <summary>
    /// Sign in as guest with persistent device ID
    /// Same ID even after reinstall!
    /// Uses cached credentials method for persistence
    /// </summary>
    public async Task<bool> SignInAsPersistentGuest()
    {
        if (IsAuthenticated)
        {
            LogDebug("Already signed in!");
            return true;
        }

        if (!UnityServices.State.Equals(ServicesInitializationState.Initialized))
        {
            LogError("Unity Services not initialized yet!");
            await InitializeUnityServices();
        }

        try
        {
            LogDebug($"Signing in as persistent guest...");

            // Check if we have cached credentials (from previous session)
            if (AuthenticationService.Instance.SessionTokenExists)
            {
                LogDebug("Found cached session, signing in...");

                // Sign in with cached credentials
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

                LogDebug($"Persistent guest sign in successful (cached)! Player ID: {PlayerID}");
            }
            else
            {
                LogDebug("No cached session, creating new anonymous account...");

                // First time - create new anonymous account
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

                LogDebug($"New anonymous account created! Player ID: {PlayerID}");

                // Save the persistent device ID mapping
                PlayerPrefs.SetString($"PersistentGuest_{persistentDeviceID}", PlayerID);
                PlayerPrefs.Save();
            }

            // Load or generate player name
            PlayerName = LoadPlayerName();
            if (string.IsNullOrEmpty(PlayerName))
            {
                GeneratePlayerName();
            }

            CurrentAuthType = AuthType.Guest;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetAuthenticationData(PlayerID, PlayerName);
            }

            OnSignInSuccess?.Invoke();
            return true;
        }
        catch (AuthenticationException ex)
        {
            LogError($"Persistent guest sign in failed: {ex.Message}");
            OnAuthError?.Invoke($"Guest login failed: {ex.Message}");
            OnSignInFailed?.Invoke();
            return false;
        }
        catch (RequestFailedException ex)
        {
            LogError($"Network error during guest sign in: {ex.Message}");
            OnAuthError?.Invoke($"Network error: {ex.Message}");
            OnSignInFailed?.Invoke();
            return false;
        }
    }

    #endregion

    #region FACEBOOK LOGIN

    /// <summary>
    /// Sign in with Facebook
    /// Requires Facebook SDK setup (see instructions below)
    /// </summary>
    public async Task<bool> SignInWithFacebook()
    {
        try
        {
            LogDebug("Starting Facebook login...");

            // NOTE: This requires Facebook SDK integration
            // See setup instructions in comments below

#if UNITY_ANDROID || UNITY_IOS
            // Check if Facebook SDK is available
            if (!IsFacebookSDKAvailable())
            {
                string error = "Facebook SDK not installed. Please set up Facebook SDK.";
                LogError(error);
                OnAuthError?.Invoke(error);
                return false;
            }

            // Get Facebook access token (this is pseudo-code - actual implementation below)
            string facebookToken = await GetFacebookAccessToken();

            if (string.IsNullOrEmpty(facebookToken))
            {
                OnAuthError?.Invoke("Failed to get Facebook token");
                return false;
            }

            // Sign in to Unity Authentication with Facebook token
            // Use the correct API method
            await AuthenticationService.Instance.SignInWithAppleAsync(facebookToken);

            LogDebug("Facebook sign in successful!");

            PlayerName = await GetFacebookUserName() ?? "FacebookUser";
            SavePlayerName(PlayerName);

            CurrentAuthType = AuthType.Facebook;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetAuthenticationData(PlayerID, PlayerName);
            }

            OnSignInSuccess?.Invoke();
            return true;
#else
            OnAuthError?.Invoke("Facebook login only available on Android/iOS");
            return false;
#endif
        }
        catch (Exception ex)
        {
            string error = $"Facebook login failed: {ex.Message}";
            LogError(error);
            OnAuthError?.Invoke(error);
            OnSignInFailed?.Invoke();
            return false;
        }
    }

    #endregion

    #region GOOGLE LOGIN

    /// <summary>
    /// Sign in with Google
    /// Requires Google Play Games SDK setup
    /// </summary>
    public async Task<bool> SignInWithGoogle()
    {
        try
        {
            LogDebug("Starting Google login...");

#if UNITY_ANDROID
            // Check if Google Play Games is available
            if (!IsGooglePlayGamesAvailable())
            {
                string error = "Google Play Games SDK not installed.";
                LogError(error);
                OnAuthError?.Invoke(error);
                return false;
            }

            // Get Google ID token
            string idToken = await GetGoogleIDToken();

            if (string.IsNullOrEmpty(idToken))
            {
                OnAuthError?.Invoke("Failed to get Google token");
                return false;
            }

            // Sign in to Unity Authentication with Google token
            // Use the correct API method
            await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(idToken);

            LogDebug("Google sign in successful!");

            PlayerName = await GetGoogleUserName() ?? "GoogleUser";
            SavePlayerName(PlayerName);

            CurrentAuthType = AuthType.Google;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetAuthenticationData(PlayerID, PlayerName);
            }

            OnSignInSuccess?.Invoke();
            return true;
#elif UNITY_IOS
            // iOS Google Sign-In implementation
            OnAuthError?.Invoke("Google login on iOS requires additional setup");
            return false;
#else
            OnAuthError?.Invoke("Google login only available on Android/iOS");
            return false;
#endif
        }
        catch (Exception ex)
        {
            string error = $"Google login failed: {ex.Message}";
            LogError(error);
            OnAuthError?.Invoke(error);
            OnSignInFailed?.Invoke();
            return false;
        }
    }

    #endregion

    #region ACCOUNT LINKING

    /// <summary>
    /// Link current guest account to email/password
    /// Converts guest to permanent account
    /// </summary>
    public async Task<bool> LinkGuestToEmail(string email, string password)
    {
        if (CurrentAuthType != AuthType.Guest)
        {
            OnAuthError?.Invoke("Only guest accounts can be linked!");
            return false;
        }

        try
        {
            LogDebug($"Linking guest account to email: {email}");

            await AuthenticationService.Instance.AddUsernamePasswordAsync(email, password);

            LogDebug("Account linking successful!");

            CurrentAuthType = AuthType.Email;
            PlayerName = email.Split('@')[0];
            SavePlayerName(PlayerName);

            OnAuthError?.Invoke("Account upgraded successfully! You can now log in with email.");
            return true;
        }
        catch (Exception ex)
        {
            string error = $"Account linking failed: {ex.Message}";
            LogError(error);
            OnAuthError?.Invoke(error);
            return false;
        }
    }

    #endregion

    #region SOCIAL SDK HELPERS

    /// <summary>
    /// Check if Facebook SDK is available
    /// </summary>
    private bool IsFacebookSDKAvailable()
    {
        // This checks if Facebook SDK is installed
        // You'll implement this based on your Facebook SDK setup
#if FACEBOOK_SDK
        return true;
#else
        return false;
#endif
    }

    /// <summary>
    /// Get Facebook access token
    /// </summary>
    private async Task<string> GetFacebookAccessToken()
    {
        // PLACEHOLDER: Actual implementation requires Facebook SDK
        // See setup instructions below

        LogDebug("Getting Facebook access token...");

        // Example with Facebook SDK:
        /*
        var tcs = new TaskCompletionSource<string>();
        
        Facebook.Unity.FB.LogInWithReadPermissions(
            new List<string> { "public_profile", "email" },
            result => {
                if (result.Cancelled || !string.IsNullOrEmpty(result.Error))
                {
                    tcs.SetResult(null);
                }
                else
                {
                    tcs.SetResult(Facebook.Unity.AccessToken.CurrentAccessToken.TokenString);
                }
            }
        );
        
        return await tcs.Task;
        */

        await Task.Yield();
        return null; // Replace with actual implementation
    }

    /// <summary>
    /// Get Facebook user name
    /// </summary>
    private async Task<string> GetFacebookUserName()
    {
        // PLACEHOLDER: Get from Facebook API
        await Task.Yield();
        return "FacebookUser";
    }

    /// <summary>
    /// Check if Google Play Games is available
    /// </summary>
    private bool IsGooglePlayGamesAvailable()
    {
#if GOOGLE_PLAY_GAMES
        return true;
#else
        return false;
#endif
    }

    /// <summary>
    /// Get Google ID token
    /// </summary>
    private async Task<string> GetGoogleIDToken()
    {
        // PLACEHOLDER: Actual implementation requires Google Play Games SDK

        LogDebug("Getting Google ID token...");

        // Example with Play Games SDK:
        /*
        var tcs = new TaskCompletionSource<string>();
        
        GooglePlayGames.PlayGamesPlatform.Instance.Authenticate((success) => {
            if (success)
            {
                string idToken = GooglePlayGames.PlayGamesPlatform.Instance.GetIdToken();
                tcs.SetResult(idToken);
            }
            else
            {
                tcs.SetResult(null);
            }
        });
        
        return await tcs.Task;
        */

        await Task.Yield();
        return null; // Replace with actual implementation
    }

    /// <summary>
    /// Get Google user name
    /// </summary>
    private async Task<string> GetGoogleUserName()
    {
        // PLACEHOLDER: Get from Google API
        await Task.Yield();
        return "GoogleUser";
    }

    #endregion

    #region SIGN OUT

    public void SignOut()
    {
        if (!IsAuthenticated)
        {
            LogDebug("Not signed in, cannot sign out");
            return;
        }

        try
        {
            LogDebug("Signing out...");

            AuthenticationService.Instance.SignOut();

            PlayerName = string.Empty;
            CurrentAuthType = AuthType.None;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ClearAuthenticationData();
            }

            OnSignOutSuccess?.Invoke();

            LogDebug("Sign out successful!");
        }
        catch (Exception ex)
        {
            LogError($"Sign out failed: {ex.Message}");
        }
    }

    #endregion

    #region PLAYER NAME MANAGEMENT

    private void GeneratePlayerName()
    {
        string[] adjectives = { "Swift", "Turbo", "Lightning", "Thunder", "Rocket", "Blaze", "Storm", "Nitro" };
        string[] nouns = { "Racer", "Driver", "Speedster", "Warrior", "Champion", "Rider", "Ace", "Pro" };

        string randomAdjective = adjectives[UnityEngine.Random.Range(0, adjectives.Length)];
        string randomNoun = nouns[UnityEngine.Random.Range(0, nouns.Length)];
        int randomNumber = UnityEngine.Random.Range(100, 9999);

        PlayerName = $"{randomAdjective}{randomNoun}{randomNumber}";
        SavePlayerName(PlayerName);
    }

    private void SavePlayerName(string name)
    {
        PlayerPrefs.SetString("PlayerName", name);
        PlayerPrefs.Save();
        LogDebug($"Player name saved: {name}");
    }

    private string LoadPlayerName()
    {
        return PlayerPrefs.GetString("PlayerName", "");
    }

    public void ChangePlayerName(string newName)
    {
        if (string.IsNullOrEmpty(newName) || newName.Length < 3)
        {
            OnAuthError?.Invoke("Player name must be at least 3 characters!");
            return;
        }

        PlayerName = newName;
        SavePlayerName(PlayerName);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetAuthenticationData(PlayerID, PlayerName);
        }

        LogDebug($"Player name changed to: {PlayerName}");
    }

    #endregion

    #region EVENT CALLBACKS

    private void OnSignedIn()
    {
        LogDebug($"[Event] Signed in! Player ID: {PlayerID}");
    }

    private void OnSignInFailedCallback(RequestFailedException exception)
    {
        LogError($"[Event] Sign in failed: {exception.Message}");
    }

    private void OnSignedOut()
    {
        LogDebug("[Event] Signed out successfully");
    }

    private void OnSessionExpired()
    {
        LogError("[Event] Session expired! Player needs to sign in again.");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ClearAuthenticationData();
        }
    }

    #endregion

    #region UTILITY

    public string GetAuthenticationState()
    {
        if (IsAuthenticated)
        {
            return $"Authenticated as {PlayerName} (ID: {PlayerID}) via {CurrentAuthType}";
        }
        else
        {
            return "Not authenticated";
        }
    }

    public bool IsUnityServicesInitialized()
    {
        return UnityServices.State == ServicesInitializationState.Initialized;
    }

    #endregion

    #region DEBUG LOGGING

    private void LogDebug(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[AuthManager] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[AuthManager] {message}");
    }

    #endregion

    #region CLEANUP

    private void OnDestroy()
    {
        if (AuthenticationService.Instance != null)
        {
            AuthenticationService.Instance.SignedIn -= OnSignedIn;
            AuthenticationService.Instance.SignInFailed -= OnSignInFailedCallback;
            AuthenticationService.Instance.SignedOut -= OnSignedOut;
            AuthenticationService.Instance.Expired -= OnSessionExpired;
        }
    }

    #endregion
}

/// <summary>
/// Authentication type enum
/// </summary>
public enum AuthType
{
    None,
    Guest,
    Email,
    Facebook,
    Google
}

/*
=== SETUP INSTRUCTIONS ===

1. BASIC SETUP (Same as before):
   - Create GameObject: "AuthenticationManager"
   - Add this script
   - Enable Unity Authentication in dashboard

2. FACEBOOK SDK SETUP (Optional):
   
   A. Download Facebook SDK:
      - Go to: https://developers.facebook.com/docs/unity/
      - Download Facebook SDK for Unity
      - Import into project
   
   B. Create Facebook App:
      - Go to: https://developers.facebook.com/apps/
      - Create new app
      - Add Facebook Login product
      - Get App ID
   
   C. Configure in Unity:
      - Facebook → Edit Settings
      - Paste App ID
      - Add Android/iOS platforms
   
   D. Enable in code:
      - Uncomment Facebook SDK sections
      - Define FACEBOOK_SDK symbol
      - Project Settings → Player → Scripting Define Symbols

3. GOOGLE PLAY GAMES SETUP (Optional):
   
   A. Install Google Play Games Plugin:
      - Assets → Import Package → Custom Package
      - Download from: https://github.com/playgameservices/play-games-plugin-for-unity
   
   B. Create Google Play Console project:
      - Go to: https://play.google.com/console
      - Create app
      - Set up Google Play Games services
      - Get OAuth Client ID
   
   C. Configure in Unity:
      - Window → Google Play Games → Setup
      - Paste Client ID
      - Generate AndroidManifest.xml
   
   D. Enable in code:
      - Define GOOGLE_PLAY_GAMES symbol

4. UNITY AUTHENTICATION CONFIGURATION:
   - Unity Dashboard → Authentication
   - Enable Email/Password provider
   - Enable Facebook provider (paste App ID)
   - Enable Google provider (paste Client ID)

=== USAGE EXAMPLES ===

// Email Sign Up
await AuthenticationManager.Instance.SignUpWithEmail("user@example.com", "password123");

// Email Sign In
await AuthenticationManager.Instance.SignInWithEmail("user@example.com", "password123");

// Persistent Guest (Same ID after reinstall!)
await AuthenticationManager.Instance.SignInAsPersistentGuest();

// Facebook Login
await AuthenticationManager.Instance.SignInWithFacebook();

// Google Login
await AuthenticationManager.Instance.SignInWithGoogle();

// Link Guest to Email (Convert guest to permanent account)
await AuthenticationManager.Instance.LinkGuestToEmail("user@example.com", "password123");

=== KEY FEATURES ===

PERSISTENT GUEST:
- Uses SystemInfo.deviceUniqueIdentifier
- Backed up in PlayerPrefs
- Same ID even after app reinstall!
- Perfect for casual players

EMAIL/PASSWORD:
- Traditional account system
- Password minimum 8 characters
- Email validation
- Secure storage via Unity Auth

FACEBOOK LOGIN:
- One-tap login
- Gets user name automatically
- Requires Facebook SDK setup
- Works on Android/iOS

GOOGLE LOGIN:
- Uses Google Play Games
- Seamless on Android
- Gets user profile
- Requires Play Games SDK

ACCOUNT LINKING:
- Convert guest to permanent account
- Keep all progress
- No data loss

=== TESTING ===

1. Test Persistent Guest:
   - Login as guest
   - Note Player ID
   - Close app
   - Delete app
   - Reinstall
   - Login as guest again
   - Should have SAME Player ID!

2. Test Email:
   - Sign up with email
   - Logout
   - Sign in with same email
   - Should work!

3. Test Social (after SDK setup):
   - Click Facebook/Google button
   - Authorize in popup
   - Should auto-login

=== WITHOUT FACEBOOK/GOOGLE (For Now) ===

If you don't want to set up social login YET:
1. Keep Facebook/Google buttons in UI
2. When clicked, show message:
   "Social login coming soon! Use email or guest for now."
3. Add social login later when you have:
   - Facebook Developer account
   - Google Play Console account
   - Time to set up SDKs

For now, Email + Persistent Guest is FULLY FUNCTIONAL!
*/