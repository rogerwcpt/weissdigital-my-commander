using Myc.App.Theming;
using Myc.App.Views;
using Myc.Core.FileSystem;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

Application.SetDefaultKeyBinding(Command.Quit, Bind.All(Key.F10, Key.Q.WithCtrl));

using IApplication app = Application.Create();
app.Init();

var files = new LocalFileSystem();
var left = new FilePanelView(files)
{
    X = 0,
    Y = 0,
    Width = Dim.Percent(50),
    Height = Dim.Fill(1),
};
var right = new FilePanelView(files)
{
    X = Pos.Right(left),
    Y = 0,
    Width = Dim.Fill(),
    Height = Dim.Fill(1),
};

var window = new Window { Title = "myc" };
window.SetScheme(Graphite.Scheme);
window.Add(left, right, FunctionKeyBar.Create(app));

left.Open(Directory.GetCurrentDirectory());
right.Open(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
left.SetFocus();

app.Run(window);
app.Dispose();
