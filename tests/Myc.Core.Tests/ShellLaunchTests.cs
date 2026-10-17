using Myc.Core.Platform;

namespace Myc.Core.Tests;

public class ShellLaunchTests
{
    [Fact]
    public void An_absolute_shell_that_exists_is_used()
    {
        string shell = ShellLaunch.Resolve("/bin/zsh", path => path == "/bin/zsh");

        Assert.Equal("/bin/zsh", shell);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("zsh")]
    [InlineData("/missing/zsh")]
    public void Anything_else_falls_back_to_zsh(string? shell)
    {
        Assert.Equal(ShellLaunch.Fallback, ShellLaunch.Resolve(shell, _ => false));
    }
}
