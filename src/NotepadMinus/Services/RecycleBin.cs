using System;
using System.Runtime.InteropServices;

namespace NotepadMinus.Services;

/// <summary>
/// v1.5 sidebar Delete sends files to the Windows Recycle Bin (never permanent), with no
/// confirmation prompt — the Recycle Bin is the safety net. Implemented via SHFileOperation
/// so we don't pull in Microsoft.VisualBasic just for one call.
/// </summary>
public static class RecycleBin
{
    public static bool Send(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        // SHFileOperation needs the path double-null-terminated.
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = filePath + "\0\0",
            fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT | FOF_WANTNUKEWARNING),
        };
        return SHFileOperation(ref op) == 0 && !op.fAnyOperationsAborted;
    }

    // ----- P/Invoke -----

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_NOERRORUI = 0x0400;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_WANTNUKEWARNING = 0x4000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);
}
