using Myc.Core.Commands;
using Terminal.Gui.Input;

namespace Myc.App.Input;

internal static class KeyMap
{
    public static KeyToken? Match(Key key)
    {
        if (key.IsAlt && !key.IsCtrl && !key.IsShift && Digit(key.NoAlt) is int altDigit)
        {
            return Alt(altDigit);
        }

        if (key == Key.F8.WithShift)
        {
            return KeyToken.ShiftF8;
        }

        if (key == Key.Q.WithCtrl)
        {
            return KeyToken.CtrlQ;
        }

        if (key == Key.R.WithCtrl)
        {
            return KeyToken.CtrlR;
        }

        if (key == Key.F3.WithCtrl)
        {
            return KeyToken.CtrlF3;
        }

        if (key == Key.F4.WithCtrl)
        {
            return KeyToken.CtrlF4;
        }

        if (key == Key.F5.WithCtrl)
        {
            return KeyToken.CtrlF5;
        }

        if (key == Key.F6.WithCtrl)
        {
            return KeyToken.CtrlF6;
        }

        if (key == new Key('.').WithAlt)
        {
            return KeyToken.AltPeriod;
        }

        if (key == Key.A.WithCtrl && !key.IsAlt && !key.IsShift)
        {
            return KeyToken.CtrlA;
        }

        if (key == Key.G.WithCtrl && !key.IsAlt && !key.IsShift)
        {
            return KeyToken.CtrlG;
        }

        if (key == Key.U.WithCtrl && !key.IsAlt && !key.IsShift)
        {
            return KeyToken.CtrlU;
        }

        if (key == Key.O.WithCtrl && !key.IsAlt && !key.IsShift)
        {
            return KeyToken.CtrlO;
        }

        if (key == new Key('=').WithAlt)
        {
            return KeyToken.AltEquals;
        }

        if (!key.IsCtrl && !key.IsAlt)
        {
            int rune = key.AsRune.Value;
            if (rune == '+')
            {
                return KeyToken.Plus;
            }

            if (rune == '-')
            {
                return KeyToken.Minus;
            }

            if (rune == '*')
            {
                return KeyToken.Star;
            }
        }

        if (key.IsCtrl || key.IsAlt || key.IsShift)
        {
            return null;
        }

        if (Digit(key) is int digit)
        {
            return Number(digit);
        }

        return key == Key.F1 ? KeyToken.F1
            : key == Key.F2 ? KeyToken.F2
            : key == Key.F3 ? KeyToken.F3
            : key == Key.F4 ? KeyToken.F4
            : key == Key.F5 ? KeyToken.F5
            : key == Key.F6 ? KeyToken.F6
            : key == Key.F7 ? KeyToken.F7
            : key == Key.F8 ? KeyToken.F8
            : key == Key.F9 ? KeyToken.F9
            : key == Key.F10 ? KeyToken.F10
            : key == Key.Esc ? KeyToken.Esc
            : null;
    }

    public static Key ToGui(KeyToken key) => key switch
    {
        KeyToken.F1 => Key.F1,
        KeyToken.F2 => Key.F2,
        KeyToken.F3 => Key.F3,
        KeyToken.F4 => Key.F4,
        KeyToken.F5 => Key.F5,
        KeyToken.F6 => Key.F6,
        KeyToken.F7 => Key.F7,
        KeyToken.F8 => Key.F8,
        KeyToken.F9 => Key.F9,
        KeyToken.F10 => Key.F10,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Only function keys are drawn on the bar."),
    };

    private static int? Digit(Key key) =>
        key == Key.D0 ? 0
        : key == Key.D1 ? 1
        : key == Key.D2 ? 2
        : key == Key.D3 ? 3
        : key == Key.D4 ? 4
        : key == Key.D5 ? 5
        : key == Key.D6 ? 6
        : key == Key.D7 ? 7
        : key == Key.D8 ? 8
        : key == Key.D9 ? 9
        : null;

    private static KeyToken Number(int digit) => digit switch
    {
        0 => KeyToken.D0,
        1 => KeyToken.D1,
        2 => KeyToken.D2,
        3 => KeyToken.D3,
        4 => KeyToken.D4,
        5 => KeyToken.D5,
        6 => KeyToken.D6,
        7 => KeyToken.D7,
        8 => KeyToken.D8,
        9 => KeyToken.D9,
        _ => throw new ArgumentOutOfRangeException(nameof(digit)),
    };

    private static KeyToken Alt(int digit) => digit switch
    {
        0 => KeyToken.Alt0,
        1 => KeyToken.Alt1,
        2 => KeyToken.Alt2,
        3 => KeyToken.Alt3,
        4 => KeyToken.Alt4,
        5 => KeyToken.Alt5,
        6 => KeyToken.Alt6,
        7 => KeyToken.Alt7,
        8 => KeyToken.Alt8,
        9 => KeyToken.Alt9,
        _ => throw new ArgumentOutOfRangeException(nameof(digit)),
    };
}
