using Microsoft.Win32;
using Npgsql;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TaskManager.Services;

namespace TaskManager.Views;

public partial class ProfilePage : Page
{
    private readonly DatabaseService _dbService = new();
    private string? _avatarFilePath; // Путь к выбранной аватарке
    private string? _emailConfirmationCode; // Код подтверждения email
    private DateTime _emailCodeSentTime;

    public ProfilePage()
    {
        InitializeComponent();
        LoadUserData();
    }

    private void LoadUserData()
    {
        var user = UserSession.Instance;
        txtDisplayName.Text = user.Username ?? "";
        txtEmail.Text = user.Email ?? "";

        // Загрузка аватарки из базы или файла
        LoadAvatarFromDatabase(user.UserId);

        // Проверяем, подтверждён ли email и включена ли 2FA
        bool emailConfirmed = CheckEmailConfirmed(user.UserId);
        UpdateEmailConfirmationUI(emailConfirmed);
        UpdateEmailStatus(emailConfirmed);
        chkTwoFactor.IsEnabled = emailConfirmed;
        chkTwoFactor.IsChecked = CheckTwoFactorEnabled(user.UserId);

    }

    private void UpdateEmailStatus(bool confirmed)
    {
        if (confirmed)
        {
            txtEmailStatus.Text = "Email подтвержён";
            txtEmailStatus.Foreground = new SolidColorBrush(Colors.Green);
        }
        else
        {
            txtEmailStatus.Text = "Email не подтвержён";
            txtEmailStatus.Foreground = new SolidColorBrush(Colors.Red);
        }
    }

    private void LoadAvatarFromDatabase(int userId)
    {
        try
        {
            using var conn = _dbService.GetConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand("SELECT avatar FROM users WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", userId);

            var result = cmd.ExecuteScalar();
            if (result != DBNull.Value && result is byte[] avatarBytes)
            {
                using var ms = new MemoryStream(avatarBytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                imgAvatar.Source = bitmap;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }

    private bool CheckEmailConfirmed(int userId)
    {
        try
        {
            using var conn = _dbService.GetConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand("SELECT email_confirmed FROM users WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", userId);
            var result = cmd.ExecuteScalar();

            if (result == null || result == DBNull.Value)
                return false;

            return Convert.ToBoolean(result);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            return false;
        }
    }

    private bool CheckTwoFactorEnabled(int userId)
    {
        try
        {
            using var conn = _dbService.GetConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand("SELECT two_factor_enabled FROM users WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", userId);
            var result = cmd.ExecuteScalar();

            if (result == null || result == DBNull.Value)
                return false;

            return Convert.ToBoolean(result);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            return false;
        }
    }


    private void SelectAvatar_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Изображения (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            Title = "Выберите аватарку"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            _avatarFilePath = openFileDialog.FileName;
            imgAvatar.Source = new BitmapImage(new Uri(_avatarFilePath));
        }
    }

    private void SaveAvatar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_avatarFilePath))
        {
            MessageBox.Show("Пожалуйста, выберите файл аватарки.");
            return;
        }

        try
        {
            byte[] avatarBytes = File.ReadAllBytes(_avatarFilePath);
            using var conn = _dbService.GetConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand("UPDATE users SET avatar = @avatar WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("avatar", avatarBytes);
            cmd.Parameters.AddWithValue("id", UserSession.Instance.UserId);
            cmd.ExecuteNonQuery();

            MessageBox.Show("Аватарка успешно сохранена.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            MessageBox.Show("Ошибка при сохранении аватарки.");
        }
    }

    private void SendEmailConfirmationCode_Click(object sender, RoutedEventArgs e)
    {
        string email = txtEmail.Text.Trim();
        if (!IsValidEmail(email))
        {
            MessageBox.Show("Введите корректный email.");
            return;
        }

        _emailConfirmationCode = GenerateConfirmationCode();
        _emailCodeSentTime = DateTime.Now;

        try
        {
            SendEmail(email, "Код подтверждения", $"Ваш код подтверждения: {_emailConfirmationCode}");
            MessageBox.Show("Код подтверждения отправлен на email.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            MessageBox.Show("Ошибка при отправке email.");
        }
    }

    private void ConfirmEmail_Click(object sender, RoutedEventArgs e)
    {
        if (_emailConfirmationCode == null)
        {
            MessageBox.Show("Сначала отправьте код подтверждения.");
            return;
        }

        if ((DateTime.Now - _emailCodeSentTime).TotalMinutes > 10)
        {
            MessageBox.Show("Время действия кода истекло. Отправьте код заново.");
            return;
        }

        if (txtEmailCode.Text.Trim() == _emailConfirmationCode)
        {
            try
            {
                using var conn = _dbService.GetConnection();
                conn.Open();
                using var cmd = new NpgsqlCommand("UPDATE users SET email = @newEmail, email_confirmed = TRUE WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("newEmail", txtEmail.Text.Trim());
                cmd.Parameters.AddWithValue("id", UserSession.Instance.UserId);
                cmd.ExecuteNonQuery();

                UserSession.Instance.UpdateEmail(txtEmail.Text.Trim());
                Settings.Default.SavedEmail = txtEmail.Text.Trim();
                Settings.Default.Save();
                UpdateEmailStatus(true);
                UpdateEmailConfirmationUI(true);
                chkTwoFactor.IsEnabled = true;

                MessageBox.Show("Email успешно подтверждён.");
                _emailConfirmationCode = null;
                txtEmailCode.Clear();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                MessageBox.Show("Ошибка при подтверждении email.");
            }
        }
        else
        {
            MessageBox.Show("Неверный код подтверждения.");
        }
    }

    private void TwoFactor_Checked(object sender, RoutedEventArgs e)
    {
        UpdateTwoFactorStatus(true);
    }

    private void TwoFactor_Unchecked(object sender, RoutedEventArgs e)
    {
        UpdateTwoFactorStatus(false);
    }

    private void UpdateTwoFactorStatus(bool enabled)
    {
        try
        {
            using var conn = _dbService.GetConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand("UPDATE users SET two_factor_enabled = @enabled WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("enabled", enabled);
            cmd.Parameters.AddWithValue("id", UserSession.Instance.UserId);
            cmd.ExecuteNonQuery();

            MessageBox.Show($"Двухфакторная аутентификация {(enabled ? "включена" : "выключена")}.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            MessageBox.Show("Ошибка при обновлении настроек 2FA.");
            // Откат переключателя
            chkTwoFactor.Checked -= TwoFactor_Checked;
            chkTwoFactor.Unchecked -= TwoFactor_Unchecked;
            chkTwoFactor.IsChecked = !enabled;
            chkTwoFactor.Checked += TwoFactor_Checked;
            chkTwoFactor.Unchecked += TwoFactor_Unchecked;
        }
    }

    private void UpdateEmailConfirmationUI(bool emailConfirmed)
    {
        if (emailConfirmed)
        {
            emailConfirmationPanel.Visibility = Visibility.Collapsed;
            txtEmailStatus.Text = "Email подтвержён";
            txtEmailStatus.Foreground = new SolidColorBrush(Colors.Green);
            chkTwoFactor.IsEnabled = true;
        }
        else
        {
            emailConfirmationPanel.Visibility = Visibility.Visible;
            txtEmailStatus.Text = "Email не подтвержён";
            txtEmailStatus.Foreground = new SolidColorBrush(Colors.Red);
            chkTwoFactor.IsEnabled = false;
            chkTwoFactor.IsChecked = false;
        }
    }


    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        string newDisplayName = txtDisplayName.Text.Trim();
        string newEmail = txtEmail.Text.Trim();

        if (string.IsNullOrEmpty(newDisplayName))
        {
            MessageBox.Show("Имя отображения не может быть пустым.");
            return;
        }

        if (!IsValidEmail(newEmail))
        {
            MessageBox.Show("Введите корректный email.");
            return;
        }

        try
        {
            using var conn = _dbService.GetConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand("UPDATE users SET username = @username, email = @email, email_confirmed = FALSE WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("username", newDisplayName);
            cmd.Parameters.AddWithValue("email", newEmail);
            cmd.Parameters.AddWithValue("id", UserSession.Instance.UserId);
            cmd.ExecuteNonQuery();

            UserSession.Instance.UpdateUsername(newDisplayName);
            UserSession.Instance.UpdateEmail(newEmail);
            Settings.Default.SavedUsername = newDisplayName;
            Settings.Default.SavedEmail = newEmail;
            Settings.Default.Save();
            UpdateEmailStatus(false);
            UpdateEmailConfirmationUI(false);
            chkTwoFactor.IsEnabled = false;
            chkTwoFactor.IsChecked = false;

            MessageBox.Show("Профиль успешно обновлён. Пожалуйста, подтвердите новый email.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            MessageBox.Show("Ошибка при сохранении профиля.");
        }
    }

    // Вспомогательные методы

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string GenerateConfirmationCode()
    {
        using var rng = RandomNumberGenerator.Create();
        byte[] data = new byte[4];
        rng.GetBytes(data);
        int code = BitConverter.ToInt32(data, 0);
        code = Math.Abs(code % 1000000); // 6 цифр
        return code.ToString("D6");
    }

    private void SendEmail(string toEmail, string subject, string body)
    {
        // Настройте SMTP по вашему почтовому провайдеру
        var smtpClient = new SmtpClient("smtp.gmail.com")
        {
            Port = 587,
            Credentials = new NetworkCredential("taskmanagerbysowert@gmail.com", "csli zxgg kwzk mbdj"),
            EnableSsl = true,
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress("taskmanagerbysowert@gmail.com", "TaskManager Support"),
            Subject = subject,
            Body = $"<p>{body}</p><p>С уважением,<br/>Команда TaskManager</p>",
            IsBodyHtml = true,
        };

        mailMessage.To.Add(toEmail);
        mailMessage.ReplyToList.Add(new MailAddress("taskmanagerbysowert@gmail.com"));
        mailMessage.Headers.Add("X-Mailer", "TaskManagerApp");

        try
        {
            smtpClient.Send(mailMessage);
        }
        catch (SmtpException ex)
        {
            Logger.LogError(ex);
            MessageBox.Show("Ошибка при отправке письма. Попробуйте позже.");
        }
    }
}
