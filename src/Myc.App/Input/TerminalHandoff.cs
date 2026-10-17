using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Myc.Core.Platform;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;

namespace Myc.App.Input;

/// <summary>
/// Runs a terminal editor or a shell on the real tty and then puts myc back.
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
    private const int SigKill = 9;
    private const int SigContMac = 19;
    private const short PosixSpawnSetPgroup = 0x0002;

    /// <summary>macOS only. On Linux this bit is <c>POSIX_SPAWN_SETSID</c>, which would drop the controlling terminal.</summary>
    private const short PosixSpawnStartSuspended = 0x0080;

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

    /// <summary>
    /// Hands the real terminal to an interactive shell in <paramref name="directory"/>.
    /// The shell is the foreground process group, so Ctrl+C and Ctrl+Z reach it. <c>exit</c> returns here.
    /// </summary>
    public static string? RunShell(IApplication app, string shell, string directory)
    {
        if (OperatingSystem.IsWindows() || !_haveCooked || _tty < 0)
        {
            return "This shell needs a terminal.";
        }

        return RunShellUnix(app, shell, directory);
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

    private static string? RunShellUnix(IApplication app, string shell, string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            return "This shell needs a terminal.";
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
        int child = -1;
        bool resumed = false;
        int foreground = -1;
        uint[]? savedSignals = null;
        try
        {
            if (!parked.Ready)
            {
                error = "Could not hand the terminal to the shell.";
            }
            else
            {
                LeaveScreen(app);
                left = true;
                if (tcsetattr(_tty, TCSANOW, _cooked) != 0)
                {
                    RunSttySane();
                }

                child = SpawnShell(shell, directory, out error);
                if (child > 0)
                {
                    // Block SIGTTOU. Ignoring it (a handled PosixSignal) makes macOS
                    // refuse tcsetpgrp with EIO, so the shell's cooked mode stays on
                    // and keys come back as echoed text.
                    savedSignals = BlockTerminalStop();
                    foreground = getpgrp();
                    // Take the tty before the shell reads it. On macOS the child is stopped until then,
                    // so a prompt cannot lose the race and get SIGTTIN.
                    if (savedSignals is null || foreground <= 0 || tcsetpgrp(_tty, child) != 0)
                    {
                        error = "Could not hand the terminal to the shell.";
                    }
                    else if (OperatingSystem.IsMacOS() && kill(child, SigContMac) != 0)
                    {
                        error = "Could not hand the terminal to the shell.";
                    }
                    else
                    {
                        resumed = true;
                        WaitPid(child);
                    }
                }
            }
        }
        finally
        {
            if (child > 0 && !resumed)
            {
                kill(child, SigKill);
                WaitPid(child);
            }

            if (foreground > 0)
            {
                tcsetpgrp(_tty, foreground);
            }

            tcsetattr(_tty, TCSANOW, raw);
            if (savedSignals is not null)
            {
                RestoreSignalMask(savedSignals);
            }

            Marshal.FreeHGlobal(raw);
            if (left)
            {
                ReturnScreen(app);
            }
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

    private static int SpawnShell(string file, string directory, out string? error)
    {
        error = null;
        string[] argv = [file];
        nint argvBlock = AllocStrings(argv, out nint[] argStrings);
        nint envBlock = AllocEnvironment(out nint[] envStrings);
        try
        {
            int pid;
            int rc = OperatingSystem.IsMacOS()
                ? SpawnShellMac(file, directory, argvBlock, envBlock, out pid)
                : SpawnShellLinux(file, directory, argvBlock, envBlock, out pid);
            if (rc != 0)
            {
                error = "Could not run " + file + ".";
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

    private static int SpawnShellMac(string file, string directory, nint argv, nint envp, out int pid)
    {
        nint actions = 0;
        nint attr = 0;
        pid = 0;
        bool actionsReady = false;
        bool attrReady = false;
        try
        {
            if (posix_spawn_file_actions_init(ref actions) != 0)
            {
                return 1;
            }

            actionsReady = true;
            if (posix_spawn_file_actions_adddup2(ref actions, _tty, 0) != 0
                || posix_spawn_file_actions_adddup2(ref actions, _tty, 1) != 0
                || posix_spawn_file_actions_adddup2(ref actions, _tty, 2) != 0
                || posix_spawn_file_actions_addchdir_np(ref actions, directory) != 0)
            {
                return 1;
            }

            if (_tty > 2)
            {
                posix_spawn_file_actions_addclose(ref actions, _tty);
            }

            if (posix_spawnattr_init(ref attr) != 0)
            {
                return 1;
            }

            attrReady = true;
            short flags = (short)(PosixSpawnSetPgroup | PosixSpawnStartSuspended);
            if (posix_spawnattr_setflags(ref attr, flags) != 0
                || posix_spawnattr_setpgroup(ref attr, 0) != 0)
            {
                return 1;
            }

            return posix_spawnp_attr(out pid, file, ref actions, ref attr, argv, envp);
        }
        finally
        {
            if (actionsReady)
            {
                posix_spawn_file_actions_destroy(ref actions);
            }

            if (attrReady)
            {
                posix_spawnattr_destroy(ref attr);
            }
        }
    }

    private static int SpawnShellLinux(string file, string directory, nint argv, nint envp, out int pid)
    {
        nint actions = Marshal.AllocHGlobal(OpaqueSize);
        nint attr = Marshal.AllocHGlobal(OpaqueSize);
        pid = 0;
        bool actionsReady = false;
        bool attrReady = false;
        try
        {
            Zero(actions);
            Zero(attr);
            if (posix_spawn_file_actions_init_linux(actions) != 0)
            {
                return 1;
            }

            actionsReady = true;
            if (posix_spawn_file_actions_adddup2_linux(actions, _tty, 0) != 0
                || posix_spawn_file_actions_adddup2_linux(actions, _tty, 1) != 0
                || posix_spawn_file_actions_adddup2_linux(actions, _tty, 2) != 0
                || posix_spawn_file_actions_addchdir_linux(actions, directory) != 0)
            {
                return 1;
            }

            if (_tty > 2)
            {
                posix_spawn_file_actions_addclose_linux(actions, _tty);
            }

            if (posix_spawnattr_init_linux(attr) != 0)
            {
                return 1;
            }

            attrReady = true;
            if (posix_spawnattr_setflags_linux(attr, PosixSpawnSetPgroup) != 0
                || posix_spawnattr_setpgroup_linux(attr, 0) != 0)
            {
                return 1;
            }

            return posix_spawnp_attr_linux(out pid, file, actions, attr, argv, envp);
        }
        finally
        {
            if (actionsReady)
            {
                posix_spawn_file_actions_destroy_linux(actions);
            }

            if (attrReady)
            {
                posix_spawnattr_destroy_linux(attr);
            }

            Marshal.FreeHGlobal(actions);
            Marshal.FreeHGlobal(attr);
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

    /// <summary>
    /// SIGTTOU is 22 on macOS and Linux. A blocked signal lets a background process
    /// take the terminal back; an ignored one does not.
    /// </summary>
    private const int Sigttou = 22;

    private static uint[]? BlockTerminalStop()
    {
        var block = new uint[32];
        block[(Sigttou - 1) / 32] |= 1u << ((Sigttou - 1) % 32);
        var previous = new uint[32];
        return sigprocmask(SignalBlock, block, previous) == 0 ? previous : null;
    }

    private static void RestoreSignalMask(uint[] previous) => sigprocmask(SignalSetMask, previous, null);

    private static int SignalBlock => OperatingSystem.IsLinux() ? 0 : 1;

    private static int SignalSetMask => OperatingSystem.IsLinux() ? 2 : 3;

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
    private static extern int kill(int pid, int sig);

    [DllImport("libc")]
    private static extern int getpgrp();

    [DllImport("libc", SetLastError = true)]
    private static extern int sigprocmask(int how, uint[] set, uint[]? old);

    [DllImport("libc", SetLastError = true)]
    private static extern int tcsetpgrp(int fd, int pgrp);

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

    [DllImport("libc", EntryPoint = "posix_spawnp", SetLastError = true)]
    private static extern int posix_spawnp_attr(out int pid, string file, ref nint actions, ref nint attr, nint argv, nint envp);

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_spawnattr_init(ref nint attr);

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_spawnattr_destroy(ref nint attr);

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_spawnattr_setflags(ref nint attr, short flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_spawnattr_setpgroup(ref nint attr, int pgroup);

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_spawn_file_actions_addchdir_np(ref nint actions, string path);

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

    [DllImport("libc", EntryPoint = "posix_spawnp", SetLastError = true)]
    private static extern int posix_spawnp_attr_linux(out int pid, string file, nint actions, nint attr, nint argv, nint envp);

    [DllImport("libc", EntryPoint = "posix_spawnattr_init", SetLastError = true)]
    private static extern int posix_spawnattr_init_linux(nint attr);

    [DllImport("libc", EntryPoint = "posix_spawnattr_destroy", SetLastError = true)]
    private static extern int posix_spawnattr_destroy_linux(nint attr);

    [DllImport("libc", EntryPoint = "posix_spawnattr_setflags", SetLastError = true)]
    private static extern int posix_spawnattr_setflags_linux(nint attr, short flags);

    [DllImport("libc", EntryPoint = "posix_spawnattr_setpgroup", SetLastError = true)]
    private static extern int posix_spawnattr_setpgroup_linux(nint attr, int pgroup);

    [DllImport("libc", EntryPoint = "posix_spawn_file_actions_addchdir", SetLastError = true)]
    private static extern int posix_spawn_file_actions_addchdir_linux(nint actions, string path);
}
