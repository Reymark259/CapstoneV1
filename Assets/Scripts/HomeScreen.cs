using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HomeScreen : MonoBehaviour
{
    public Button logoutButton; // 🔹 Reference to the Logout Button
    public Button tvButton; // 🔹 Reference to the TV Button
    public GameObject tvPopupPanel; // 🔹 Reference to the TV Pop-up Panel
    public Button mealButton, exerciseButton, progressButton, backButton; // 🔹 Buttons inside pop-up

    void Start()
    {
        // 🔹 Ensure logout button works when clicked
        if (logoutButton != null)
        {
            logoutButton.onClick.AddListener(Logout);
        }

        // 🔹 Ensure TV button shows pop-up
        if (tvButton != null && tvPopupPanel != null)
        {
            tvButton.onClick.AddListener(ToggleTVPopup);
            tvPopupPanel.SetActive(false); // Hide popup initially
        }

        // 🔹 Assign functions to buttons inside pop-up
        if (mealButton != null) mealButton.onClick.AddListener(GoToMeals);
        if (exerciseButton != null) exerciseButton.onClick.AddListener(GoToExercise);
        if (progressButton != null) progressButton.onClick.AddListener(GoToProgress);
        if (backButton != null) backButton.onClick.AddListener(CloseTVPopup);
    }

    // ✅ Toggle TV Pop-up visibility
    public void ToggleTVPopup()
    {
        tvPopupPanel.SetActive(true);
    }

    // ✅ Close TV Pop-up
    public void CloseTVPopup()
    {
        tvPopupPanel.SetActive(false);
    }

    // ✅ Navigate to Meals Scene
    public void GoToMeals()
    {
        SceneManager.LoadScene("MealsScene");
    }

    // ✅ Navigate to Exercise Scene
    public void GoToExercise()
    {
        SceneManager.LoadScene("ExerciseScene");
    }

    // ✅ Navigate to Progress Scene
    public void GoToProgress()
    {
        SceneManager.LoadScene("ProgressScene");
    }

    // ✅ Logout Function
    public void Logout()
    {
        PlayerPrefs.DeleteKey("UserID");
        PlayerPrefs.DeleteKey("HasCompletedSetup"); // Reset progress
        PlayerPrefs.Save();

        Debug.Log("🔄 User logged out. Redirecting to login...");
        SceneManager.LoadScene("LoginRegisterScene");
    }
}
