using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GoalSelectionManager : MonoBehaviour
{
    [Header("Calorie Popup")]
    public GameObject caloriePopupPanel;
    public TMP_Text caloriePopupText;
    public Button calorieConfirmButton;
    public Button calorieCancelButton;

    [Header("Experience Popup")]
    public GameObject experiencePopupPanel;
    public Button beginnerButton;
    public Button intermediateButton;
    public Button experiencedButton;

    private float tdee;
    private float bmi;
    private float recommendedCalories;
    private string gender;
    private int userID;

    void Start()
    {
        // Retrieve user data
        userID = PlayerPrefs.GetInt("UserID", -1);
        tdee = PlayerPrefs.GetFloat("TDEE", 0);
        bmi = PlayerPrefs.GetFloat("BMI", 0);
        gender = PlayerPrefs.GetString("Gender", "Male"); // Default to Male

        if (userID == -1)
        {
            Debug.LogError("❌ No logged-in user found! Redirecting to login...");
            SceneManager.LoadScene("LoginRegisterScene");
            return;
        }

        if (tdee == 0)
        {
            Debug.LogError("❌ TDEE value is missing or not set correctly!");
            caloriePopupText.text = "TDEE not calculated. Please go back and submit your details.";
            caloriePopupPanel.SetActive(true);
            return;
        }

        // Hide popups at start
        caloriePopupPanel.SetActive(false);
        experiencePopupPanel.SetActive(false);

        // Assign button listeners for calorie popup
        calorieConfirmButton.onClick.AddListener(OnCaloriePopupConfirm);
        calorieCancelButton.onClick.AddListener(OnCaloriePopupCancel);

        // Assign button listeners for experience selection (No confirm/cancel)
        beginnerButton.onClick.AddListener(() => SelectExperienceLevel("Beginner"));
        intermediateButton.onClick.AddListener(() => SelectExperienceLevel("Intermediate"));
        experiencedButton.onClick.AddListener(() => SelectExperienceLevel("Experienced"));
    }

    public void OnGoalButtonClick(string goal)
    {
        float adjustment = 0;

        switch (goal)
        {
            case "Maintain": adjustment = 0; break;
            case "Lose": adjustment = -500; break;
            case "Gain": adjustment = 500; break;
            case "Muscle": adjustment = 300; break;
        }

        if (gender == "Female")
        {
            adjustment *= 0.9f; // Reduce by 10% for females
        }

        recommendedCalories = tdee + adjustment;

        // Save selected goal temporarily
        PlayerPrefs.SetFloat("RecommendedCalories", recommendedCalories);
        PlayerPrefs.SetString("SelectedGoal", goal);
        PlayerPrefs.Save();

        // Show calorie intake popup
        caloriePopupText.text = $"Your recommended daily intake: {recommendedCalories} kcal";
        caloriePopupPanel.SetActive(true);
    }

    // ✅ Confirm in the Calorie Intake Popup
    public void OnCaloriePopupConfirm()
    {
        caloriePopupPanel.SetActive(false);

        string selectedGoal = PlayerPrefs.GetString("SelectedGoal", "");

        if (selectedGoal == "Muscle")
        {
            experiencePopupPanel.SetActive(true);
        }
        else
        {
            SaveUserGoalAndProceed(selectedGoal, "");
        }
    }

    // ❌ Cancel in the Calorie Intake Popup
    public void OnCaloriePopupCancel()
    {
        caloriePopupPanel.SetActive(false);
        Debug.Log("❌ Goal selection canceled.");
    }

    // ✅ Select Experience Level (Now directly proceeds to Home Screen)
    public void SelectExperienceLevel(string level)
    {
        Debug.Log($"✅ Experience Level '{level}' selected!");
        experiencePopupPanel.SetActive(false);

        string selectedGoal = PlayerPrefs.GetString("SelectedGoal", "");
        SaveUserGoalAndProceed(selectedGoal, level);
    }

    void SaveUserGoalAndProceed(string selectedGoal, string experienceLevel)
    {
        if (userID == -1 || string.IsNullOrEmpty(selectedGoal))
        {
            Debug.LogError("❌ No goal or user found!");
            return;
        }

        // ✅ Save user goal & experience in the database
        DatabaseManager.Instance.SaveUserGoal(userID, selectedGoal, recommendedCalories, experienceLevel);

        PlayerPrefs.SetInt("HasCompletedSetup", 1);
        PlayerPrefs.Save();

        Debug.Log($"✅ Goal '{selectedGoal}' with Experience Level '{experienceLevel}' saved! Redirecting to HomeScreen...");
        SceneManager.LoadScene("HomeScreen");
    }
}
