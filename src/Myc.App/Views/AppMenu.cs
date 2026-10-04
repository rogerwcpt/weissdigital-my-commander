using Myc.App.Theming;
using Myc.Core.Commands;
using Myc.Core.Configuration;
using Myc.Core.Sorting;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Myc.App.Views;

/// <summary>F9 menu. Every item runs, and the shortcut stays visible beside it.</summary>
internal sealed class AppMenu
{
    private const string SchemeName = "myc.menu";

    private readonly MenuBar _bar;
    private readonly MenuBarItem[] _menus;
    private readonly Func<ConfirmationSettings> _confirmations;
    private readonly Action<ConfirmationSettings> _saveConfirmations;
    private MenuItem _sortName = null!;
    private MenuItem _sortExtension = null!;
    private MenuItem _sortSize = null!;
    private MenuItem _sortModified = null!;
    private MenuItem _hidden = null!;
    private MenuItem _confirmDelete = null!;
    private MenuItem _confirmOverwrite = null!;
    private MenuItem _confirmTransfer = null!;

    public AppMenu(
        IApplication app,
        Action<MycCommand> invoke,
        Action chooseTheme,
        Action openSettings,
        Action showAbout,
        Func<ConfirmationSettings> confirmations,
        Action<ConfirmationSettings> saveConfirmations)
    {
        _confirmations = confirmations;
        _saveConfirmations = saveConfirmations;
        RegisterScheme();

        // MenuBar treats its key as "open the menu", and the library default is F10.
        // F10 quits myc, so the bar keeps a key nothing else uses. F9 is a normal command.
        MenuBar.DefaultKey = Key.F13;

        MenuBarItem file = File(app, invoke);
        MenuBarItem select = Select(app, invoke);
        MenuBarItem panel = PanelMenu(app, invoke);
        MenuBarItem options = Options(app, invoke, chooseTheme, openSettings, showAbout);
        _menus = [file, select, panel, options];
        _bar = new MenuBar(_menus)
        {
            SchemeName = SchemeName,
        };
        foreach (MenuBarItem item in _menus)
        {
            Paint(item);
        }
    }

    public MenuBar Bar => _bar;

    public bool IsOpen => _bar.IsOpen();

    public void Toggle() => _bar.InvokeCommand(IsOpen ? Command.Quit : Command.Activate);

    /// <summary>The Panel and Options menus show the active panel and the confirmation boxes.</summary>
    public void Reflect(string sort, bool descending, bool showHidden, ConfirmationSettings confirm)
    {
        string mark = descending ? " ▼" : " ▲";
        string mode = PanelSort.Normalize(sort);
        _sortName.Title = mode == PanelSort.Name ? "Name" + mark : "Name";
        _sortExtension.Title = mode == PanelSort.Extension ? "Extension" + mark : "Extension";
        _sortSize.Title = mode == PanelSort.Size ? "Size" + mark : "Size";
        _sortModified.Title = mode == PanelSort.Modified ? "Modified" + mark : "Modified";
        _hidden.Title = showHidden ? "☑ Show hidden files" : "☐ Show hidden files";
        _confirmDelete.Title = Box(confirm.Delete, "Delete");
        _confirmOverwrite.Title = Box(confirm.Overwrite, "Overwrite");
        _confirmTransfer.Title = Box(confirm.Transfer, "Copy/Move");
    }

    /// <summary>A theme change replaces the menu scheme. The bar looks the name up once, so it is set again.</summary>
    public void Restyle()
    {
        Scheme scheme = MenuScheme();
        if (SchemeManager.TryGetScheme(SchemeName, out _))
        {
            SchemeManager.RemoveScheme(SchemeName);
        }

        SchemeManager.AddScheme(SchemeName, scheme);
        _bar.SetScheme(scheme);
        foreach (MenuBarItem item in _menus)
        {
            Paint(item);
            if (item.PopoverMenu is { } popover)
            {
                popover.SetScheme(scheme);
                if (popover.Root is { } root)
                {
                    root.SetScheme(scheme);
                }
            }
        }

        _bar.SetNeedsDraw();
    }

    public void Close()
    {
        if (IsOpen)
        {
            _bar.InvokeCommand(Command.Quit);
        }
    }

    private static MenuBarItem File(IApplication app, Action<MycCommand> invoke) => new(
        "File",
        [
            Item("Copy…", Key.F5, Defer(app, invoke, MycCommand.Copy)),
            Item("Move…", Key.F6, Defer(app, invoke, MycCommand.Move)),
            Item("Rename…", Key.F2, Defer(app, invoke, MycCommand.Rename)),
            Item("New folder…", Key.F7, Defer(app, invoke, MycCommand.MakeDirectory)),
            Item("Move to Trash…", Key.F8, Defer(app, invoke, MycCommand.Delete)),
            Item("Open with default app", Key.Enter, Defer(app, invoke, MycCommand.Activate)),
            Item("Reveal in Finder", Key.O.WithCtrl, Defer(app, invoke, MycCommand.Reveal)),
            new Line { Orientation = Orientation.Horizontal },
            Item("Quit", Key.F10, Defer(app, invoke, MycCommand.Quit)),
        ]);

    private static MenuBarItem Select(IApplication app, Action<MycCommand> invoke) => new(
        "Select",
        [
            Item("Toggle mark", Key.Space, Defer(app, invoke, MycCommand.Mark)),
            Item("Select by pattern…", new Key('+'), Defer(app, invoke, MycCommand.SelectPattern)),
            Item("Deselect by pattern…", new Key('-'), Defer(app, invoke, MycCommand.DeselectPattern)),
            Item("Invert selection", new Key('*'), Defer(app, invoke, MycCommand.InvertSelection)),
            Item("Select all", Key.A.WithCtrl, Defer(app, invoke, MycCommand.SelectAll)),
            Item("Clear selection", Key.Esc, Defer(app, invoke, MycCommand.ClearMarks)),
        ]);

    private MenuBarItem PanelMenu(IApplication app, Action<MycCommand> invoke)
    {
        _sortName = Item("Name", Key.F3.WithCtrl, Defer(app, invoke, MycCommand.SortName));
        _sortExtension = Item("Extension", Key.F4.WithCtrl, Defer(app, invoke, MycCommand.SortExtension));
        _sortSize = Item("Size", Key.F5.WithCtrl, Defer(app, invoke, MycCommand.SortSize));
        _sortModified = Item("Modified", Key.F6.WithCtrl, Defer(app, invoke, MycCommand.SortModified));
        _hidden = Item("☑ Show hidden files", new Key('.').WithAlt, Defer(app, invoke, MycCommand.ToggleHidden));
        var modes = new Menu([_sortName, _sortExtension, _sortSize, _sortModified])
        {
            SchemeName = SchemeName,
            BorderStyle = LineStyle.Single,
        };

        return new MenuBarItem(
            "Panel",
            [
                new MenuItem("Sort by", "", modes),
                _hidden,
                Item("Go to path…", Key.G.WithCtrl, Defer(app, invoke, MycCommand.GoToPath)),
                Item("Refresh", Key.R.WithCtrl, Defer(app, invoke, MycCommand.Refresh)),
                Item("Same directory in other panel", new Key('=').WithAlt, Defer(app, invoke, MycCommand.SameDirectory)),
                Item("Swap panels", Key.U.WithCtrl, Defer(app, invoke, MycCommand.SwapPanels)),
            ]);
    }

    private MenuBarItem Options(
        IApplication app,
        Action<MycCommand> invoke,
        Action chooseTheme,
        Action openSettings,
        Action showAbout)
    {
        _confirmDelete = Dash("☑ Delete", Defer(app, () => ToggleConfirm(current => current with { Delete = !current.Delete })));
        _confirmOverwrite = Dash("☑ Overwrite", Defer(app, () => ToggleConfirm(current => current with { Overwrite = !current.Overwrite })));
        _confirmTransfer = Dash("☐ Copy/Move", Defer(app, () => ToggleConfirm(current => current with { Transfer = !current.Transfer })));
        var confirm = new Menu([_confirmDelete, _confirmOverwrite, _confirmTransfer])
        {
            SchemeName = SchemeName,
            BorderStyle = LineStyle.Single,
        };

        return new MenuBarItem(
            "Options",
            [
                new MenuItem("Confirm before", "", confirm),
                Dash("Theme…", Defer(app, chooseTheme)),
                Item("Key reference", Key.F1, Defer(app, invoke, MycCommand.Help)),
                Dash("Open settings file", Defer(app, openSettings)),
                Dash("About", Defer(app, showAbout)),
            ]);
    }

    private void ToggleConfirm(Func<ConfirmationSettings, ConfirmationSettings> change) =>
        _saveConfirmations(change(_confirmations()));

    private static string Box(bool on, string label) => (on ? "☑ " : "☐ ") + label;

    private static void Paint(MenuBarItem item)
    {
        if (item.PopoverMenu is not { } popover)
        {
            return;
        }

        popover.SchemeName = SchemeName;
        if (popover.Root is { } root)
        {
            root.SchemeName = SchemeName;
            root.BorderStyle = LineStyle.Single;
        }
    }

    /// <summary>
    /// The key stays visible. It is not a hotkey: the command already has one, and a second
    /// binding would run it twice. While the menu is open the popover still matches the key.
    /// </summary>
    private static MenuItem Item(string title, Key key, Action? action)
    {
        var item = new MenuItem(title, "", action, key)
        {
            Enabled = action is not null,
        };
        item.HotKeyBindings.Remove(key);
        return item;
    }

    /// <summary>An item with no shortcut. The dash stays visible so the column still lines up.</summary>
    private static MenuItem Dash(string title, Action action)
    {
        var item = new MenuItem(title, "", action) { Enabled = true };
        item.KeyView.Text = "—";
        return item;
    }

    private static Action Defer(IApplication app, Action<MycCommand> invoke, MycCommand command) =>
        () => app.AddTimeout(TimeSpan.Zero, () =>
        {
            invoke(command);
            return false;
        });

    private static Action Defer(IApplication app, Action action) =>
        () => app.AddTimeout(TimeSpan.Zero, () =>
        {
            action();
            return false;
        });

    private void RegisterScheme()
    {
        if (SchemeManager.TryGetScheme(SchemeName, out _))
        {
            SchemeManager.RemoveScheme(SchemeName);
        }

        SchemeManager.AddScheme(SchemeName, MenuScheme());
    }

    private static Scheme MenuScheme() => new()
    {
        Normal = Graphite.Bar.Normal,
        Focus = Graphite.Bar.Focus,
        Disabled = Graphite.Bar.Disabled,
        HotNormal = Graphite.Bar.Normal,
        HotFocus = Graphite.Bar.Focus,
        HotActive = Graphite.Bar.Normal,
        Highlight = Graphite.Bar.Normal,
        Active = Graphite.Bar.Focus,
    };
}
