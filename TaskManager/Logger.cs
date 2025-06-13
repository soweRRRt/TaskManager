using System.IO;
using System.Windows;

namespace TaskManager;

public static class Logger
{
    private static readonly string exeFolder = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string errorLogFilePath = Path.Combine(exeFolder, "error.log");
    private static readonly string loginAttemptsLogFilePath = Path.Combine(exeFolder, "login_attempts.log");

    public static void LogError(Exception ex)
    {
        try
        {
            using StreamWriter sw = File.AppendText(errorLogFilePath);
            sw.WriteLine($"[{DateTime.Now}] [{ex.GetType()}] [{ex.Message}]");
        }
        catch
        {
            // Игнорируем ошибки логирования, чтобы не ломать приложение
        }

        MessageBox.Show("Ошибка: " + ex.Message);
    }

    public static void LogLoginAttempt(string username, bool success)
    {
        try
        {
            string message = $"{DateTime.Now}: Попытка входа пользователя '{username}' - {(success ? "Успешно" : "Неудачно")}";
            File.AppendAllText(loginAttemptsLogFilePath, message + Environment.NewLine);
        }
        catch
        {
            // Игнорируем ошибки логирования
        }
    }
}
