using System.Runtime.InteropServices;

namespace FfxivReader
{
    // 通用 P/Invoke：外部讀取只需 OpenProcess + ReadProcessMemory + CloseHandle。
    // 這個檔案跟目標資料無關，任何要讀別的處理程序的專案都能原樣複用。
    public static partial class WinApi
    {
        // OpenProcess 存取旗標：0x0010 = PROCESS_VM_READ。
        [LibraryImport("kernel32.dll", SetLastError = true)]
        public static partial IntPtr OpenProcess(uint processAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint processId);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, IntPtr dwSize, IntPtr lpNumberOfBytesRead);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CloseHandle(IntPtr hObject);
    }
}
