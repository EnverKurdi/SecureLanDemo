namespace ServerApp;

public sealed class UserAuthenticator
{
    private readonly Dictionary<string, (string Password, string Group)> _users = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UserAdmin"] = ("adminpass", "Group1"),

        ["userA"] = ("passA", "Group2"),
        ["userB"] = ("passB", "Group2"),
        ["userE"] = ("passE", "Group2"),

        ["userC"] = ("passC", "Group3"),
        ["userD"] = ("passD", "Group3"),
        ["userF"] = ("passF", "Group3"),
    };

    public bool Authenticate(string user, string pass, out string group)
    {
        group = "";
        if (!_users.TryGetValue(user, out var entry)) return false;
        if (!string.Equals(entry.Password, pass, StringComparison.Ordinal)) return false;
        group = entry.Group;
        return true;
    }
}
