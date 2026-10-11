using System.ComponentModel;
using System.Diagnostics;

namespace Myc.Core.Platform;

public interface IFileOpener
{
    /// <summary>Opens <paramref name="path"/> with the default application. Returns an error, or null.</summary>
    string? Open(string path);

    /// <summary>Shows <paramref name="path"/> selected in Finder. Returns an error, or null.</summary>
    string? Reveal(string path);

    /// <summary>
    /// Starts a graphical editor or a Windows shell verb. A spawn that takes the terminal is refused;
    /// the app has to hand the screen over first.
    /// </summary>
    string? Start(ExternalEdit edit);
}

public sealed class SystemFileOpener : IFileOpener
{
    public string? Open(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return Shell(path, "open", fallbackOpen: false);
        }

        if (OperatingSystem.IsMacOS())
        {
            return Spawn("open", [path], wait: false);
        }

        return Spawn("xdg-open", [path], wait: false);
    }

    public string? Reveal(string path)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return "Finder is only available on macOS.";
        }

        return Spawn("open", ["-R", path], wait: false);
    }

    public string? Start(ExternalEdit edit) => edit switch
    {
        ExternalEdit.Spawn { TakeTerminal: true } => "That editor needs the terminal.",
        ExternalEdit.Spawn spawn => Spawn(spawn.FileName, spawn.Arguments, wait: false),
        ExternalEdit.Shell shell => Shell(shell.Path, shell.Verb, fallbackOpen: true),
        _ => "Could not edit the file.",
    };

    /// <summary>The desktop id that opens <c>text/plain</c>, or null when it cannot be asked.</summary>
    public static string? TextPlainHandler()
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            return null;
        }

        try
        {
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-mime",
                ArgumentList = { "query", "default", "text/plain" },
                RedirectStandardOutput = true,
                UseShellExecute = false,
            });
            if (process is null || !process.WaitForExit(2000))
            {
                try
                {
                    process?.Kill(entireProcessTree: true);
                }
                catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
                {
                }

                return null;
            }

            string handler = process.StandardOutput.ReadToEnd().Trim();
            return handler.EndsWith(".desktop", StringComparison.Ordinal) ? handler : null;
        }
        catch (Exception exception) when (exception is Win32Exception or IOException)
        {
            return null;
        }
    }

    private static string? Spawn(string fileName, IReadOnlyList<string> arguments, bool wait)
    {
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
            };
            foreach (string argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using Process? process = Process.Start(start);
            if (process is null)
            {
                return "Could not start " + fileName + ".";
            }

            if (wait)
            {
                process.WaitForExit();
            }

            return null;
        }
        catch (Exception exception) when (exception is Win32Exception or IOException)
        {
            return exception.Message;
        }
    }

    private static string? Shell(string path, string verb, bool fallbackOpen)
    {
        string? error = ShellOnce(path, verb);
        if (error is null || !fallbackOpen || verb == "open")
        {
            return error;
        }

        return ShellOnce(path, "open");
    }

    private static string? ShellOnce(string path, string verb)
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = verb,
            });
            return process is null && verb.Length == 0 ? "Could not open the file." : null;
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            return exception.Message;
        }
    }
}
