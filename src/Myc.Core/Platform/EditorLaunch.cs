namespace Myc.Core.Platform;

public enum EditorHost
{
    Mac,
    Linux,
    Windows,
}

/// <summary>How to start an external editor. Nothing here starts a process.</summary>
public abstract record ExternalEdit
{
    private ExternalEdit()
    {
    }

    /// <summary>A program and its arguments. <see cref="TakeTerminal"/> means myc must leave the screen first.</summary>
    public sealed record Spawn(string FileName, IReadOnlyList<string> Arguments, bool TakeTerminal) : ExternalEdit;

    /// <summary>Windows shell verb. The starter tries <see cref="Verb"/> and then <c>open</c>.</summary>
    public sealed record Shell(string Path, string Verb) : ExternalEdit;
}

/// <summary>
/// Chooses the program for F4. A settings command wins, then <c>VISUAL</c>, then <c>EDITOR</c>,
/// then the operating system's text editor. <c>{file}</c> in a command is the path; otherwise
/// the path is one more argument. The command is not passed through a shell.
/// </summary>
public static class EditorLaunch
{
    private static readonly HashSet<string> GuiLaunchers = new(StringComparer.OrdinalIgnoreCase)
    {
        "open", "xdg-open", "gio", "gtk-launch", "start", "cmd", "explorer",
        "code", "cursor", "subl", "sublime_text", "mate", "gedit", "kate",
        "notepad", "notepad++", "textedit", "rider", "idea", "zed", "bbedit",
    };

    public static EditorHost CurrentHost =>
        OperatingSystem.IsWindows() ? EditorHost.Windows
        : OperatingSystem.IsMacOS() ? EditorHost.Mac
        : EditorHost.Linux;

    public static ExternalEdit Resolve(
        string path,
        string? configured,
        string? visual,
        string? editor,
        EditorHost host,
        string? textPlainHandler = null)
    {
        foreach (string? command in new[] { configured, visual, editor })
        {
            if (FromCommand(command, path) is { } edit)
            {
                return edit;
            }
        }

        return host switch
        {
            EditorHost.Mac => new ExternalEdit.Spawn("open", ["-t", "--", path], false),
            EditorHost.Windows => new ExternalEdit.Shell(path, "edit"),
            _ => LinuxDefault(path, textPlainHandler),
        };
    }

    private static ExternalEdit LinuxDefault(string path, string? textPlainHandler)
    {
        string handler = textPlainHandler?.Trim() ?? "";
        if (handler.Length > 0)
        {
            return new ExternalEdit.Spawn("gio", ["launch", handler, path], false);
        }

        return new ExternalEdit.Spawn("xdg-open", [path], false);
    }

    private static ExternalEdit? FromCommand(string? command, string path)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        string[] tokens = Split(command);
        if (tokens.Length == 0)
        {
            return null;
        }

        bool mentioned = false;
        var args = new List<string>(tokens.Length);
        for (int i = 1; i < tokens.Length; i++)
        {
            if (tokens[i].Contains("{file}", StringComparison.Ordinal))
            {
                mentioned = true;
            }

            args.Add(tokens[i].Replace("{file}", path, StringComparison.Ordinal));
        }

        string fileName = tokens[0];
        if (fileName.Contains("{file}", StringComparison.Ordinal))
        {
            mentioned = true;
            fileName = fileName.Replace("{file}", path, StringComparison.Ordinal);
        }

        if (!mentioned)
        {
            args.Add(path);
        }

        return new ExternalEdit.Spawn(fileName, args, TakesTerminal(fileName));
    }

    /// <summary>
    /// vim, nano, hx, and anything we do not recognise take the screen.
    /// Graphical launchers (open, code, cursor, xdg-open) do not.
    /// </summary>
    private static bool TakesTerminal(string fileName)
    {
        string name = Path.GetFileName(fileName);
        foreach (string extension in new[] { ".exe", ".cmd", ".bat", ".com" })
        {
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^extension.Length];
                break;
            }
        }

        return !GuiLaunchers.Contains(name);
    }

    private static string[] Split(string command)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        char quote = '\0';
        foreach (char c in command)
        {
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
                else
                {
                    current.Append(c);
                }

                continue;
            }

            if (c is '\'' or '"')
            {
                quote = c;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens.ToArray();
    }
}
