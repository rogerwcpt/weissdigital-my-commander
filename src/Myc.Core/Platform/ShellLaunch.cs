namespace Myc.Core.Platform;

/// <summary>Which program to hand the terminal to. Nothing here starts a process.</summary>
public static class ShellLaunch
{
    public const string Fallback = "/bin/zsh";

    /// <summary>
    /// <paramref name="shell"/> when it is an absolute path that exists.
    /// A relative value, a blank value, and a missing file all use <see cref="Fallback"/>.
    /// </summary>
    public static string Resolve(string? shell, Func<string, bool> exists)
    {
        if (!string.IsNullOrWhiteSpace(shell))
        {
            string candidate = shell.Trim();
            if (Path.IsPathRooted(candidate) && exists(candidate))
            {
                return candidate;
            }
        }

        return Fallback;
    }
}
