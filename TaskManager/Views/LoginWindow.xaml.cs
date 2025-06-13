using Npgsql;
using System.Windows;
using TaskManager.Services;

namespace TaskManager.Views;

public partial class LoginWindow : Window
{
    private readonly DatabaseService _dbService = new();
    private const int maxAttempts = 3;

    public LoginWindow()
    {
        InitializeComponent();

        if (Settings.Default.SavedUserId != 0)
        {
            // Заполнить сессию
            UserSession.Instance.SetUser(
                Settings.Default.SavedUserId,
                Settings.Default.SavedUsername,
                Settings.Default.SavedEmail);

            // Открыть главное окно сразу
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }
    }

    string GetRussianPlural(int number, string form1, string form2, string form5)
    {
        number = Math.Abs(number) % 100;
        int n1 = number % 10;
        if (number > 10 && number < 20) return form5;
        if (n1 > 1 && n1 < 5) return form2;
        if (n1 == 1) return form1;
        return form5;
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        string username = txtUsername.Text.Trim().ToLower();
        string password = txtPassword.Password;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            MessageBox.Show("Пожалуйста, заполните все поля.");
            return;
        }

        try
        {
            if (DateTime.Now < Settings.Default.LockoutEndTime)
            {
                TimeSpan remaining = Settings.Default.LockoutEndTime - DateTime.Now;
                int minutes = Math.Max(0, remaining.Minutes);
                int seconds = Math.Max(0, remaining.Seconds);

                string timeString;

                if (minutes > 0)
                {
                    string minuteWord = GetRussianPlural(minutes, "минуту", "минуты", "минут");
                    string secondWord = GetRussianPlural(seconds, "секунду", "секунды", "секунд");
                    timeString = $"{minutes} {minuteWord} {seconds} {secondWord}";
                }
                else
                {
                    string secondWord = GetRussianPlural(seconds, "секунда", "секунды", "секунд");
                    timeString = $"{seconds} {secondWord}";
                }

                MessageBox.Show($"Слишком много попыток. Попробуйте снова через {timeString}.");

                return;
            }


            var (isValid, userId, email) = ValidateUser(username, password);

            Logger.LogLoginAttempt(username, isValid);

            if (isValid)
            {
                MessageBox.Show("Вход выполнен успешно!");
                UserSession.Instance.SetUser(userId, username ?? string.Empty, email ?? string.Empty);

                // При успешном входе и если чекбокс отмечен
                if (chkRememberMe.IsChecked == true)
                {
                    Settings.Default.SavedUserId = userId;
                    Settings.Default.SavedUsername = username;
                    Settings.Default.SavedEmail = email;
                    Settings.Default.Save();
                }
                else
                {
                    // Очистить сохранённые данные
                    Settings.Default.SavedUserId = 0;
                    Settings.Default.SavedUsername = "";
                    Settings.Default.SavedEmail = "";
                    Settings.Default.Save();
                }

                Settings.Default.LoginAttempts = 0;
                Settings.Default.LockoutEndTime = DateTime.MinValue;
                Settings.Default.Save();

                // Открываем главное окно
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            }
            else
            {
                Settings.Default.LoginAttempts++;
                if (Settings.Default.LoginAttempts >= maxAttempts)
                {
                    Settings.Default.LockoutEndTime = DateTime.Now.AddMinutes(1);
                    MessageBox.Show("Аккаунт заблокирован на 1 минуту.");
                }
                else
                {
                    MessageBox.Show($"Неверный логин или пароль. Осталось попыток: {maxAttempts - Settings.Default.LoginAttempts}");
                }
                Settings.Default.Save();
            }
        }
        catch (NpgsqlException ex)
        {
            Logger.LogError(ex);
        }
    }

    private (bool IsValid, int UserId, string? Email) ValidateUser(string username, string password)
    {
        using var conn = _dbService.GetConnection();
        conn.Open();

        using var cmd = new NpgsqlCommand(
            "SELECT id, password_hash, salt, email FROM users WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("username", username);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            byte[] storedHash = (byte[])reader["password_hash"];
            byte[] salt = (byte[])reader["salt"];
            int userId = reader.GetInt32(reader.GetOrdinal("id"));
            string email = reader.GetString(reader.GetOrdinal("email"));

            bool isValid = VerifyPassword(password, storedHash, salt);
            return (isValid, userId, email);
        }
        else
        {
            return (false, 0, null);
        }
    }


    private bool VerifyPassword(string password, byte[] storedHash, byte[] salt)
    {
        using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(
            password,
            salt,
            10000,
            System.Security.Cryptography.HashAlgorithmName.SHA256);

        byte[] computedHash = pbkdf2.GetBytes(32);
        return AreHashesEqual(storedHash, computedHash);
    }

    private bool AreHashesEqual(byte[] hash1, byte[] hash2)
    {
        if (hash1.Length != hash2.Length) return false;
        for (int i = 0; i < hash1.Length; i++)
        {
            if (hash1[i] != hash2[i]) return false;
        }
        return true;
    }

    private void GoToRegister_Click(object sender, RoutedEventArgs e)
    {
        RegistrationWindow regWindow = new RegistrationWindow();
        regWindow.Show();
        this.Close();
    }
}
