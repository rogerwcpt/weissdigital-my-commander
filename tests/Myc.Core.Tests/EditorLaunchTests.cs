using Myc.Core.Platform;

namespace Myc.Core.Tests;

public class EditorLaunchTests
{
    private const string Path = "/tmp/My File.txt";

    [Fact]
    public void A_settings_command_wins_over_the_environment()
    {
        ExternalEdit edit = EditorLaunch.Resolve(Path, "hx", "vim", "nano", EditorHost.Mac);

        ExternalEdit.Spawn spawn = Assert.IsType<ExternalEdit.Spawn>(edit);
        Assert.Equal("hx", spawn.FileName);
        Assert.Equal([Path], spawn.Arguments);
        Assert.True(spawn.TakeTerminal);
    }

    [Fact]
    public void Visual_wins_over_editor_when_settings_are_empty()
    {
        ExternalEdit edit = EditorLaunch.Resolve(Path, "  ", "vim", "nano", EditorHost.Mac);

        ExternalEdit.Spawn spawn = Assert.IsType<ExternalEdit.Spawn>(edit);
        Assert.Equal("vim", spawn.FileName);
        Assert.True(spawn.TakeTerminal);
    }

    [Fact]
    public void A_graphical_command_does_not_take_the_terminal()
    {
        ExternalEdit edit = EditorLaunch.Resolve(Path, "code --wait", null, "vim", EditorHost.Mac);

        ExternalEdit.Spawn spawn = Assert.IsType<ExternalEdit.Spawn>(edit);
        Assert.Equal("code", spawn.FileName);
        Assert.Equal(["--wait", Path], spawn.Arguments);
        Assert.False(spawn.TakeTerminal);
    }

    [Fact]
    public void The_file_token_is_replaced_and_not_appended_again()
    {
        ExternalEdit edit = EditorLaunch.Resolve(Path, "vim -f {file}", null, null, EditorHost.Linux);

        ExternalEdit.Spawn spawn = Assert.IsType<ExternalEdit.Spawn>(edit);
        Assert.Equal("vim", spawn.FileName);
        Assert.Equal(["-f", Path], spawn.Arguments);
        Assert.True(spawn.TakeTerminal);
    }

    [Fact]
    public void A_quoted_command_keeps_spaces_inside_one_argument()
    {
        ExternalEdit edit = EditorLaunch.Resolve(Path, "\"My Editor\" --goto \"{file}:1\"", null, null, EditorHost.Mac);

        ExternalEdit.Spawn spawn = Assert.IsType<ExternalEdit.Spawn>(edit);
        Assert.Equal("My Editor", spawn.FileName);
        Assert.Equal(["--goto", Path + ":1"], spawn.Arguments);
        Assert.True(spawn.TakeTerminal);
    }

    [Theory]
    [InlineData("vim")]
    [InlineData("nano")]
    [InlineData("/opt/homebrew/bin/hx")]
    [InlineData("fooedit")]
    [InlineData("vim.exe")]
    public void Terminal_and_unknown_editors_take_the_screen(string command)
    {
        ExternalEdit edit = EditorLaunch.Resolve(Path, command, null, null, EditorHost.Mac);

        Assert.True(Assert.IsType<ExternalEdit.Spawn>(edit).TakeTerminal);
    }

    [Theory]
    [InlineData("open -t")]
    [InlineData("cursor")]
    [InlineData("xdg-open")]
    [InlineData("gio launch org.gnome.TextEditor.desktop")]
    [InlineData("notepad.exe")]
    public void Graphical_launchers_stay_in_their_own_window(string command)
    {
        ExternalEdit edit = EditorLaunch.Resolve(Path, command, null, null, EditorHost.Mac);

        Assert.False(Assert.IsType<ExternalEdit.Spawn>(edit).TakeTerminal);
    }

    [Fact]
    public void Mac_falls_back_to_the_default_text_editor()
    {
        ExternalEdit edit = EditorLaunch.Resolve(Path, null, null, null, EditorHost.Mac);

        ExternalEdit.Spawn spawn = Assert.IsType<ExternalEdit.Spawn>(edit);
        Assert.Equal("open", spawn.FileName);
        Assert.Equal(["-t", "--", Path], spawn.Arguments);
        Assert.False(spawn.TakeTerminal);
    }

    [Fact]
    public void Linux_uses_the_text_plain_handler_when_one_was_found()
    {
        ExternalEdit edit = EditorLaunch.Resolve(Path, null, null, null, EditorHost.Linux, "org.gnome.TextEditor.desktop");

        ExternalEdit.Spawn spawn = Assert.IsType<ExternalEdit.Spawn>(edit);
        Assert.Equal("gio", spawn.FileName);
        Assert.Equal(["launch", "org.gnome.TextEditor.desktop", Path], spawn.Arguments);
        Assert.False(spawn.TakeTerminal);
    }

    [Fact]
    public void Linux_without_a_handler_uses_xdg_open()
    {
        ExternalEdit edit = EditorLaunch.Resolve(Path, null, "", null, EditorHost.Linux, "  ");

        ExternalEdit.Spawn spawn = Assert.IsType<ExternalEdit.Spawn>(edit);
        Assert.Equal("xdg-open", spawn.FileName);
        Assert.Equal([Path], spawn.Arguments);
        Assert.False(spawn.TakeTerminal);
    }

    [Fact]
    public void Windows_uses_the_edit_verb()
    {
        ExternalEdit edit = EditorLaunch.Resolve(@"C:\Notes.txt", null, null, null, EditorHost.Windows);

        ExternalEdit.Shell shell = Assert.IsType<ExternalEdit.Shell>(edit);
        Assert.Equal(@"C:\Notes.txt", shell.Path);
        Assert.Equal("edit", shell.Verb);
    }
}
