using Myc.Core.Commands;

namespace Myc.Core.Tests;

public class CommandCatalogTests
{
    [Theory]
    [InlineData(KeyToken.D1, KeyToken.F1, MycCommand.Help)]
    [InlineData(KeyToken.D5, KeyToken.F5, MycCommand.Copy)]
    [InlineData(KeyToken.D0, KeyToken.F10, MycCommand.Quit)]
    [InlineData(KeyToken.Alt5, KeyToken.F5, MycCommand.Copy)]
    [InlineData(KeyToken.Alt0, KeyToken.F10, MycCommand.Quit)]
    public void Digit_fallbacks_use_the_function_key_command(KeyToken pressed, KeyToken function, MycCommand command)
    {
        Assert.Equal(function, CommandCatalog.AsFunctionKey(pressed));
        Assert.Equal(command, CommandCatalog.Find(function)!.Command);
    }

    [Fact]
    public void A_function_key_is_not_an_alias_of_itself()
    {
        Assert.Null(CommandCatalog.AsFunctionKey(KeyToken.F5));
        Assert.Null(CommandCatalog.AsFunctionKey(KeyToken.Esc));
    }

    [Fact]
    public void Each_key_belongs_to_one_command()
    {
        var seen = new HashSet<KeyToken>();
        foreach (CommandSpec spec in CommandCatalog.All)
        {
            foreach (KeyToken key in spec.Keys)
            {
                Assert.True(seen.Add(key), key.ToString());
            }
        }
    }

    [Fact]
    public void The_bar_is_F1_through_F10_in_order()
    {
        Assert.Equal(
            [KeyToken.F1, KeyToken.F2, KeyToken.F3, KeyToken.F4, KeyToken.F5, KeyToken.F6, KeyToken.F7, KeyToken.F8, KeyToken.F9, KeyToken.F10],
            CommandCatalog.Bar.Select(spec => spec.Keys[0]));
    }

    [Fact]
    public void Help_is_generated_from_the_table()
    {
        string help = CommandCatalog.HelpText();

        Assert.Contains("F5   Copy", help);
        Assert.Contains("F3   View", help);
        Assert.Contains("F4   Edit", help);
        Assert.Contains("Ctrl+Shift+P", help);
        Assert.Contains("Copy path", help);
        Assert.Contains("New file", help);
        Assert.Contains("View certificate", help);
        Assert.Contains("View JWT", help);
        Assert.Contains("Checksum", help);
        Assert.Contains("Duplicate", help);
        Assert.Contains(CommandCatalog.Palette, spec => spec.Command == MycCommand.ViewCertificate);
        Assert.Contains(CommandCatalog.Palette, spec => spec.Command == MycCommand.Duplicate);
        Assert.True(CommandCatalog.Find(KeyToken.F4)!.Available);
        Assert.Equal(MycCommand.Edit, CommandCatalog.Find(KeyToken.F4)!.Command);
        Assert.Contains(CommandCatalog.Palette, spec => spec.Command == MycCommand.Copy);
        Assert.Contains(CommandCatalog.Palette, spec => spec.Command == MycCommand.Edit);
        Assert.DoesNotContain(CommandCatalog.Palette, spec => spec.Command == MycCommand.Menu);
        Assert.DoesNotContain(CommandCatalog.Palette, spec => spec.Command == MycCommand.MoveCursor);
        Assert.Contains("F9   Menu", help);
        Assert.True(CommandCatalog.Find(KeyToken.F9)!.Available);
        Assert.Contains("F10  Quit", help);
        Assert.Contains("Esc, then 1-9 or 0", help);
        Assert.Contains("Esc closes a dialog", help);
        Assert.Contains("Option+digit", help);
        Assert.Contains("Shift+F8", help);
        Assert.Contains("Ctrl+R", help);
        Assert.Contains("Ctrl+F3", help);
        Assert.Contains("Alt+.", help);
        Assert.Contains("Clear marks", help);
        Assert.Contains("Ctrl+A", help);
        Assert.Contains("matching a pattern", help);
        Assert.Contains("Ctrl+G", help);
        Assert.Contains("Ctrl+E", help);
        Assert.Contains("Ctrl+U", help);
        Assert.Contains("Ctrl+O", help);
        Assert.Contains("Alt+O", help);
        Assert.Contains("Alt+C", help);
        Assert.Contains("Open a shell in this folder", help);
        Assert.Contains("Copy the name of this file or folder", help);
        Assert.Contains("The sequence clears after 2 seconds", help);
        Assert.Contains("Show this item in Finder", help);
        Assert.Contains(CommandCatalog.Palette, spec => spec.Command == MycCommand.Shell);

        int lines = help.Split('\n').Length;
        Assert.InRange(lines, 10, 48);
    }
}
