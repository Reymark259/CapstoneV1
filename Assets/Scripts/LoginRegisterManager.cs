using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Data;
using Mono.Data.Sqlite;
using UnityEngine.SceneManagement;
using System.Security.Cryptography;
using System.Text;
using System;
using UnityEngine.EventSystems;

public class LoginRegisterManager : MonoBehaviour
{
    public GameObject registerPopupPanel;
    public TMP_InputField loginUsernameInput, loginPasswordInput;
    public Button loginButton, registerButton;
    public TMP_InputField registerUsernameInput, registerPasswordInput;
    public Button submitButton, cancelButton;

    private string dbPath;

    void Start()
    {
        if (DatabaseManager.Instance == null)
        {
            Debug.LogError("❌ DatabaseManager is missing from the scene!");
            return;
        }

        dbPath = DatabaseManager.Instance.GetDatabasePath();
        Debug.Log("📂 Database Path: " + dbPath);

        registerPopupPanel.SetActive(false);

        // Attach event listeners
        loginButton?.onClick.AddListener(OnClickLogin);
        registerButton?.onClick.AddListener(OnClickRegister);
        submitButton?.onClick.AddListener(OnClickSubmit);
        cancelButton?.onClick.AddListener(OnClickCancel);
        AddEventTrigger(loginUsernameInput.gameObject, EventTriggerType.PointerClick, () => ShowKeyboard(loginUsernameInput));
        AddEventTrigger(loginPasswordInput.gameObject, EventTriggerType.PointerClick, () => ShowKeyboard(loginPasswordInput));
        AddEventTrigger(registerUsernameInput.gameObject, EventTriggerType.PointerClick, () => ShowKeyboard(registerUsernameInput));
        AddEventTrigger(registerPasswordInput.gameObject, EventTriggerType.PointerClick, () => ShowKeyboard(registerPasswordInput));

        InsertTestUser(); // Ensures test users are available
    }

    void ShowKeyboard(TMP_InputField inputField)
    {
        inputField.ActivateInputField(); // Focus on the input field
        inputField.Select(); // Ensure the input field is selected
    }

    void AddEventTrigger(GameObject target, EventTriggerType eventType, Action action)
    {
        EventTrigger trigger = target.GetComponent<EventTrigger>() ?? target.AddComponent<EventTrigger>();
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener((data) => action());
        trigger.triggers.Add(entry);
    }

    void InsertTestUser()
    {
        using (var connection = new SqliteConnection("URI=file:" + dbPath))
        {
            connection.Open();

            // Insert test user
            using (var checkCommand = connection.CreateCommand())
            {
                checkCommand.CommandText = "SELECT COUNT(*) FROM users WHERE LOWER(username) = 'testuser'";
                int userExists = Convert.ToInt32(checkCommand.ExecuteScalar());

                if (userExists == 0)
                {
                    using (var insertCommand = connection.CreateCommand())
                    {
                        insertCommand.CommandText = "INSERT INTO users (username, password, isAdmin) VALUES ('testuser', @password, 0)";
                        insertCommand.Parameters.Add(new SqliteParameter("@password", HashPassword("testpass")));
                        insertCommand.ExecuteNonQuery();
                        Debug.Log("✅ Test user 'testuser' added.");
                    }
                }
            }

            // Insert admin user
            using (var checkAdminCommand = connection.CreateCommand())
            {
                checkAdminCommand.CommandText = "SELECT COUNT(*) FROM users WHERE LOWER(username) = 'admin'";
                int adminExists = Convert.ToInt32(checkAdminCommand.ExecuteScalar());

                if (adminExists == 0)
                {
                    using (var insertAdminCommand = connection.CreateCommand())
                    {
                        insertAdminCommand.CommandText = "INSERT INTO users (username, password, isAdmin) VALUES ('admin', @adminPassword, 1)";
                        insertAdminCommand.Parameters.Add(new SqliteParameter("@adminPassword", HashPassword("admin123")));
                        insertAdminCommand.ExecuteNonQuery();
                        Debug.Log("✅ Admin account 'admin' added.");
                    }
                }
            }

            connection.Close();
        }
    }

    public void OnClickLogin()
    {
        string username = loginUsernameInput.text.Trim().ToLower();
        string password = loginPasswordInput.text.Trim();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Debug.LogWarning("⚠️ Please enter both username and password.");
            return;
        }

        using (var connection = new SqliteConnection("URI=file:" + dbPath))
        {
            connection.Open();
            Debug.Log($"🔍 Searching for user: {username}");

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id, username, password, isAdmin FROM users WHERE LOWER(username) = LOWER(@username) LIMIT 1";
                command.Parameters.Add(new SqliteParameter("@username", username));

                using (IDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int userID = reader.GetInt32(0);
                        string storedUsername = reader["username"].ToString();
                        string storedHashedPassword = reader["password"].ToString();
                        bool isAdmin = reader.GetInt32(3) == 1;

                        Debug.Log($"🔑 Found user: {storedUsername}, UserID: {userID}, Admin: {isAdmin}");
                        string hashedInputPassword = HashPassword(password);

                        if (storedHashedPassword == hashedInputPassword)
                        {
                            Debug.Log("✅ Login successful!");

                            // Store login details
                            PlayerPrefs.SetInt("UserID", userID);
                            PlayerPrefs.SetString("LoggedInUser", storedUsername);
                            PlayerPrefs.SetInt("IsAdmin", isAdmin ? 1 : 0);
                            PlayerPrefs.Save();

                            Debug.Log($"📌 UserID {userID} stored in PlayerPrefs!");

                            if (isAdmin)
                            {
                                SceneManager.LoadScene("AdminScene");
                            }
                            else
                            {
                                OnLoginSuccess(userID);
                            }
                        }
                        else
                        {
                            Debug.LogError("❌ Invalid password.");
                        }
                    }
                    else
                    {
                        Debug.LogError("❌ Invalid username or password. User not found.");
                    }
                }
            }
            connection.Close();
        }
    }

    void OnLoginSuccess(int userID)
    {
        bool hasCompletedSetup = DatabaseManager.Instance.HasUserCompletedSetup(userID);

        if (hasCompletedSetup)
        {
            Debug.Log("✅ User setup already completed. Redirecting to HomeScreen.");
            SceneManager.LoadScene("HomeScreen"); // ✅ Go directly to home
        }
        else
        {
            Debug.Log("⚠️ User setup not completed. Redirecting to UserInputScene.");
            SceneManager.LoadScene("UserInputScene"); // ✅ Ask for data if missing
        }
    }

    public void OnClickRegister()
    {
        registerPopupPanel.SetActive(true);
    }

    public void OnClickSubmit()
    {
        string username = registerUsernameInput.text.Trim().ToLower();
        string password = registerPasswordInput.text.Trim();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Debug.LogWarning("⚠️ Please fill in all fields.");
            return;
        }

        using (var connection = new SqliteConnection("URI=file:" + dbPath))
        {
            connection.Open();

            using (var checkCommand = connection.CreateCommand())
            {
                checkCommand.CommandText = "SELECT COUNT(*) FROM users WHERE LOWER(username) = LOWER(@username)";
                checkCommand.Parameters.Add(new SqliteParameter("@username", username));
                int userExists = Convert.ToInt32(checkCommand.ExecuteScalar());

                if (userExists > 0)
                {
                    Debug.LogWarning("⚠️ Username already exists! Choose a different one.");
                    return;
                }
            }

            int newUserId = -1;

            using (var insertCommand = connection.CreateCommand())
            {
                insertCommand.CommandText = "INSERT INTO users (username, password, isAdmin) VALUES (@username, @password, 0)";
                insertCommand.Parameters.Add(new SqliteParameter("@username", username));
                insertCommand.Parameters.Add(new SqliteParameter("@password", HashPassword(password)));

                insertCommand.ExecuteNonQuery();
                Debug.Log($"✅ User '{username}' registered as a normal user.");
            }

            // Get the newly inserted user's ID
            using (var getUserIdCommand = connection.CreateCommand())
            {
                getUserIdCommand.CommandText = "SELECT id FROM users WHERE LOWER(username) = LOWER(@username)";
                getUserIdCommand.Parameters.Add(new SqliteParameter("@username", username));
                newUserId = Convert.ToInt32(getUserIdCommand.ExecuteScalar());
            }

            connection.Close();

            // Store login details in PlayerPrefs
            PlayerPrefs.SetInt("UserID", newUserId);
            PlayerPrefs.SetString("LoggedInUser", username);
            PlayerPrefs.SetInt("IsAdmin", 0);
            PlayerPrefs.SetInt("HasCompletedSetup", 0); // New users have NOT completed setup
            PlayerPrefs.Save();

            Debug.Log($"📌 UserID {newUserId} stored in PlayerPrefs!");

            // Redirect to UserInputScene (since they are new)
            Debug.Log("⚠️ User setup not completed. Redirecting to UserInputScene.");
            SceneManager.LoadScene("UserInputScene");
        }

        registerPopupPanel.SetActive(false);
    }

    public void OnClickCancel()
    {
        registerPopupPanel.SetActive(false);
    }

    string HashPassword(string password)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }
    }

    // ✅ Logout function (should be placed in HomeScreen script)
    public void Logout()
    {
        PlayerPrefs.DeleteKey("UserID");
        PlayerPrefs.DeleteKey("HasCompletedSetup");  // ✅ Reset progress
        PlayerPrefs.Save();

        Debug.Log("🔄 User logged out. Redirecting to login...");
        SceneManager.LoadScene("LoginRegisterScene");
    }
}