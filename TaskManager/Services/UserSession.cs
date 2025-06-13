namespace TaskManager.Services;

public sealed class UserSession
{
    private static readonly Lazy<UserSession> lazy = new(() => new UserSession());

    public static UserSession Instance => lazy.Value;

    private UserSession() { }

    public int UserId { get; private set; }
    public string? Username { get; private set; }
    public string? Email { get; private set; }

    public bool IsLoggedIn => UserId > 0;

    public void SetUser(int userId, string? username, string? email)
    {
        UserId = userId;
        Username = username;
        Email = email;
    }

    public void Clear()
    {
        UserId = 0;
        Username = null;
        Email = null;
    }

    public void UpdateUsername(string username)
    {
        Username = username;
    }

    public void UpdateEmail(string email)
    {
        Email = email;
    }

}
