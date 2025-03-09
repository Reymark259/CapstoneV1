using UnityEngine;
using System.Data;
using Mono.Data.Sqlite;
using System.IO;
using System;

public class DatabaseManager : MonoBehaviour
{
    public static DatabaseManager Instance { get; private set; }
    private string dbPath;
     private string currentCategory; // ✅ Declare this as a class variable

    void Start()
    {
        currentCategory = GetCategoryFromScene(); // ✅ Now it exists in this context
        Debug.Log($"📌 Current Category: {currentCategory}");

        LoadWorkouts(); // ✅ Make sure LoadWorkouts() is implemented
    }

    private string GetCategoryFromScene()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName.Contains("Beginner")) return "Beginner";
        if (sceneName.Contains("Intermediate")) return "Intermediate";
        if (sceneName.Contains("Expert")) return "Expert";
        return "Beginner"; // Default
    }

    private void LoadWorkouts() // ✅ Add this function
    {
        int userID = GetCurrentUserID();
        if (userID == -1) return;

        DataTable workouts = GetWorkouts(currentCategory, userID);

        foreach (DataRow row in workouts.Rows)
        {
            string workoutName = row["workout_name"].ToString();
            int reps = Convert.ToInt32(row["reps"]);
            float caloriesBurned = Convert.ToSingle(row["calories_burned"]);
            Debug.Log($"🔥 Workout: {workoutName}, Reps: {reps}, Calories Burned: {caloriesBurned}");
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            dbPath = Path.Combine(Application.persistentDataPath, "UserDatabase.db");
            Debug.Log($"📂 Database Path: {dbPath}");

            CreateDatabase();
            MigrateDatabase();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void CreateDatabase()
    {
        try
        {
            using (var connection = new SqliteConnection("URI=file:" + dbPath))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                PRAGMA foreign_keys = ON;

                -- Create tables if they don't exist
                CREATE TABLE IF NOT EXISTS users (
                    id INTEGER PRIMARY KEY AUTOINCREMENT, 
                    username TEXT UNIQUE NOT NULL COLLATE NOCASE, 
                    password TEXT NOT NULL,
                    isAdmin INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS workouts (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id INTEGER NOT NULL,
                    workout_name TEXT NOT NULL,
                    reps INTEGER NOT NULL,
                    calories_burned REAL NOT NULL,
                    category TEXT NOT NULL CHECK(category IN ('Beginner', 'Intermediate', 'Expert')),
                    FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS meals (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id INTEGER DEFAULT NULL,  
                    meal_name TEXT NOT NULL,  -- 🔹 Changed `name` to `meal_name`
                    protein REAL NOT NULL,
                    calories REAL NOT NULL,
                    FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE
                );

                -- ✅ Added missing user_meals table
                CREATE TABLE IF NOT EXISTS user_meals (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id INTEGER NOT NULL,
                    meal_id INTEGER NOT NULL,
                    meal_name TEXT NOT NULL,
                    calories REAL NOT NULL,
                    protein REAL NOT NULL,
                    quantity INTEGER DEFAULT 1,
                    FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE,
                    FOREIGN KEY(meal_id) REFERENCES meals(id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS user_data (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id INTEGER UNIQUE NOT NULL,  
                    weight REAL NOT NULL,
                    height REAL NOT NULL,
                    age INTEGER NOT NULL,
                    gender TEXT NOT NULL CHECK(gender IN ('Male', 'Female')),
                    activity_level TEXT NOT NULL CHECK(activity_level IN ('Sedentary', 'Lightly active', 'Moderately active', 'Very active')),
                    bmr REAL NOT NULL,
                    tdee REAL NOT NULL,
                    goal TEXT DEFAULT NULL,
                    recommended_calories REAL DEFAULT 0,
                    experience_level TEXT DEFAULT NULL,
                    hasCompletedSetup INTEGER DEFAULT 0,
                    FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE
                );

                -- ✅ Fixed wrong index name (Changed `name` to `meal_name`)
                CREATE INDEX IF NOT EXISTS idx_meal_name ON meals(meal_name);
                CREATE INDEX IF NOT EXISTS idx_users_username ON users(username);
                CREATE INDEX IF NOT EXISTS idx_workout_user_id ON workouts(user_id);
                CREATE INDEX IF NOT EXISTS idx_meal_user_id ON meals(user_id);
            ";

                    command.ExecuteNonQuery();
                }
            }
            Debug.Log("✅ Database and tables initialized successfully.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Database creation failed: {ex.Message}");
        }
    }




    void MigrateDatabase()
    {
        try
        {
            using (var connection = new SqliteConnection("URI=file:" + dbPath))
            {
                connection.Open();

                // ✅ Check for missing columns in user_data
                bool hasGoal = false, hasCalories = false, hasExperience = false;

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA table_info(user_data)";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string columnName = reader["name"].ToString();
                            if (columnName == "goal") hasGoal = true;
                            if (columnName == "recommended_calories") hasCalories = true;
                            if (columnName == "experience_level") hasExperience = true;
                        }
                    }
                }

                if (!hasGoal)
                {
                    ExecuteNonQuery("ALTER TABLE user_data ADD COLUMN goal TEXT DEFAULT NULL");
                    Debug.Log("✅ 'goal' column added.");
                }
                if (!hasCalories)
                {
                    ExecuteNonQuery("ALTER TABLE user_data ADD COLUMN recommended_calories REAL DEFAULT 0");
                    Debug.Log("✅ 'recommended_calories' column added.");
                }
                if (!hasExperience)
                {
                    ExecuteNonQuery("ALTER TABLE user_data ADD COLUMN experience_level TEXT DEFAULT NULL");
                    Debug.Log("✅ 'experience_level' column added.");
                }

                // ✅ Check for missing 'category' column in workouts
                bool hasCategory = false;
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA table_info(workouts)";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string columnName = reader["name"].ToString();
                            if (columnName == "category") hasCategory = true;
                        }
                    }
                }

                if (!hasCategory)
                {
                    ExecuteNonQuery("ALTER TABLE workouts ADD COLUMN category TEXT NOT NULL DEFAULT 'Beginner'");
                    Debug.Log("✅ 'category' column added to workouts table.");
                }

                // ✅ Check for missing 'quantity' column in user_meals
                bool hasQuantity = false;
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA table_info(user_meals)";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string columnName = reader["name"].ToString();
                            if (columnName == "quantity") hasQuantity = true;
                        }
                    }
                }

                if (!hasQuantity)
                {
                    ExecuteNonQuery("ALTER TABLE user_meals ADD COLUMN quantity INTEGER DEFAULT 1");
                    Debug.Log("✅ 'quantity' column added to user_meals table.");
                }

                // ✅ Check for missing 'meal_name' column in user_meals
                bool hasMealNameInUserMeals = false, hasOldNameInUserMeals = false;
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA table_info(user_meals)";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string columnName = reader["name"].ToString();
                            if (columnName == "meal_name") hasMealNameInUserMeals = true;
                            if (columnName == "name") hasOldNameInUserMeals = true;
                        }
                    }
                }

                if (hasOldNameInUserMeals && !hasMealNameInUserMeals)
                {
                    Debug.Log("⚠️ Renaming `name` to `meal_name` in user_meals table...");

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                    BEGIN TRANSACTION;

                    CREATE TABLE IF NOT EXISTS user_meals_new (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        user_id INTEGER NOT NULL,
                        meal_id INTEGER NOT NULL,
                        meal_name TEXT NOT NULL,
                        calories REAL NOT NULL,
                        protein REAL NOT NULL,
                        quantity INTEGER DEFAULT 1,
                        FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE,
                        FOREIGN KEY(meal_id) REFERENCES meals(id) ON DELETE CASCADE
                    );

                    INSERT INTO user_meals_new (id, user_id, meal_id, meal_name, calories, protein, quantity)
                    SELECT id, user_id, meal_id, name, calories, protein, quantity FROM user_meals;

                    DROP TABLE user_meals;

                    ALTER TABLE user_meals_new RENAME TO user_meals;

                    COMMIT;
                ";
                        command.ExecuteNonQuery();
                    }

                    Debug.Log("✅ `user_meals` table updated successfully! `name` column is now `meal_name`.");
                }

                // ✅ Check for missing 'meal_name' column in meals (Rename `name` to `meal_name`)
                bool hasMealName = false, hasOldName = false;
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA table_info(meals)";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string columnName = reader["name"].ToString();
                            if (columnName == "meal_name") hasMealName = true;
                            if (columnName == "name") hasOldName = true;
                        }
                    }
                }

                if (hasOldName && !hasMealName)
                {
                    Debug.Log("⚠️ Renaming `name` to `meal_name` in meals table...");

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                    BEGIN TRANSACTION;

                        -- Create a new table with meal_name
                        CREATE TABLE IF NOT EXISTS user_meals_new (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            user_id INTEGER NOT NULL,
                            meal_id INTEGER NOT NULL,
                            meal_name TEXT NOT NULL,
                            calories REAL NOT NULL,
                            protein REAL NOT NULL,
                            quantity INTEGER DEFAULT 1,
                            FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE,
                            FOREIGN KEY(meal_id) REFERENCES meals(id) ON DELETE CASCADE
                        );

                        -- Copy data from old table to new table
                        INSERT INTO user_meals_new (id, user_id, meal_id, meal_name, calories, protein, quantity)
                        SELECT id, user_id, meal_id, name, calories, protein, quantity FROM user_meals;

                        -- Delete the old table
                        DROP TABLE user_meals;

                        -- Rename the new table to `user_meals`
                        ALTER TABLE user_meals_new RENAME TO user_meals;

                        COMMIT;

                ";
                        command.ExecuteNonQuery();
                    }

                    Debug.Log("✅ `meals` table updated successfully! `name` column is now `meal_name`.");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Database migration failed: {ex.Message}");
        }
    }







    void ExecuteNonQuery(string query)
    {
        using (var connection = new SqliteConnection("URI=file:" + dbPath))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = query;
                command.ExecuteNonQuery();
            }
        }
    }


    public string GetDatabasePath() => dbPath;

    public void InsertWorkout(int userID, string workoutName, int reps, float caloriesBurned, string category)
    {
        using (var connection = new SqliteConnection("URI=file:" + dbPath))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
            INSERT INTO workouts (user_id, workout_name, reps, calories_burned, category)
            VALUES (@userID, @workoutName, @reps, @caloriesBurned, @category)";

                command.Parameters.AddWithValue("@userID", userID);
                command.Parameters.AddWithValue("@workoutName", workoutName);
                command.Parameters.AddWithValue("@reps", reps);
                command.Parameters.AddWithValue("@caloriesBurned", caloriesBurned);
                command.Parameters.AddWithValue("@category", category); // Now adding category

                command.ExecuteNonQuery();
                Debug.Log($"✅ Workout '{workoutName}' added in '{category}' category.");
            }
        }
    }



    public DataTable GetWorkouts(string category, int userID)
    {
        DataTable workoutsTable = new DataTable();
        using (var connection = new SqliteConnection("URI=file:" + dbPath))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
            SELECT * FROM workouts 
            WHERE user_id = @userID AND category = @category";

                command.Parameters.AddWithValue("@userID", userID);
                command.Parameters.AddWithValue("@category", category); // Filtering by category

                using (var reader = command.ExecuteReader())
                {
                    workoutsTable.Load(reader);
                }
            }
        }
        return workoutsTable;
    }


    private string GetWorkoutTableName(string experienceLevel)
    {
        switch (experienceLevel.ToLower())
        {
            case "beginner": return "workouts_beginner";
            case "intermediate": return "workouts_intermediate";
            case "expert": return "workouts_expert";
            default:
                Debug.LogError($"❌ Invalid experience level: {experienceLevel}");
                return null;
        }
    }

    public void SaveUserGoal(int userID, string selectedGoal, float recommendedCalories, string experienceLevel)
    {
        using (var connection = new SqliteConnection("URI=file:" + dbPath))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                UPDATE user_data 
                SET goal = @goal, 
                    recommended_calories = @calories, 
                    experience_level = @experience 
                WHERE user_id = @userID";

                command.Parameters.AddWithValue("@goal", selectedGoal);
                command.Parameters.AddWithValue("@calories", recommendedCalories);
                command.Parameters.AddWithValue("@experience", experienceLevel);
                command.Parameters.AddWithValue("@userID", userID);

                command.ExecuteNonQuery();
                Debug.Log($"✅ Goal '{selectedGoal}', Calories '{recommendedCalories}', and Experience Level '{experienceLevel}' saved for UserID: {userID}");
            }
        }
    }

    public void SetUserSetupCompleted(int userID)
    {
        ExecuteNonQuery($"UPDATE user_data SET hasCompletedSetup = 1 WHERE user_id = {userID}");
        Debug.Log($"✅ User {userID} setup marked as completed.");
    }

    public bool HasUserCompletedSetup(int userID)
    {
        using (var connection = new SqliteConnection("URI=file:" + dbPath))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT hasCompletedSetup FROM user_data WHERE user_id = @userID";
                command.Parameters.AddWithValue("@userID", userID);

                object result = command.ExecuteScalar();
                return result != null && Convert.ToInt32(result) == 1;
            }
        }
    }
    public int GetCurrentUserID()
    {
        if (PlayerPrefs.HasKey("UserID"))
        {
            return PlayerPrefs.GetInt("UserID");
        }

        Debug.LogError("❌ No user logged in!");
        return -1;
    }

    public void InsertOrUpdateUserData(int userID, float weight, float height, int age, string gender, string activityLevel, float bmr, float tdee)
    {
        using (var connection = new SqliteConnection("URI=file:" + dbPath))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                INSERT INTO user_data (user_id, weight, height, age, gender, activity_level, bmr, tdee, hasCompletedSetup)
                VALUES (@userID, @weight, @height, @age, @gender, @activityLevel, @bmr, @tdee, 1)
                ON CONFLICT(user_id) DO UPDATE SET 
                    weight = excluded.weight,
                    height = excluded.height,
                    age = excluded.age,
                    gender = excluded.gender,
                    activity_level = excluded.activity_level,
                    bmr = excluded.bmr,
                    tdee = excluded.tdee,
                    hasCompletedSetup = 1";

                command.Parameters.AddWithValue("@userID", userID);
                command.Parameters.AddWithValue("@weight", weight);
                command.Parameters.AddWithValue("@height", height);
                command.Parameters.AddWithValue("@age", age);
                command.Parameters.AddWithValue("@gender", gender);
                command.Parameters.AddWithValue("@activityLevel", activityLevel);
                command.Parameters.AddWithValue("@bmr", bmr);
                command.Parameters.AddWithValue("@tdee", tdee);

                command.ExecuteNonQuery();
            }
        }
    }

}
