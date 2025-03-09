using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Data;
using Mono.Data.Sqlite;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.SceneManagement;

public class WorkoutSceneManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField searchBar;
    public Button openWorkoutPopupButton;
    public Button closeWorkoutPopupButton;
    public GameObject workoutPopup;
    public Transform popUpWorkoutContainer;
    public GameObject popupWorkoutPrefab;

    [Header("Main List UI")]
    public Transform workoutItemsContainer;
    public GameObject workoutItemsPrefab;

    [Header("Result UI")]
    public TMP_Text totalCaloriesBurnedText;
    public TMP_Text recommendedCaloriesText;
    public TMP_Text totalCaloriesText; // Added total calories from MealScene

    private string dbPath;
    private float totalCaloriesBurned = 0;
    private float recommendedCalories = 0;
    private float totalCalories = 0; // New variable for total calories from MealScene

    private List<GameObject> popUpWorkoutItems = new List<GameObject>();
    private List<GameObject> workoutItems = new List<GameObject>();

    [Header("Navigation")]
    public Button goBackButton;

    void Start()
    {
        if (DatabaseManager.Instance != null)
        {
            dbPath = DatabaseManager.Instance.GetDatabasePath();
        }

        if (string.IsNullOrEmpty(dbPath))
        {
            Debug.LogError("❌ Database path is empty! Ensure DatabaseManager is initialized properly.");
            return;
        }

        workoutPopup.SetActive(false);
        openWorkoutPopupButton.onClick.AddListener(ToggleWorkoutPopup);
        closeWorkoutPopupButton.onClick.AddListener(CloseWorkoutPopup);
        searchBar.onValueChanged.AddListener(SearchWorkouts);

        recommendedCalories = PlayerPrefs.GetFloat("RecommendedCalories", 0);
        totalCalories = PlayerPrefs.GetFloat("TotalCalories", 0); // Load total calories from MealScene

        UpdateResultText();

        if (goBackButton != null)
        {
            goBackButton.onClick.AddListener(GoBackToHome);
        }
        else
        {
            Debug.LogError("❌ GoBackButton is not assigned in the Inspector!");
        }
    }

    void GoBackToHome()
    {
        Debug.Log("🔙 Returning to Home Screen...");
        SceneManager.LoadScene("HomeScreen");
    }

    void ToggleWorkoutPopup()
    {
        bool isActive = workoutPopup.activeSelf;
        workoutPopup.SetActive(!isActive);

        if (!isActive)
        {
            LoadPopupWorkouts();
        }
    }

    void LoadPopupWorkouts()
    {
        if (popupWorkoutPrefab == null || popUpWorkoutContainer == null)
        {
            Debug.LogError("❌ popupWorkoutPrefab or popUpWorkoutContainer is not assigned!");
            return;
        }

        foreach (Transform child in popUpWorkoutContainer)
        {
            Destroy(child.gameObject);
        }
        popUpWorkoutItems.Clear();

        List<(int id, string name, int reps, float caloriesBurned)> workoutData = new List<(int, string, int, float)>();

        using (var connection = new SqliteConnection("URI=file:" + dbPath))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id, name, reps, caloriesBurned FROM workouts";
                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int workoutID = reader.GetInt32(0);
                        string workoutName = reader.GetString(1);
                        int reps = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                        float caloriesBurned = reader.IsDBNull(3) ? 0f : reader.GetFloat(3);

                        Debug.Log($"📌 Loaded Workout: {workoutName} | Calories Burned: {caloriesBurned}");
                        workoutData.Add((workoutID, workoutName, reps, caloriesBurned));
                    }
                }
            }
        }

        workoutData.Sort((x, y) => x.name.CompareTo(y.name));

        foreach (var workout in workoutData)
        {
            CreatePopupWorkoutItem(workout.id, workout.name, workout.caloriesBurned);
        }

        StartCoroutine(UpdateLayout());
    }

    void CreatePopupWorkoutItem(int workoutID, string workoutName, float caloriesBurned)
    {
        GameObject popUpWorkoutItem = Instantiate(popupWorkoutPrefab, popUpWorkoutContainer);
        popUpWorkoutItem.transform.localScale = Vector3.one;
        popUpWorkoutItem.SetActive(true);

        TMP_Text workoutNameText = popUpWorkoutItem.transform.Find("WorkoutNameText")?.GetComponent<TMP_Text>();
        TMP_Text caloriesBurnedText = popUpWorkoutItem.transform.Find("CaloriesBurnedText")?.GetComponent<TMP_Text>();
        Button addWorkoutButton = popUpWorkoutItem.transform.Find("AddButton")?.GetComponent<Button>();

        if (workoutNameText != null) workoutNameText.text = workoutName;
        if (caloriesBurnedText != null) caloriesBurnedText.text = $"Calories Burned: {caloriesBurned:F1} kcal";

        if (addWorkoutButton != null)
        {
            addWorkoutButton.onClick.AddListener(() => AddWorkoutToList(workoutID, workoutName, caloriesBurned));
        }

        popUpWorkoutItems.Add(popUpWorkoutItem);
    }

    void AddWorkoutToList(int workoutID, string workoutName, float caloriesBurned)
    {
        if (workoutItemsPrefab == null || workoutItemsContainer == null)
        {
            Debug.LogError("❌ workoutItemsPrefab or workoutItemsContainer is missing!");
            return;
        }

        GameObject workoutItem = Instantiate(workoutItemsPrefab, workoutItemsContainer);
        workoutItem.transform.localScale = Vector3.one;
        workoutItem.SetActive(true);

        SetWorkoutItemUI(workoutItem, workoutName, caloriesBurned);

        workoutItems.Add(workoutItem);

        totalCaloriesBurned += caloriesBurned;
        UpdateResultText();

        StartCoroutine(UpdateWorkoutListLayout());
    }

    void SetWorkoutItemUI(GameObject workoutItem, string workoutName, float caloriesBurned)
    {
        if (workoutItem == null)
        {
            Debug.LogError("❌ Workout item is NULL!");
            return;
        }

        TMP_Text workoutNameText = workoutItem.transform.Find("WorkoutNameText")?.GetComponent<TMP_Text>();
        TMP_Text caloriesBurnedText = workoutItem.transform.Find("CaloriesBurnedText")?.GetComponent<TMP_Text>();

        if (workoutNameText != null) workoutNameText.text = workoutName;
        if (caloriesBurnedText != null) caloriesBurnedText.text = $"Calories Burned: {caloriesBurned:F1} kcal";
    }

    void UpdateResultText()
    {
        totalCaloriesBurnedText.text = $"Total Calories Burned: {totalCaloriesBurned:F1} kcal";
        recommendedCaloriesText.text = $"Recommended Calories: {recommendedCalories:F1} kcal";
        totalCaloriesText.text = $"Total Calories: {totalCalories:F1} kcal"; // Display total calories from MealScene
    }

    void SearchWorkouts(string query)
    {
        foreach (GameObject item in popUpWorkoutItems)
        {
            TMP_Text workoutNameText = item.transform.Find("WorkoutNameText")?.GetComponent<TMP_Text>();
            bool match = workoutNameText != null && workoutNameText.text.ToLower().Contains(query.ToLower());
            item.SetActive(match);
        }
    }

    void CloseWorkoutPopup()
    {
        workoutPopup.SetActive(false);
    }

    IEnumerator UpdateWorkoutListLayout()
    {
        yield return new WaitForEndOfFrame();
        LayoutRebuilder.ForceRebuildLayoutImmediate(workoutItemsContainer.GetComponent<RectTransform>());
    }

    IEnumerator UpdateLayout()
    {
        yield return new WaitForEndOfFrame();
        LayoutRebuilder.ForceRebuildLayoutImmediate(popUpWorkoutContainer.GetComponent<RectTransform>());
    }
}
