using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Data;
using Mono.Data.Sqlite;
using System.Collections.Generic;
using System;
using UnityEngine.SceneManagement;

public class AdminWorkoutManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField searchBar;
    public Button addWorkoutButton;
    public Transform workoutListContainer;
    public GameObject workoutItemPrefab;

    [Header("Category Buttons")]
    public Button beginnerButton;
    public Button intermediateButton;
    public Button experiencedButton;

    [Header("Pop-up UI")]
    public GameObject addWorkoutPopup;
    public TMP_InputField workoutNameInput, repsInput, caloriesBurnedInput;
    public Button submitWorkoutButton, cancelWorkoutButton;

    [Header("Other References")]
    public Button logoutButton;

    private string dbPath;
    private List<GameObject> workoutItems = new List<GameObject>();
    private int editingWorkoutID = -1;
    private string selectedCategory; // Category will be set based on the scene name

    void Start()
    {
        // Set the category based on the current scene name
        SetCategoryFromScene();

        if (DatabaseManager.Instance != null)
        {
            dbPath = DatabaseManager.Instance.GetDatabasePath();
        }

        if (string.IsNullOrEmpty(dbPath))
        {
            Debug.LogError("❌ Database path is empty! Ensure DatabaseManager is initialized properly.");
            return;
        }

        if (addWorkoutPopup != null) addWorkoutPopup.SetActive(false);

        // Add button listeners
        addWorkoutButton.onClick.AddListener(() => ShowPopup(false));
        submitWorkoutButton.onClick.AddListener(SubmitWorkout);
        cancelWorkoutButton.onClick.AddListener(HidePopup);
        searchBar.onValueChanged.AddListener(SearchWorkouts);
        logoutButton.onClick.AddListener(Logout);

        // Add category button listeners
        beginnerButton.onClick.AddListener(() => ChangeScene("AdminWorkoutScene"));
        intermediateButton.onClick.AddListener(() => ChangeScene("AdminIWorkoutScene"));
        experiencedButton.onClick.AddListener(() => ChangeScene("AdminXWorkoutScene"));

        // Load workouts for the selected category
        LoadWorkoutsByCategory(selectedCategory);
    }

    void SetCategoryFromScene()
    {
        // Set the category based on the current scene name
        string sceneName = SceneManager.GetActiveScene().name;

        switch (sceneName)
        {
            case "AdminWorkoutScene":
                selectedCategory = "Beginner";
                break;
            case "AdminIWorkoutScene":
                selectedCategory = "Intermediate";
                break;
            case "AdminXWorkoutScene":
                selectedCategory = "Experienced";
                break;
            default:
                Debug.LogWarning("⚠️ Invalid scene name. Defaulting to Beginner.");
                selectedCategory = "Beginner";
                break;
        }

        Debug.Log($"🔹 Selected Category: {selectedCategory}");
    }

    void ChangeScene(string sceneName)
    {
        Debug.Log($"🔄 Attempting to load scene: {sceneName}");

        // Check if the scene exists in build settings
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError($"❌ Scene '{sceneName}' not found in build settings!");
        }
    }

    void ShowPopup(bool isEdit)
    {
        addWorkoutPopup.SetActive(true);
        if (!isEdit)
        {
            ClearInputs();
            editingWorkoutID = -1;
        }
    }

    void HidePopup()
    {
        addWorkoutPopup.SetActive(false);
        ClearInputs();
    }

    public void SubmitWorkout()
    {
        if (string.IsNullOrEmpty(workoutNameInput.text) || string.IsNullOrEmpty(repsInput.text) || string.IsNullOrEmpty(caloriesBurnedInput.text))
        {
            Debug.LogWarning("⚠️ Please fill in all fields.");
            return;
        }

        if (!float.TryParse(repsInput.text, out float repsValue) || !float.TryParse(caloriesBurnedInput.text, out float caloriesBurnedValue))
        {
            Debug.LogWarning("⚠️ Please enter valid numeric values.");
            return;
        }

        if (editingWorkoutID == -1)
        {
            AddWorkoutToDatabase(workoutNameInput.text, repsValue, caloriesBurnedValue, selectedCategory);
        }
        else
        {
            UpdateWorkoutInDatabase(editingWorkoutID, workoutNameInput.text, repsValue, caloriesBurnedValue, selectedCategory);
        }

        HidePopup();
        LoadWorkoutsByCategory(selectedCategory);
    }

    void AddWorkoutToDatabase(string workoutName, float reps, float caloriesBurned, string category)
    {
        int userID = DatabaseManager.Instance.GetCurrentUserID();
        if (userID == -1)
        {
            Debug.LogError("❌ User ID not found! Ensure user is logged in.");
            return;
        }

        try
        {
            using (var connection = new SqliteConnection("URI=file:" + dbPath))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "INSERT INTO workouts (workout_name, reps, calories_burned, category, user_id) VALUES (@workout_name, @reps, @caloriesBurned, @category, @user_id)";
                    command.Parameters.Add(new SqliteParameter("@workout_name", workoutName));
                    command.Parameters.Add(new SqliteParameter("@reps", reps));
                    command.Parameters.Add(new SqliteParameter("@caloriesBurned", caloriesBurned));
                    command.Parameters.Add(new SqliteParameter("@category", category));
                    command.Parameters.Add(new SqliteParameter("@user_id", userID));

                    command.ExecuteNonQuery();
                    Debug.Log("✅ Workout added successfully!");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("❌ Error adding workout: " + ex.Message);
        }
    }

    void LoadWorkoutsByCategory(string category)
    {
        foreach (GameObject item in workoutItems) Destroy(item);
        workoutItems.Clear();

        int userID = DatabaseManager.Instance.GetCurrentUserID();
        if (userID == -1)
        {
            Debug.LogError("❌ User ID not found! Ensure user is logged in.");
            return;
        }

        using (var connection = new SqliteConnection("URI=file:" + dbPath))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id, workout_name, reps, calories_burned FROM workouts WHERE user_id = @user_id AND category = @category";
                command.Parameters.Add(new SqliteParameter("@user_id", userID));
                command.Parameters.Add(new SqliteParameter("@category", category));

                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int workoutID = reader.GetInt32(0);
                        string workoutName = reader.GetString(1);
                        float reps = Convert.ToSingle(reader.GetValue(2));
                        float caloriesBurned = Convert.ToSingle(reader.GetValue(3));

                        CreateWorkoutItem(workoutID, workoutName, reps, caloriesBurned);
                    }
                }
            }
        }
    }

    void SearchWorkouts(string query)
    {
        foreach (GameObject item in workoutItems)
        {
            bool match = item.GetComponentInChildren<TMP_Text>().text.ToLower().Contains(query.ToLower());
            item.SetActive(match);
        }
    }

    void ClearInputs()
    {
        workoutNameInput.text = "";
        caloriesBurnedInput.text = "";
        repsInput.text = "";
        editingWorkoutID = -1;
    }

    void EditWorkout(int workoutID, string workoutName, float reps, float caloriesBurned)
    {
        editingWorkoutID = workoutID;
        workoutNameInput.text = workoutName;
        repsInput.text = reps.ToString();
        caloriesBurnedInput.text = caloriesBurned.ToString();

        ShowPopup(true);
    }

    void CreateWorkoutItem(int workoutID, string workoutName, float reps, float caloriesBurned)
    {
        GameObject workoutItem = Instantiate(workoutItemPrefab, workoutListContainer);
        TMP_Text workoutNameText = workoutItem.transform.Find("WorkoutNameText")?.GetComponent<TMP_Text>();
        TMP_Text repsText = workoutItem.transform.Find("RepsText")?.GetComponent<TMP_Text>();
        TMP_Text caloriesBurnedText = workoutItem.transform.Find("CaloriesBurnedText")?.GetComponent<TMP_Text>();

        if (workoutNameText != null) workoutNameText.text = workoutName;
        if (repsText != null) repsText.text = $"Reps: {reps}";
        if (caloriesBurnedText != null) caloriesBurnedText.text = $"Calories Burned: {caloriesBurned}";

        Transform buttonContainer = workoutItem.transform.Find("ButtonContainer");
        if (buttonContainer != null)
        {
            Button editButton = buttonContainer.Find("EditWorkoutButton")?.GetComponent<Button>();
            Button deleteButton = buttonContainer.Find("DeleteWorkoutButton")?.GetComponent<Button>();

            if (editButton != null)
            {
                editButton.onClick.AddListener(() => EditWorkout(workoutID, workoutName, reps, caloriesBurned));
            }

            if (deleteButton != null)
            {
                deleteButton.onClick.AddListener(() => DeleteWorkout(workoutID, workoutItem));
            }
        }

        workoutItems.Add(workoutItem);
    }

    void DeleteWorkout(int workoutID, GameObject workoutItem)
    {
        using (var connection = new SqliteConnection("URI=file:" + dbPath))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM workouts WHERE id = @id";
                command.Parameters.Add(new SqliteParameter("@id", workoutID));
                command.ExecuteNonQuery();
            }
        }
        Destroy(workoutItem);
    }

    void UpdateWorkoutInDatabase(int workoutID, string workoutName, float reps, float caloriesBurned, string category)
    {
        using (var connection = new SqliteConnection("URI=file:" + dbPath))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE workouts SET workout_name = @workout_name, reps = @reps, calories_burned = @caloriesBurned, category = @category WHERE id = @id";
                command.Parameters.Add(new SqliteParameter("@id", workoutID));
                command.Parameters.Add(new SqliteParameter("@workout_name", workoutName));
                command.Parameters.Add(new SqliteParameter("@reps", reps));
                command.Parameters.Add(new SqliteParameter("@caloriesBurned", caloriesBurned));
                command.Parameters.Add(new SqliteParameter("@category", category));
                command.ExecuteNonQuery();
            }
        }
    }

    void Logout()
    {
        SceneManager.LoadScene("AdminScene");
    }
}