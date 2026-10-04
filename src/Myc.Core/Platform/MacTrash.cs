using System.Runtime.InteropServices;
using System.Text;

namespace Myc.Core.Platform;

/// <summary>
/// Finder Trash via <c>NSFileManager trashItemAtURL:</c>. Put Back keeps working because this is
/// the same call Finder uses, not a shell delete.
/// </summary>
public sealed class MacTrash : ITrash
{
    public TrashMove MoveToTrash(string path)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return new TrashMove("Trash is only set up on macOS.", null);
        }

        IntPtr pool = IntPtr.Zero;
        IntPtr cString = IntPtr.Zero;
        IntPtr resultSlot = IntPtr.Zero;
        IntPtr errorSlot = IntPtr.Zero;
        try
        {
            pool = ObjC.Send(ObjC.Send(ObjC.Class("NSAutoreleasePool"), ObjC.Sel("alloc")), ObjC.Sel("init"));
            cString = Utf8(path);
            IntPtr nsPath = ObjC.SendUtf8(ObjC.Class("NSString"), ObjC.Sel("stringWithUTF8String:"), cString);
            IntPtr url = ObjC.Send(ObjC.Class("NSURL"), ObjC.Sel("fileURLWithPath:"), nsPath);
            IntPtr manager = ObjC.Send(ObjC.Class("NSFileManager"), ObjC.Sel("defaultManager"));

            resultSlot = Marshal.AllocHGlobal(IntPtr.Size);
            errorSlot = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(resultSlot, IntPtr.Zero);
            Marshal.WriteIntPtr(errorSlot, IntPtr.Zero);

            byte ok = ObjC.SendTrash(
                manager,
                ObjC.Sel("trashItemAtURL:resultingItemURL:error:"),
                url,
                resultSlot,
                errorSlot);
            if (ok == 0)
            {
                return new TrashMove(ErrorText(Marshal.ReadIntPtr(errorSlot)), null);
            }

            return new TrashMove(null, UrlPath(Marshal.ReadIntPtr(resultSlot)));
        }
        catch (Exception exception) when (exception is IOException or DllNotFoundException or EntryPointNotFoundException)
        {
            return new TrashMove(exception.Message, null);
        }
        finally
        {
            if (cString != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(cString);
            }

            if (resultSlot != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(resultSlot);
            }

            if (errorSlot != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(errorSlot);
            }

            if (pool != IntPtr.Zero)
            {
                ObjC.Send(pool, ObjC.Sel("drain"));
            }
        }
    }

    private static string ErrorText(IntPtr error)
    {
        if (error == IntPtr.Zero)
        {
            return "Couldn't move it to Trash.";
        }

        IntPtr description = ObjC.Send(error, ObjC.Sel("localizedDescription"));
        return NsString(description) ?? "Couldn't move it to Trash.";
    }

    private static string? UrlPath(IntPtr url)
    {
        if (url == IntPtr.Zero)
        {
            return null;
        }

        return NsString(ObjC.Send(url, ObjC.Sel("path")));
    }

    private static string? NsString(IntPtr value)
    {
        if (value == IntPtr.Zero)
        {
            return null;
        }

        IntPtr utf8 = ObjC.Send(value, ObjC.Sel("UTF8String"));
        return utf8 == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(utf8);
    }

    private static IntPtr Utf8(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        IntPtr memory = Marshal.AllocHGlobal(bytes.Length + 1);
        Marshal.Copy(bytes, 0, memory, bytes.Length);
        Marshal.WriteByte(memory, bytes.Length, 0);
        return memory;
    }

    private static class ObjC
    {
        private const string Library = "/usr/lib/libobjc.dylib";

        [DllImport(Library, EntryPoint = "objc_getClass")]
        public static extern IntPtr Class(string name);

        [DllImport(Library, EntryPoint = "sel_registerName")]
        public static extern IntPtr Sel(string name);

        [DllImport(Library, EntryPoint = "objc_msgSend")]
        public static extern IntPtr Send(IntPtr receiver, IntPtr selector);

        [DllImport(Library, EntryPoint = "objc_msgSend")]
        public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr argument);

        [DllImport(Library, EntryPoint = "objc_msgSend")]
        public static extern IntPtr SendUtf8(IntPtr receiver, IntPtr selector, IntPtr utf8);

        [DllImport(Library, EntryPoint = "objc_msgSend")]
        public static extern byte SendTrash(IntPtr receiver, IntPtr selector, IntPtr url, IntPtr resultingUrl, IntPtr error);
    }
}
