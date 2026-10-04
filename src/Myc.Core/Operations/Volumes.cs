namespace Myc.Core.Operations;

/// <summary>Whether two paths sit on the same mount. A same-mount move can be a rename.</summary>
public static class Volumes
{
    public static bool Same(string left, string right) =>
        string.Equals(Mount(left), Mount(right), StringComparison.Ordinal);

    public static string Mount(string path)
    {
        string full = Path.GetFullPath(path);
        string best = "/";
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            string name = drive.Name.TrimEnd(Path.DirectorySeparatorChar);
            if (name.Length == 0)
            {
                name = "/";
            }

            bool match = name == "/"
                ? full.StartsWith('/')
                : full.Equals(name, StringComparison.Ordinal)
                  || full.StartsWith(name + Path.DirectorySeparatorChar, StringComparison.Ordinal);
            if (match && name.Length >= best.Length)
            {
                best = name;
            }
        }

        return best;
    }
}
