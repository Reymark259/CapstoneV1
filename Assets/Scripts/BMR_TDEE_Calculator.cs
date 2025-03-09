using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Data;
using Mono.Data.Sqlite;

public class UserInputManager : MonoBehaviour
{
    public TMP_InputField weightInput;
    public TMP_InputField heightInput;
    public TMP_InputField ageInput;

    public Button genderButton;
    public Button activityLevelButton;

    public TextMeshProUGUI genderText;
    public TextMeshProUGUI activityLevelText;

    public GameObject genderPopup;
    public GameObject activityLevelPopup;

    public Button submitButton;

    private int userID;
    private string selectedGender;
    private string selectedActivityLevel;

    void Start()
    {
        // Ensure popups are hidden at the start
        genderPopup.SetActive(false);
        activityLevelPopup.SetActive(false);

        userID = PlayerPrefs.GetInt("UserID", -1);
        if (userID == -1)
        {
            Debug.LogError("❌ No logged-in user found! Redirecting to login...");
            SceneManager.LoadScene("LoginRegisterScene");
            return;
        }

        Debug.Log($"✅ Logged-in User ID: {userID}");

        if (PlayerPrefs.GetInt("HasCompletedSetup", 0) == 1)
        {
            Debug.Log("✅ Setup already completed! Redirecting to HomeScene...");
            SceneManager.LoadScene("HomeScreen");
            return;
        }

        genderButton.onClick.AddListener(() => TogglePopup(genderPopup));
        activityLevelButton.onClick.AddListener(() => TogglePopup(activityLevelPopup));
        submitButton.onClick.AddListener(SaveUserData);

        // Restore last selected gender & activity level
        selectedGender = PlayerPrefs.GetString("SelectedGender", "");
        selectedActivityLevel = PlayerPrefs.GetString("SelectedActivityLevel", "");

        genderText.text = string.IsNullOrEmpty(selectedGender) ? "Select Gender" : selectedGender;
        activityLevelText.text = string.IsNullOrEmpty(selectedActivityLevel) ? "Select Activity" : selectedActivityLevel;

        LoadUserData();
    }

    void Update()
    {
        // Press ESC to close any open popup
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            genderPopup.SetActive(false);
            activityLevelPopup.SetActive(false);
        }
    }

    void LoadUserData()
    {
        using (var connection = new SqliteConnection("URI=file:" + DatabaseManager.Instance.GetDatabasePath()))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT weight, height, age, gender, activity_level FROM user_data WHERE user_id = @userID";
                command.Parameters.AddWithValue("@userID", userID);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        weightInput.text = reader["weight"].ToString();
                        heightInput.text = reader["height"].ToString();
                        ageInput.text = reader["age"].ToString();

                        string dbGender = reader["gender"].ToString();
                        string dbActivityLevel = reader["activity_level"].ToString();

                        // Only update text if it's not empty
                        if (!string.IsNullOrEmpty(dbGender))
                        {
                            selectedGender = dbGender;
                            genderText.text = dbGender;
                        }

                        if (!string.IsNullOrEmpty(dbActivityLevel))
                        {
                            selectedActivityLevel = dbActivityLevel;
                            activityLevelText.text = dbActivityLevel;
                        }

                        Debug.Log("✅ User data loaded successfully!");
                    }
                    else
                    {
                        Debug.Log("ℹ️ No previous user data found. User must enter details.");
                    }
                }
            }
        }
    }

    // Toggle Popup Visibility (Open/Close)
    void TogglePopup(GameObject popup)
    {
        popup.SetActive(!popup.activeSelf);
    }

    // Select Gender & Close Popup
    public void SelectGender(string gender)
    {
        Debug.Log($"🔹 SelectGender called with gender: {gender}"); // Check the value of gender

        if (string.IsNullOrEmpty(gender))
        {
            Debug.LogError("❌ Gender parameter is empty or null!");
            return;
        }

        selectedGender = gender;
        PlayerPrefs.SetString("SelectedGender", gender);
        PlayerPrefs.Save();

        if (genderText != null)
        {
            Debug.Log($"🔹 Before Update: {genderText.text}");

            genderText.text = gender;  // Update text
            Debug.Log($"✅ Button Text Updated to: {genderText.text}");

            // Force UI Refresh
            genderText.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(genderText.GetComponent<RectTransform>());
        }
        else
        {
            Debug.LogError("❌ GenderText is NULL! Check Inspector Assignment.");
        }

        genderPopup.SetActive(false);
        Debug.Log($"🔹 Gender popup closed. Current selected gender: {selectedGender}");
    }

    // Select Activity Level & Close Popup
    public void SelectActivityLevel(string activity)
    {
        Debug.Log($"🔹 SelectActivityLevel called with activity: {activity}"); // Check the value of activity

        if (string.IsNullOrEmpty(activity))
        {
            Debug.LogError("❌ Activity parameter is empty or null!");
            return;
        }

        selectedActivityLevel = activity;
        PlayerPrefs.SetString("SelectedActivityLevel", activity);
        PlayerPrefs.Save();

        if (activityLevelText != null)
        {
            Debug.Log($"🔹 Before Update: {activityLevelText.text}");

            activityLevelText.text = activity;  // Update text
            Debug.Log($"✅ Button Text Updated to: {activityLevelText.text}");

            // Force UI Refresh
            activityLevelText.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(activityLevelText.GetComponent<RectTransform>());
        }
        else
        {
            Debug.LogError("❌ ActivityLevelText is NULL! Check Inspector Assignment.");
        }

        activityLevelPopup.SetActive(false);
        Debug.Log($"🔹 Activity popup closed. Current selected activity: {selectedActivityLevel}");
    }

    void SaveUserData()
    {
        if (!ValidateInput()) return;

        float weight = float.Parse(weightInput.text);
        float height = float.Parse(heightInput.text);
        int age = int.Parse(ageInput.text);

        if (string.IsNullOrEmpty(selectedGender) || string.IsNullOrEmpty(selectedActivityLevel))
        {
            Debug.LogError("❌ Please select gender and activity level.");
            return;
        }

        float bmr = CalculateBMR(weight, height, age, selectedGender);
        float tdee = CalculateTDEE(bmr, selectedActivityLevel);

        DatabaseManager.Instance.InsertOrUpdateUserData(userID, weight, height, age, selectedGender, selectedActivityLevel, bmr, tdee);

        PlayerPrefs.SetInt("HasCompletedSetup", 1);
        PlayerPrefs.Save();

        Debug.Log($"✅ User data saved! Moving to GoalSelectionScene... BMR: {bmr}, TDEE: {tdee}");

        SceneManager.LoadScene("GoalSelectionScene");
    }

    bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(weightInput.text) ||
            string.IsNullOrWhiteSpace(heightInput.text) ||
            string.IsNullOrWhiteSpace(ageInput.text))
        {
            Debug.LogError("❌ Please fill in all fields.");
            return false;
        }

        if (!float.TryParse(weightInput.text, out _) ||
            !float.TryParse(heightInput.text, out _) ||
            !int.TryParse(ageInput.text, out _))
        {
            Debug.LogError("❌ Invalid input! Ensure numbers are entered correctly.");
            return false;
        }

        return true;
    }

    float CalculateBMR(float weight, float height, int age, string gender)
    {
        if (gender == "Male")
        {
            return 88.36f + (13.4f * weight) + (4.8f * height) - (5.7f * age);
        }
        else
        {
            return 447.6f + (9.2f * weight) + (3.1f * height) - (4.3f * age);
        }
    }

    float CalculateTDEE(float bmr, string activityLevel)
    {
        switch (activityLevel)
        {
            case "Sedentary": return bmr * 1.2f;
            case "Lightly active": return bmr * 1.375f;
            case "Moderately active": return bmr * 1.55f;
            case "Very active": return bmr * 1.725f;
            default:
                Debug.LogError("❌ Invalid activity level!");
                return bmr;
        }
    }
}