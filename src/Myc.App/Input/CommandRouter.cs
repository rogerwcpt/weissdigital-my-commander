using Myc.App.Theming;
using Myc.App.Views;
using Myc.Core.Commands;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Myc.App.Input;

/// <summary>
/// App-wide keys: F1, Ctrl+R, and the Esc / Option digit fallbacks.
/// Esc followed by anything other than a digit clears marks, then that key is handled normally.
/// </summary>
internal sealed class CommandRouter
{
    private readonly IApplication _app;
    private readonly Window _window;
    private bool _escapePending;

    public CommandRouter(IApplication app, Window window)
    {
        _app = app;
        _window = window;
    }

    public void Attach() => _app.Keyboard.KeyDown += OnKeyDown;

    public void ShowHelp()
    {
        string help = CommandCatalog.HelpText();
        int lines = help.Split('\n').Length;
        var body = new Label
        {
            Text = help,
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
            Height = Dim.Fill(1),
        };
        var dialog = new Dialog
        {
            Title = "Keys",
            Width = 72,
            Height = lines + 4,
        };
        dialog.SetScheme(Graphite.Bar);
        dialog.Add(body);
        dialog.AddButton(new Button { Text = "Close", IsDefault = true });
        _app.Run(dialog);
    }

    private void OnKeyDown(object? sender, Key key)
    {
        if (!ReferenceEquals(_app.TopRunnable, _window))
        {
            return;
        }

        if (_escapePending)
        {
            _escapePending = false;
            if (KeyMap.Match(key) is KeyToken pending && CommandCatalog.AsFunctionKey(pending) is KeyToken function)
            {
                key.Handled = true;
                Run(function);
                return;
            }

            ActivePanel()?.ClearMarks();
            if (key == Key.Esc)
            {
                key.Handled = true;
                return;
            }
        }

        if (key == Key.Esc)
        {
            _escapePending = true;
            key.Handled = true;
            return;
        }

        if (KeyMap.Match(key) is not KeyToken pressed)
        {
            return;
        }

        if (CommandCatalog.AsFunctionKey(pressed) is KeyToken aliased)
        {
            key.Handled = true;
            Run(aliased);
            return;
        }

        if (pressed == KeyToken.CtrlR)
        {
            key.Handled = true;
            ActivePanel()?.Refresh();
        }
    }

    private void Run(KeyToken functionKey)
    {
        if (CommandCatalog.Find(functionKey) is not { Available: true } spec)
        {
            return;
        }

        switch (spec.Command)
        {
            case MycCommand.Help:
                ShowHelp();
                break;
            case MycCommand.Quit:
                _app.RequestStop();
                break;
        }
    }

    private FilePanelView? ActivePanel() => _window.MostFocused as FilePanelView;
}
