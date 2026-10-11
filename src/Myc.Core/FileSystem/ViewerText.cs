using System.Text;

namespace Myc.Core.FileSystem;

/// <summary>
/// Reads one file for the viewer. A NUL in the first 8 KB means it is not text.
/// Anything over 1 MB is refused so the viewer does not parse a huge file.
/// </summary>
public static class ViewerText
{
    public const int MaxBytes = 1024 * 1024;

    public const int ProbeBytes = 8 * 1024;

    public readonly record struct Content(bool Ok, string Text, string? Error)
    {
        public static Content Ready(string text) => new(true, text, null);

        public static Content Refuse(string error) => new(false, "", error);
    }

    public static bool LooksLikeMarkdown(string name)
    {
        string ext = Path.GetExtension(name);
        return ext.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".markdown", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".mdown", StringComparison.OrdinalIgnoreCase);
    }

    public static Content Read(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return Content.Refuse("No such file.");
            }

            if ((info.Attributes & FileAttributes.Directory) != 0)
            {
                return Content.Refuse("Not a file.");
            }

            if (info.Length > MaxBytes)
            {
                return Content.Refuse("This file is larger than 1 MB.");
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            int count = (int)info.Length;
            byte[] bytes = new byte[count];
            int read = 0;
            while (read < count)
            {
                int n = stream.Read(bytes, read, count - read);
                if (n == 0)
                {
                    break;
                }

                read += n;
            }

            int probe = Math.Min(read, ProbeBytes);
            for (int i = 0; i < probe; i++)
            {
                if (bytes[i] == 0)
                {
                    return Content.Refuse("This is not a text file.");
                }
            }

            using var reader = new StreamReader(new MemoryStream(bytes, 0, read), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return Content.Ready(reader.ReadToEnd());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Content.Refuse(exception.Message);
        }
    }
}
