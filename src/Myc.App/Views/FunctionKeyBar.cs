using Myc.App.Input;
using Myc.App.Theming;
using Myc.Core.Commands;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Myc.App.Views;

/// <summary>
/// The bottom F-key strip. Unimplemented keys stay visible and dimmed so the Norton
/// layout is there before the commands are.
/// </summary>
internal static class FunctionKeyBar
{
    public static StatusBar Create(Action<MycCommand> invoke)
    {
        Shortcut[] keys = CommandCatalog.Bar.Select(spec =>
        {
            Action? action = spec.Available ? () => invoke(spec.Command) : null;
            return Item(KeyMap.ToGui(spec.Keys[0]), spec.Label, action);
        }).ToArray();

        var bar = new FlexBar(keys)
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
            CanFocus = false,
        };
        bar.SetScheme(Graphite.Bar);
        return bar;
    }

    private static Shortcut Item(Key key, string title, Action? action)
    {
        return new Shortcut(key, title, action ?? (() => { }), helpText: "")
        {
            Enabled = action is not null,
            TabStop = TabBehavior.NoStop,
        };
    }

    /// <summary>
    /// StatusBar sizes each key to its label. This gives every key an equal share of the row,
    /// including the leftover columns, so the strip reaches both edges.
    /// </summary>
    private sealed class FlexBar : StatusBar
    {
        public FlexBar(IEnumerable<Shortcut> shortcuts)
            : base(shortcuts)
        {
        }

        protected override void OnSubViewLayout(LayoutEventArgs args)
        {
            base.OnSubViewLayout(args);
            int count = SubViews.Count;
            for (int index = 0; index < count; index++)
            {
                int slot = index;
                SubViews.ElementAt(index).Width = Dim.Func(_ => Share(slot, count));
            }
        }

        private int Share(int index, int count)
        {
            int width = Viewport.Width > 0 ? Viewport.Width : Frame.Width;
            if (count <= 0 || width <= 0)
            {
                return 0;
            }

            int share = width / count;
            return share + (index < width % count ? 1 : 0);
        }
    }
}
