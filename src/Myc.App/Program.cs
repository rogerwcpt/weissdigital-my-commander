using Myc.App.Input;
using Myc.App.Theming;
using Myc.App.Views;
using Myc.Core.Configuration;
using Myc.Core.FileSystem;
using Myc.Core.Platform;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

Application.SetDefaultKeyBinding(Command.Quit, Bind.All(Key.F10, Key.Q.WithCtrl));

using IApplication app = Application.Create();
app.Init();

var store = new ConfigStore();
AppSettings settings = store.EnsureSettings();
Themes.Use(settings.Theme);

var files = new LocalFileSystem();
var opener = new MacFileOpener();
var left = new FilePanelView(files, opener)
{
    X = 0,
    Y = 0,
    Width = Dim.Percent(50),
    Height = Dim.Fill(1),
};
var right = new FilePanelView(files, opener)
{
    X = Pos.Right(left),
    Y = 0,
    Width = Dim.Fill(),
    Height = Dim.Fill(1),
};

var window = new Window { Title = "myc" };
var router = new CommandRouter(app, window, files, left, right);
router.UseConfirmations(() => settings.Confirm);
var menu = new AppMenu(app, router.Invoke, ChooseTheme, OpenSettings, () => AboutPrompt.Show(app), () => settings.Confirm, SaveConfirm);
var bar = FunctionKeyBar.Create(router.Invoke);
using var leftWatch = new DirectoryWatcher(() => app.Invoke(() => left.ReloadKeepingCursor()));
using var rightWatch = new DirectoryWatcher(() => app.Invoke(() => right.ReloadKeepingCursor()));
left.DirectoryChanged += () => leftWatch.Watch(left.Directory);
right.DirectoryChanged += () => rightWatch.Watch(right.Directory);
router.UseMenu(menu);
left.Y = Pos.Bottom(menu.Bar);
right.Y = Pos.Bottom(menu.Bar);
window.SetScheme(Graphite.Scheme);
window.Add(menu.Bar, left, right, bar);
router.Attach();
Themes.Changed += Restyle;

string currentDirectory = Directory.GetCurrentDirectory();
string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
SessionState remembered = store.LoadState(currentDirectory, home);
bool restore = settings.Startup != AppSettings.DefaultStartup;

left.ShowHidden = restore ? remembered.Left.ShowHidden : true;
right.ShowHidden = restore ? remembered.Right.ShowHidden : true;
if (restore)
{
    left.UseSort(remembered.Left.Sort, remembered.Left.Descending);
    right.UseSort(remembered.Right.Sort, remembered.Right.Descending);
}
left.Open(restore
    ? SessionPaths.ResolveDirectory(remembered.Left.Directory, currentDirectory)
    : currentDirectory);
right.Open(restore
    ? SessionPaths.ResolveDirectory(remembered.Right.Directory, home)
    : home);
if (restore && remembered.Active == SessionState.RightPanel)
{
    right.SetFocus();
}
else
{
    left.SetFocus();
}

app.Run(window);
leftWatch.Dispose();
rightWatch.Dispose();
store.SaveState(new SessionState
{
    Left = new PanelState
    {
        Directory = left.Directory,
        Sort = left.Sort,
        ShowHidden = left.ShowHidden,
        Descending = left.SortDescending,
    },
    Right = new PanelState
    {
        Directory = right.Directory,
        Sort = right.Sort,
        ShowHidden = right.ShowHidden,
        Descending = right.SortDescending,
    },
    Active = right.HasFocus ? SessionState.RightPanel : SessionState.LeftPanel,
});
app.Dispose();

void SaveConfirm(ConfirmationSettings confirm)
{
    settings = settings with { Confirm = confirm };
    store.SaveSettings(settings);
}

void OpenSettings()
{
    string? error = opener.Open(store.SettingsPath);
    if (error is not null)
    {
        (right.HasFocus ? right : left).ShowError(error);
    }
}

void ChooseTheme()
{
    string? chosen = ThemePrompt.Ask(app, Themes.Current);
    if (chosen is null || chosen == Themes.Current)
    {
        return;
    }

    Themes.Apply(chosen);
    settings = settings with { Theme = Themes.Current };
    store.SaveSettings(settings);
}

void Restyle()
{
    window.SetScheme(Graphite.Scheme);
    menu.Restyle();
    bar.SetScheme(Graphite.Bar);
    left.Restyle();
    right.Restyle();
    window.SetNeedsDraw();
}
