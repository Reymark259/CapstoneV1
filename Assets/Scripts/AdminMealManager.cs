using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Data;
using Mono.Data.Sqlite;
using System.Collections.Generic;
using System;

public class AdminMealManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField searchBar;
    public Button addMealButton;
    public Transform mealListContainer;
    public GameObject mealItemPrefab;

    [Header("Pop-up UI")]
    public GameObject addMealPopup;
    public TMP_InputField mealNameInput, caloriesInput, proteinInput;
    public Button submitMealButton, cancelMealButton;

    private string dbPath;
    private List<GameObject> mealItems = new List<GameObject>();
    private int editingMealID = -1; // Track meal being edited
    public Button logoutButton;
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

        if (addMealPopup != null) addMealPopup.SetActive(false);  // Ensure popup is hidden initially

        addMealButton.onClick.AddListener(() => ShowPopup(false)); // Show add meal popup when button is clicked
        submitMealButton.onClick.AddListener(SubmitMeal);
        cancelMealButton.onClick.AddListener(HidePopup);
        searchBar.onValueChanged.AddListener(SearchMeals);

        // Add listener for logout button
        logoutButton.onClick.AddListener(Logout);

        LoadMeals();
    }


    void ShowPopup(bool isEdit)
    {
        addMealPopup.SetActive(true);
        if (!isEdit)
        {
            ClearInputs();
            editingMealID = -1;
        }
    }

    void HidePopup()
    {
        addMealPopup.SetActive(false);
        ClearInputs();
    }

    public void SubmitMeal()
    {
        if (string.IsNullOrEmpty(mealNameInput.text) || string.IsNullOrEmpty(caloriesInput.text) || string.IsNullOrEmpty(proteinInput.text))
        {
            Debug.LogWarning("⚠️ Please fill in all fields.");
            return;
        }

        if (!float.TryParse(proteinInput.text, out float proteinValue) || !float.TryParse(caloriesInput.text, out float calorieValue))
        {
            Debug.LogWarning("⚠️ Please enter valid numeric values.");
            return;
        }

        if (editingMealID == -1)
        {
            AddMealToDatabase(mealNameInput.text, calorieValue, proteinValue);
        }
        else
        {
            UpdateMealInDatabase(editingMealID, mealNameInput.text, calorieValue, proteinValue);
        }

        HidePopup();
        LoadMeals();
    }

    void AddMealToDatabase(string mealName, float calories, float protein)
    {
        int userID = DatabaseManager.Instance.GetCurrentUserID();
        if (userID == -1)
        {
            Debug.LogError("❌ User ID not found! Ensure user is logged in.");
            return;
        }

        using (var connection = new SqliteConnection("URI=file:" + dbPath))
        {
            connection.Open();

            // Check if meal already exists
            using (var checkCmd = connection.CreateCommand())
            {
                checkCmd.CommandText = "SELECT COUNT(*) FROM meals WHERE meal_name = @meal_name AND user_id = @user_id";
                checkCmd.Parameters.Add(new SqliteParameter("@meal_name", mealName));
                checkCmd.Parameters.Add(new SqliteParameter("@user_id", userID));

                int count = System.Convert.ToInt32(checkCmd.ExecuteScalar());
                if (count > 0)
                {
                    Debug.LogWarning("⚠️ Meal already exists for this user! Choose a different name.");
                    return;
                }
            }

            // Insert the meal with updated column name
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO meals (meal_name, protein, calories, user_id) VALUES (@meal_name, @protein, @calories, @user_id)";
                command.Parameters.Add(new SqliteParameter("@meal_name", mealName));
                command.Parameters.Add(new SqliteParameter("@protein", protein));
                command.Parameters.Add(new SqliteParameter("@calories", calories));
                command.Parameters.Add(new SqliteParameter("@user_id", userID));

                command.ExecuteNonQuery();
            }
        }
    }



    void LoadMeals()
    {
        foreach (GameObject item in mealItems) Destroy(item);
        mealItems.Clear();

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
                command.CommandText = "SELECT id, meal_name, protein, calories FROM meals WHERE user_id = @user_id";
                command.Parameters.Add(new SqliteParameter("@user_id", userID));

                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int mealID = reader.GetInt32(0);
                        string mealName = reader.GetString(1);
                        float protein = reader.IsDBNull(2) ? 0f : Convert.ToSingle(reader.GetValue(2));
                        float calories = reader.IsDBNull(3) ? 0f : Convert.ToSingle(reader.GetValue(3));

                        Debug.Log($"📌 Loaded Meal: ID={mealID}, Name={mealName}, Calories={calories}, Protein={protein}");
                        CreateMealItem(mealID, mealName, calories, protein);
                    }
                }
            }
        }
    }





    void CreateMealItem(int mealID, string mealName, float calories, float protein)
    {
        GameObject mealItem = Instantiate(mealItemPrefab, mealListContainer);
        Debug.Log($"✅ Created MealItem: {mealItem.name}");

        TMP_Text mealsNameText = mealItem.transform.Find("MealNameText")?.GetComponent<TMP_Text>();
        TMP_Text caloriesText = mealItem.transform.Find("CaloriesText")?.GetComponent<TMP_Text>();
        TMP_Text proteinsText = mealItem.transform.Find("ProteinText")?.GetComponent<TMP_Text>();

        if (mealsNameText != null)
        {
            mealsNameText.text = mealName;
        }
        else
        {
            Debug.LogError("❌ MealNameText NOT found in MealItemPrefab! Check your prefab.");
        }

        if (caloriesText != null)
        {
            caloriesText.text = $"Calories: {calories}";
        }
        else
        {
            Debug.LogError("❌ CaloriesText NOT found in MealItemPrefab! Check your prefab.");
        }

        if (proteinsText != null)
        {
            proteinsText.text = $"Protein: {protein}";
        }
        else
        {
            Debug.LogError("❌ ProteinText NOT found in MealItemPrefab! Check your prefab.");
        }

        Transform buttonsContainer = mealItem.transform.Find("ButtonsContainer");
        if (buttonsContainer == null)
        {
            Debug.LogError("❌ ButtonsContainer NOT found in MealItemPrefab! Check your prefab structure.");
            return;
        }

        Debug.Log($"✅ ButtonsContainer found in {mealItem.name}");

        Button editButton = buttonsContainer.Find("EditMealButton")?.GetComponent<Button>();
        Button deleteButton = buttonsContainer.Find("DeleteMealButton")?.GetComponent<Button>();

        if (editButton == null) Debug.LogError("❌ EditMealButton NOT found! Check if it's inside ButtonsContainer.");
        else editButton.onClick.AddListener(() => EditMeal(mealID, mealName, calories, protein));

        if (deleteButton == null) Debug.LogError("❌ DeleteMealButton NOT found! Check if it's inside ButtonsContainer.");
        else deleteButton.onClick.AddListener(() => DeleteMeal(mealID, mealItem));

        mealItems.Add(mealItem);
    }


    void UpdateMealInDatabase(int mealID, string mealName, float calories, float protein)
    {
        using (var connection = new SqliteConnection("URI=file:" + dbPath))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE meals SET meal_name = @meal_name, protein = @protein, calories = @calories WHERE id = @id";
                command.Parameters.Add(new SqliteParameter("@id", mealID));
                command.Parameters.Add(new SqliteParameter("@meal_name", mealName));
                command.Parameters.Add(new SqliteParameter("@protein", protein));
                command.Parameters.Add(new SqliteParameter("@calories", calories));
                command.ExecuteNonQuery();
            }
        }
    }


    void DeleteMeal(int mealID, GameObject mealItem)
    {
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
                command.CommandText = "DELETE FROM meals WHERE id = @id AND user_id = @user_id";
                command.Parameters.Add(new SqliteParameter("@id", mealID));
                command.Parameters.Add(new SqliteParameter("@user_id", userID));

                command.ExecuteNonQuery();
            }
        }

        Destroy(mealItem);
    }



    void EditMeal(int mealID, string mealName, float calories, float protein)
    {
        editingMealID = mealID;
        mealNameInput.text = mealName;
        caloriesInput.text = calories.ToString();
        proteinInput.text = protein.ToString();

        ShowPopup(true);
    }

    void SearchMeals(string query)
    {
        foreach (GameObject item in mealItems)
        {
            bool match = item.GetComponentInChildren<TMP_Text>().text.ToLower().Contains(query.ToLower());
            item.SetActive(match);
        }
    }

    void ClearInputs()
    {
        mealNameInput.text = "";
        caloriesInput.text = "";
        proteinInput.text = "";
        editingMealID = -1;
    }

    void Logout()
    {

        // Load the LoginRegisterScene
        UnityEngine.SceneManagement.SceneManager.LoadScene("AdminScene");
    }

}
