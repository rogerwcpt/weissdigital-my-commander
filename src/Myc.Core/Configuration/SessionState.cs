using Myc.Core.Sorting;

namespace Myc.Core.Configuration;

/// <summary>One panel as remembered between runs. Sort is a name such as <c>name</c>.</summary>
public sealed record PanelState
{
    public string Directory { get; init; } = "";

    public string Sort { get; init; } = PanelSort.Name;

    public bool ShowHidden { get; init; } = true;

    public bool Descending { get; init; }

    public static PanelState Default(string directory) => new() { Directory = directory };
}

/// <summary>Last place the two panels were, and which one had the keyboard.</summary>
public sealed record SessionState
{
    public const string LeftPanel = "left";
    public const string RightPanel = "right";

    public PanelState Left { get; init; } = new();

    public PanelState Right { get; init; } = new();

    public string Active { get; init; } = LeftPanel;

    public static SessionState Default(string leftDirectory, string rightDirectory) => new()
    {
        Left = PanelState.Default(leftDirectory),
        Right = PanelState.Default(rightDirectory),
    };
}
