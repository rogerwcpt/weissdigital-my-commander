namespace Myc.Core.Display;

/// <summary>
/// Which columns fit in a panel. Modified is dropped first, then Size, and the name
/// column keeps whatever width is left.
/// </summary>
public readonly record struct PanelColumns(int Name, bool ShowSize, bool ShowModified)
{
    public const int SizeWidth = 7;
    public const int DateWidth = 16;

    public static PanelColumns For(int width)
    {
        const int nameMin = 12;
        bool showModified = width >= nameMin + 1 + SizeWidth + 1 + DateWidth;
        bool showSize = width >= nameMin + 1 + SizeWidth;
        int used = (showSize ? SizeWidth + 1 : 0) + (showModified ? DateWidth + 1 : 0);
        return new PanelColumns(Math.Max(0, width - used), showSize, showModified);
    }
}
