using Npgsql;
using NpgsqlTypes;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Windows;

namespace TaskManager.Views;

public partial class RegistrationWindow : Window
{
    private readonly DatabaseService _dbService = new();

    public RegistrationWindow()
    {
        InitializeComponent();
    }

    private void Register_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text) ||
                string.IsNullOrWhiteSpace(txtPassword.Password) ||
                string.IsNullOrWhiteSpace(txtEmail.Text))
            {
                MessageBox.Show("Все поля обязательны для заполнения!");
                return;
            }

            if (!IsValidEmail(txtEmail.Text))
            {
                MessageBox.Show("Некорректный формат email");
                return;
            }

            byte[] salt = GenerateSalt();
            byte[] hash = HashPassword(txtPassword.Password, salt);

            using (var conn = _dbService.GetConnection())
            {
                conn.Open();
                using var cmd = new NpgsqlCommand(
                    "INSERT INTO users (username, password_hash, salt, email) " +
                    "VALUES (@username, @hash, @salt, @email)", conn);

                cmd.Parameters.AddWithValue("username", txtUsername.Text);
                cmd.Parameters.Add(new NpgsqlParameter("hash", NpgsqlDbType.Bytea) { Value = hash });
                cmd.Parameters.Add(new NpgsqlParameter("salt", NpgsqlDbType.Bytea) { Value = salt });
                cmd.Parameters.AddWithValue("email", txtEmail.Text);

                cmd.ExecuteNonQuery();
            }

            MessageBox.Show("Регистрация успешно завершена!");
            Close();
        }
        catch (NpgsqlException ex)
        {
            Logger.LogError(ex);
        }
    }

    private void GoToLogin_Click(object sender, RoutedEventArgs e)
    {
        LoginWindow loginWindow = new LoginWindow();
        loginWindow.Show();
        this.Close();
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] GenerateSalt()
    {
        byte[] salt = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        return salt;
    }

    private static byte[] HashPassword(string password, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            salt,
            10000,
            HashAlgorithmName.SHA256);

        return pbkdf2.GetBytes(32);
    }
}
