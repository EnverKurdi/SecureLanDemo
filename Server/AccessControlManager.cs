namespace ServerApp;

public sealed class AccessControlManager
{
    public const string FolderGroup2 = "Folder_Group2";
    public const string FolderGroup3 = "Folder_Group3";

    private static readonly Dictionary<string, string> UserToGroup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UserAdmin"] = "Group1",

        ["userA"] = "Group2",
        ["userB"] = "Group2",
        ["userE"] = "Group2",

        ["userC"] = "Group3",
        ["userD"] = "Group3",
        ["userF"] = "Group3",
    };

    public bool HasPermission(string user, string resourceFolder, string action)
    {
        var group = UserToGroup.TryGetValue(user, out var g) ? g : "Unknown";

        // Group1 (admin): alt
        if (group == "Group1") return true;

        // Group2: kun Folder_Group2
        if (group == "Group2")
            return resourceFolder.Equals(FolderGroup2, StringComparison.OrdinalIgnoreCase);

        // Group3: kun Folder_Group3
        if (group == "Group3")
            return resourceFolder.Equals(FolderGroup3, StringComparison.OrdinalIgnoreCase);

        return false;
    }

    public IEnumerable<string> AllowedFolders(string user)
    {
        var group = UserToGroup.TryGetValue(user, out var g) ? g : "Unknown";
        return group switch
        {
            "Group1" => new[] { FolderGroup2, FolderGroup3 },
            "Group2" => new[] { FolderGroup2 },
            "Group3" => new[] { FolderGroup3 },
            _ => Array.Empty<string>()
        };
    }
}
