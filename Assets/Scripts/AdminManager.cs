using UnityEngine;
using UnityEngine.SceneManagement;

public class AdminSceneManager : MonoBehaviour
{
    public void OpenAdminMealScene()
    {
        SceneManager.LoadScene("AdminMealScene"); // Make sure this scene exists
    }

    public void OpenAdminWorkoutScene()
    {
        SceneManager.LoadScene("AdminWorkoutScene"); // Make sure this scene exists
    }

    public void Logout()
    {
        PlayerPrefs.DeleteKey("UserID"); // Remove stored login data
        SceneManager.LoadScene("LoginRegisterScene"); // Redirect to login scene
        Debug.Log("✅ Admin logged out successfully.");
    }
}
