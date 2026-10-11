using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Myc.Core.Platform;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;

namespace Myc.App.Input;

/// <summary>
/// Runs a terminal editor on the real tty and then puts myc back.
/// <see cref="IDriver.Suspend"/> is Ctrl+Z (SIGTSTP), so it is not used here.
/// </summary>
internal static class TerminalHandoff
{
    private const int TermiosSize = 128;
    private const int OpaqueSize = 1024;
    private const int O_RDWR = 2;
    private const int TCSANOW = 0;
    private const int F_SETFD = 2;
    private const int FD_CLOEXEC = 1;
    private const int Eintr = 4;

    private static int _tty = -1;
    private static nint _cooked;
    private static bool _haveCooked;

    /// <summary>Saves the cooked terminal mode. Call this before <c>app.Init()</c>.</summary>
    public static void Capture()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        _tty = open("/dev/tty", O_RDWR);
        if (_tty < 0)
        {
            return;
        }

        fcntl(_tty, F_SETFD, FD_CLOEXEC);
        _cooked = Marshal.AllocHGlobal(TermiosSize);
        if (tcgetattr(_tty, _cooked) == 0)
        {
            _haveCooked = true;
            return;
        }

        close(_tty);
        _tty = -1;
        Marshal.FreeHGlobal(_cooked);
        _cooked = 0;
    }

    public static string? RunEditor(IApplication app, ExternalEdit.Spawn spawn)
    {
        if (OperatingSystem.IsWindows())
        {
            return Wait(spawn);
        }

        if (!_haveCooked || _tty < 0)
        {
            return "This editor needs a terminal.";
        }

        return RunUnix(app, spawn);
    }

    private static string? RunUnix(IApplication app, ExternalEdit.Spawn spawn)
    {
        if (OperatingSystem.IsWindows())
        {
            return "This editor needs a terminal.";
        }

        nint raw = Marshal.AllocHGlobal(TermiosSize);
        if (tcgetattr(_tty, raw) != 0)
        {
            Marshal.FreeHGlobal(raw);
            return "Could not prepare the terminal.";
        }

        using var parked = new StdinPark();
        string? error = null;
        bool left = false;
        PosixSignalRegistration? ignoreInterrupt = null;
        try
        {
            if (!parked.Ready)
            {
                error = "Could not hand the terminal to the editor.";
            }
            else
            {
                LeaveScreen(app);
                left = true;
                if (tcsetattr(_tty, TCSANOW, _cooked) != 0)
                {
                    RunSttySane();
                }

                int child = Spawn(spawn, out error);
                if (child > 0)
                {
                    // Same process group as myc, so the editor can read the terminal at once.
                    // Ctrl+C is delivered to both; myc ignores it and the editor handles it.
                    try
                    {
                        ignoreInterrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, static context => context.Cancel = true);
                    }
                    catch (PlatformNotSupportedException)
                    {
                    }

                    WaitPid(child);
                }
            }
        }
        finally
        {
            tcsetattr(_tty, TCSANOW, raw);
            Marshal.FreeHGlobal(raw);
            if (left)
            {
                ReturnScreen(app);
            }

            ignoreInterrupt?.Dispose();
        }

        return error;
    }

    private static void LeaveScreen(IApplication app)
    {
        try
        {
            app.Driver?.WriteRaw(
                EscSeqUtils.CSI_DisableBracketedPaste
                + EscSeqUtils.CSI_DisableMouseEvents
                + EscSeqUtils.CSI_ResetAttributes
                + EscSeqUtils.CSI_RestoreCursorAndRestoreAltBufferWithBackscroll
                + EscSeqUtils.CSI_ShowCursor);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static void ReturnScreen(IApplication app)
    {
        try
        {
            app.Driver?.WriteRaw(
                EscSeqUtils.CSI_SaveCursorAndActivateAltBufferNoBackscroll
                + EscSeqUtils.CSI_HideCursor
                + EscSeqUtils.CSI_EnableMouseEvents
                + EscSeqUtils.CSI_EnableBracketedPaste);
            app.ClearScreenNextIteration = true;
            app.TopRunnableView?.SetNeedsDraw();
            app.LayoutAndDraw(true);
        }
        catch (Exception exception) when (exception is ObjectDisposedException or InvalidOperationException)
        {
        }
    }

    private static string? Wait(ExternalEdit.Spawn spawn)
    {
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = spawn.FileName,
                UseShellExecute = false,
            };
            foreach (string argument in spawn.Arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using Process? process = Process.Start(start);
            if (process is null)
            {
                return "Could not run " + spawn.FileName + ".";
            }

            process.WaitForExit();
            return null;
        }
        catch (Exception exception) when (exception is Win32Exception or IOException)
        {
            return exception.Message;
        }
    }

    private static int Spawn(ExternalEdit.Spawn spawn, out string? error)
    {
        error = null;
        string[] argv = [spawn.FileName, .. spawn.Arguments];
        nint argvBlock = AllocStrings(argv, out nint[] argStrings);
        // A null envp is an empty environment on macOS, so the editor would start without TERM.
        nint envBlock = AllocEnvironment(out nint[] envStrings);
        try
        {
            int pid;
            int rc = OperatingSystem.IsMacOS()
                ? SpawnMac(spawn.FileName, argvBlock, envBlock, out pid)
                : SpawnLinux(spawn.FileName, argvBlock, envBlock, out pid);
            if (rc != 0)
            {
                error = "Could not run " + spawn.FileName + ".";
                return -1;
            }

            return pid;
        }
        finally
        {
            Free(argStrings);
            Free(envStrings);
            Marshal.FreeHGlobal(argvBlock);
            Marshal.FreeHGlobal(envBlock);
        }
    }

    private static int SpawnMac(string file, nint argv, nint envp, out int pid)
    {
        nint actions = 0;
        pid = 0;
        bool ready = false;
        try
        {
            if (posix_spawn_file_actions_init(ref actions) != 0)
            {
                return 1;
            }

            ready = true;
            if (posix_spawn_file_actions_adddup2(ref actions, _tty, 0) != 0
                || posix_spawn_file_actions_adddup2(ref actions, _tty, 1) != 0
                || posix_spawn_file_actions_adddup2(ref actions, _tty, 2) != 0)
            {
                return 1;
            }

            if (_tty > 2)
            {
                posix_spawn_file_actions_addclose(ref actions, _tty);
            }

            return posix_spawnp(out pid, file, ref actions, 0, argv, envp);
        }
        finally
        {
            if (ready)
            {
                posix_spawn_file_actions_destroy(ref actions);
            }
        }
    }

    private static int SpawnLinux(string file, nint argv, nint envp, out int pid)
    {
        nint actions = Marshal.AllocHGlobal(OpaqueSize);
        pid = 0;
        bool ready = false;
        try
        {
            Zero(actions);
            if (posix_spawn_file_actions_init_linux(actions) != 0)
            {
                return 1;
            }

            ready = true;
            if (posix_spawn_file_actions_adddup2_linux(actions, _tty, 0) != 0
                || posix_spawn_file_actions_adddup2_linux(actions, _tty, 1) != 0
                || posix_spawn_file_actions_adddup2_linux(actions, _tty, 2) != 0)
            {
                return 1;
            }

            if (_tty > 2)
            {
                posix_spawn_file_actions_addclose_linux(actions, _tty);
            }

            return posix_spawnp_linux(out pid, file, actions, 0, argv, envp);
        }
        finally
        {
            if (ready)
            {
                posix_spawn_file_actions_destroy_linux(actions);
            }

            Marshal.FreeHGlobal(actions);
        }
    }

    private static void WaitPid(int pid)
    {
        while (waitpid(pid, out _, 0) < 0)
        {
            if (Marshal.GetLastWin32Error() != Eintr)
            {
                return;
            }
        }
    }

    private static void RunSttySane()
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo("/bin/sh", "-c \"stty sane < /dev/tty\"")
            {
                UseShellExecute = false,
            });
            process?.WaitForExit();
        }
        catch (Exception exception) when (exception is Win32Exception or IOException)
        {
        }
    }

    private static nint AllocEnvironment(out nint[] strings)
    {
        System.Collections.IDictionary variables = Environment.GetEnvironmentVariables();
        var pairs = new string[variables.Count];
        int index = 0;
        foreach (System.Collections.DictionaryEntry entry in variables)
        {
            pairs[index++] = entry.Key + "=" + entry.Value;
        }

        return AllocStrings(pairs, out strings);
    }

    private static nint AllocStrings(string[] args, out nint[] strings)
    {
        strings = new nint[args.Length];
        nint block = Marshal.AllocHGlobal(nint.Size * (args.Length + 1));
        for (int i = 0; i < args.Length; i++)
        {
            strings[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
            Marshal.WriteIntPtr(block, i * nint.Size, strings[i]);
        }

        Marshal.WriteIntPtr(block, args.Length * nint.Size, 0);
        return block;
    }

    private static void Free(nint[] pointers)
    {
        foreach (nint pointer in pointers)
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    private static void Zero(nint pointer)
    {
        Marshal.Copy(new byte[OpaqueSize], 0, pointer, OpaqueSize);
    }

    /// <summary>
    /// The input thread polls stdin. A pipe on fd 0 keeps those polls off the tty while the editor runs.
    /// </summary>
    private sealed class StdinPark : IDisposable
    {
        private int _saved = -1;
        private int _write = -1;

        public bool Ready => _saved >= 0;

        public StdinPark()
        {
            int[] ends = new int[2];
            if (pipe(ends) != 0)
            {
                return;
            }

            _saved = dup(0);
            if (_saved < 0 || dup2(ends[0], 0) < 0)
            {
                close(ends[0]);
                close(ends[1]);
                if (_saved >= 0)
                {
                    close(_saved);
                    _saved = -1;
                }

                return;
            }

            close(ends[0]);
            _write = ends[1];
            fcntl(_write, F_SETFD, FD_CLOEXEC);
            fcntl(_saved, F_SETFD, FD_CLOEXEC);
        }

        public void Dispose()
        {
            if (_saved >= 0)
            {
                dup2(_saved, 0);
                close(_saved);
                _saved = -1;
            }

            if (_write >= 0)
            {
                close(_write);
                _write = -1;
            }
        }
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int open(string path, int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int dup(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int dup2(int oldfd, int newfd);

    [DllImport("libc", SetLastError = true)]
    private static extern int pipe([Out] int[] pipefd);

    [DllImport("libc", SetLastError = true)]
    private static extern int fcntl(int fd, int cmd, int arg);

    [DllImport("libc", SetLastError = true)]
    private static extern int tcgetattr(int fd, nint termios);

    [DllImport("libc", SetLastError = true)]
    private static extern int tcsetattr(int fd, int actions, nint termios);

    [DllImport("libc", SetLastError = true)]
    private static extern int waitpid(int pid, out int status, int options);

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_spawn_file_actions_init(ref nint actions);

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_spawn_file_actions_destroy(ref nint actions);

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_spawn_file_actions_adddup2(ref nint actions, int fd, int newfd);

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_spawn_file_actions_addclose(ref nint actions, int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_spawnp(out int pid, string file, ref nint actions, nint attr, nint argv, nint envp);

    [DllImport("libc", EntryPoint = "posix_spawn_file_actions_init", SetLastError = true)]
    private static extern int posix_spawn_file_actions_init_linux(nint actions);

    [DllImport("libc", EntryPoint = "posix_spawn_file_actions_destroy", SetLastError = true)]
    private static extern int posix_spawn_file_actions_destroy_linux(nint actions);

    [DllImport("libc", EntryPoint = "posix_spawn_file_actions_adddup2", SetLastError = true)]
    private static extern int posix_spawn_file_actions_adddup2_linux(nint actions, int fd, int newfd);

    [DllImport("libc", EntryPoint = "posix_spawn_file_actions_addclose", SetLastError = true)]
    private static extern int posix_spawn_file_actions_addclose_linux(nint actions, int fd);

    [DllImport("libc", EntryPoint = "posix_spawnp", SetLastError = true)]
    private static extern int posix_spawnp_linux(out int pid, string file, nint actions, nint attr, nint argv, nint envp);
}
