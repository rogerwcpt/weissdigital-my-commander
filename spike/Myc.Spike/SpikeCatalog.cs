namespace Myc.Spike;

/// <summary>
/// Synthetic directory used by both panels. The first rows are Unicode cases
/// (NFC, NFD, wide CJK, emoji); the rest pad the list out to <see cref="Count"/>.
/// </summary>
public static class SpikeCatalog
{
    public const int Count = 50_000;

    public static IReadOnlyList<SpikeEntry> Create()
    {
        List<SpikeEntry> entries =
        [
            new("café.txt", false, "NFC é"),
            new("cafe\u0301.txt", false, "NFD e + acute"),
            new("naïve.md", false, "NFC ï"),
            new("nai\u0308ve.md", false, "NFD i + diaeresis"),
            new("文件.txt", false, "CJK, width 2"),
            new("写真フォルダ", true, "CJK directory"),
            new("reef-\U0001F420.jpg", false, "emoji (tropical fish)"),
            new("README", false, "ASCII"),
        ];

        for (int i = entries.Count; i < Count; i++)
        {
            bool directory = i % 17 == 0;
            entries.Add(new(directory ? $"folder-{i:000000}" : $"file-{i:000000}.dat", directory, ""));
        }

        return entries;
    }
}

public readonly record struct SpikeEntry(string Name, bool IsDirectory, string Note);
