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
        Assert.Contains("F10  Quit", help);
        Assert.Contains("Esc, then 1-9 or 0", help);
        Assert.Contains("Option+digit", help);
        Assert.Contains("Shift+F8", help);
        Assert.Contains("Ctrl+R", help);
        Assert.Contains("Clear marks", help);

        int lines = help.Split('\n').Length;
        Assert.InRange(lines, 10, 16);
    }
}
