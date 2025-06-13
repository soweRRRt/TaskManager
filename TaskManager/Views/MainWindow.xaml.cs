using System.Windows;
using TaskManager.Services;

namespace TaskManager.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void TasksMenu_Click(object sender, RoutedEventArgs e)
    {
        //MainFrame.Navigate(new TasksPage());
    }

    private void ProfileMenu_Click(object sender, RoutedEventArgs e)
    {
        MainFrame.Navigate(new ProfilePage());
    }

    private void StatisticsMenu_Click(object sender, RoutedEventArgs e)
    {
        //MainFrame.Navigate(new StatisticsPage());
    }

    private void LogoutMenu_Click(object sender, RoutedEventArgs e)
    {
        UserSession.Instance.Clear();

        Settings.Default.SavedUserId = 0;
        Settings.Default.SavedUsername = "";
        Settings.Default.SavedEmail = "";
        Settings.Default.Save();

        LoginWindow login = new LoginWindow();
        login.Show();
        this.Close();
    }
}
