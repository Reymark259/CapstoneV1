using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Data;
using Mono.Data.Sqlite;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.SceneManagement;

public class MealSceneManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField searchBar;
    public Button openMealPopupButton;
    public Button closeMealPopupButton;
    public GameObject mealPopup;
    public Transform popUpMealContainer;
    public GameObject popupMealPrefab;

    [Header("Main List UI")]
    public Transform mealItemsContainer;
    public GameObject mealItemsPrefab;

    [Header("Result UI")]
    public TMP_Text totalCaloriesText;
    public TMP_Text totalProteinText;
    public TMP_Text recommendedCaloriesText;

    private string dbPath;
    private float totalCalories = 0;
    private float totalProtein = 0;
    private int loggedInUserID;
    private List<GameObject> popUpMealItems = new List<GameObject>();
    private List<GameObject> mealItems = new List<GameObject>();

    private float recommendedCalories = 0;

    [Header("Navigation")]
    public Button goBackButton;

    void Start()
    {
        if (DatabaseManager.Instance != null)
        {
            dbPath = DatabaseManager.Instance.GetDatabasePath();
            loggedInUserID = DatabaseManager.Instance.GetCurrentUserID();
        }

        if (string.IsNullOrEmpty(dbPath))
        {
            Debug.LogError("❌ Database path is empty! Ensure DatabaseManager is initialized properly.");
            return;
        }

        mealPopup.SetActive(false);
        openMealPopupButton.onClick.AddListener(ToggleMealPopup);
        closeMealPopupButton.onClick.AddListener(CloseMealPopup);

        recommendedCalories = PlayerPrefs.GetFloat("RecommendedCalories", 0);
        UpdateRecommendedCaloriesText();

        if (goBackButton != null)
        {
            goBackButton.onClick.AddListener(GoBackToHome);
        }

        searchBar.onValueChanged.AddListener(SearchMeals);

        LoadUserMeals(); // ✅ Now exists
    }

    void GoBackToHome()
    {
        SceneManager.LoadScene("HomeScreen");
    }

    void ToggleMealPopup()
    {
        mealPopup.SetActive(!mealPopup.activeSelf);

        if (mealPopup.activeSelf)
        {
            LoadPopupMeals();
        }
    }

    void LoadPopupMeals()
    {
        if (popupMealPrefab == null || popUpMealContainer == null)
        {
            Debug.LogError("❌ popupMealPrefab or popUpMealContainer is not assigned!");
            return;
        }

        foreach (Transform child in popUpMealContainer)
        {
            Destroy(child.gameObject);
        }
        popUpMealItems.Clear();

        List<(int id, string mealName, float protein, float calories)> mealData = new List<(int, string, float, float)>();

        using (var connection = new SqliteConnection("URI=file:" + dbPath))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id, meal_name, protein, calories FROM meals"; 

                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int mealID = reader.GetInt32(0);
                        string mealName = reader.GetString(1);
                        float protein = reader.IsDBNull(2) ? 0f : reader.GetFloat(2);
                        float calories = reader.IsDBNull(3) ? 0f : reader.GetFloat(3);

                        mealData.Add((mealID, mealName, protein, calories));
                    }
                }
            }
        }

        foreach (var meal in mealData)
        {
            CreatePopupMealItem(meal.id, meal.mealName, meal.calories, meal.protein);
        }
    }

    void CreatePopupMealItem(int mealID, string mealName, float calories, float protein)
    {
        GameObject popUpMealItem = Instantiate(popupMealPrefab, popUpMealContainer);
        popUpMealItem.transform.localScale = Vector3.one;
        popUpMealItem.SetActive(true);

        TMP_Text mealNameText = popUpMealItem.transform.Find("MealNameText")?.GetComponent<TMP_Text>();
        TMP_Text caloriesText = popUpMealItem.transform.Find("CalorieText")?.GetComponent<TMP_Text>();
        TMP_Text proteinText = popUpMealItem.transform.Find("ProteinText")?.GetComponent<TMP_Text>();
        Button addMealButton = popUpMealItem.transform.Find("AddButton")?.GetComponent<Button>();

        if (mealNameText != null) mealNameText.text = mealName;
        if (caloriesText != null) caloriesText.text = $"{calories:F1}";
        if (proteinText != null) proteinText.text = $"{protein:F1}g";

        if (addMealButton != null)
        {
            addMealButton.onClick.AddListener(() => AddMealToList(mealID, mealName, calories, protein));
        }

        popUpMealItems.Add(popUpMealItem);
    }

    void AddMealToList(int mealID, string mealName, float calories, float protein)
    {
        if (mealItemsPrefab == null || mealItemsContainer == null)
        {
            Debug.LogError("❌ mealItemsPrefab or mealItemsContainer is missing!");
            return;
        }

        GameObject mealItem = Instantiate(mealItemsPrefab, mealItemsContainer);
        mealItem.transform.localScale = Vector3.one;
        mealItem.SetActive(true);

        SetMealItemUI(mealItem, mealName, calories, protein);
        mealItems.Add(mealItem);

        totalCalories += calories;
        totalProtein += protein;
        UpdateResultText();

        SaveUserMeal(mealID, mealName, calories, protein);

        // ✅ Automatically close the meal popup after adding a meal
        CloseMealPopup();
    }




    void SaveUserMeal(int mealID, string mealName, float calories, float protein)
    {
        using (var connection = new SqliteConnection("URI=file:" + dbPath))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO user_meals (user_id, meal_id, meal_name, calories, protein, quantity) VALUES (@userID, @mealID, @meal_name, @calories, @protein, 1)";
                command.Parameters.AddWithValue("@userID", loggedInUserID);
                command.Parameters.AddWithValue("@mealID", mealID);
                command.Parameters.AddWithValue("@meal_name", mealName);
                command.Parameters.AddWithValue("@calories", calories);
                command.Parameters.AddWithValue("@protein", protein);
                command.ExecuteNonQuery();
            }
        }
    }


    void LoadUserMeals()
    {
        totalCalories = 0;
        totalProtein = 0;

        using (var connection = new SqliteConnection("URI=file:" + dbPath))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT meal_name, calories, protein FROM user_meals WHERE user_id = @userID";
                command.Parameters.AddWithValue("@userID", loggedInUserID);

                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string mealName = reader.GetString(0);
                        float calories = reader.GetFloat(1);
                        float protein = reader.GetFloat(2);
                        AddMealToList(0, mealName, calories, protein);
                    }
                }
            }
        }
    }

    void SetMealItemUI(GameObject mealItem, string mealName, float calories, float protein)
    {
        TMP_Text mealNameText = mealItem.transform.Find("MealNameText")?.GetComponent<TMP_Text>();
        TMP_Text caloriesText = mealItem.transform.Find("CalorieText")?.GetComponent<TMP_Text>();
        TMP_Text proteinText = mealItem.transform.Find("ProteinText")?.GetComponent<TMP_Text>();

        if (mealNameText != null) mealNameText.text = mealName;
        if (caloriesText != null) caloriesText.text = $" {calories:F1} ";
        if (proteinText != null) proteinText.text = $"{protein:F1}g";
    }

    void UpdateResultText()
    {
        totalCaloriesText.text = $"Total Calories: {totalCalories:F1}";
        totalProteinText.text = $"Total Protein: {totalProtein:F1}g";
    }

    void UpdateRecommendedCaloriesText()
    {
        recommendedCaloriesText.text = $"Recommended Calories: {recommendedCalories:F1} kcal";
    }
    void SearchMeals(string query)
    {
        Debug.Log($"🔍 Searching in pop-up for: {query}");

        foreach (Transform child in popUpMealContainer) // Loop through instantiated meals in the pop-up
        {
            TMP_Text mealNameText = child.Find("MealNameText")?.GetComponent<TMP_Text>();

            if (mealNameText == null)
            {
                Debug.LogError($"❌ MealNameText not found in {child.name}");
                continue;
            }

            bool match = mealNameText.text.ToLower().Contains(query.ToLower());
            child.gameObject.SetActive(match); // Show or hide based on search query
        }
    }




    void CloseMealPopup()
    {
        mealPopup.SetActive(false);
    }
}
