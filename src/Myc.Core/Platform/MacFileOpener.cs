using System.ComponentModel;
using System.Diagnostics;

namespace Myc.Core.Platform;

public interface IFileOpener
{
    /// <summary>Opens <paramref name="path"/> with the default application. Returns an error, or null.</summary>
    string? Open(string path);

    /// <summary>Shows <paramref name="path"/> selected in Finder. Returns an error, or null.</summary>
    string? Reveal(string path);
}

public sealed class MacFileOpener : IFileOpener
{
    public string? Open(string path)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return "Opening files is only set up on macOS.";
        }

        try
        {
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                ArgumentList = { path },
                UseShellExecute = false,
            });
            return process is null ? "Could not open the file." : null;
        }
        catch (Exception exception) when (exception is Win32Exception or IOException)
        {
            return exception.Message;
        }
    }

    public string? Reveal(string path)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return "Finder is only available on macOS.";
        }

        try
        {
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                ArgumentList = { "-R", path },
                UseShellExecute = false,
            });
            return process is null ? "Could not reveal the item." : null;
        }
        catch (Exception exception) when (exception is Win32Exception or IOException)
        {
            return exception.Message;
        }
    }
}
