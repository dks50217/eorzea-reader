using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FfxivReader
{
    // 可複用骨架：開啟處理程序、掃 .text 段、解 RIP 相對位址、跟隨指標鏈。
    // 讀任何 FFXIV 資料時繼承它，只需提供各自的特徵碼與結構解析。
    public class MemScanner
    {
        protected readonly IntPtr hProcess;
        protected readonly IntPtr textAddress;
        protected readonly byte[] textSection;

        public MemScanner(Process gameProcess)
        {
            hProcess = WinApi.OpenProcess(0x00000010 /*VM_READ*/, false, (uint)gameProcess.Id);
            if (hProcess == IntPtr.Zero) throw new InvalidOperationException("存取遊戲處理程序失敗（需要系統管理員權限）");
            var pBase = gameProcess.MainModule!.BaseAddress;

            // 從 PE header 找 .text 段的位址與大小。
            var header = new byte[0x800];
            WinApi.ReadProcessMemory(hProcess, pBase, header, header.Length, IntPtr.Zero);
            var header64 = MemoryMarshal.Cast<byte, ulong>(header);
            uint textSize = 0;
            textAddress = pBase;
            for (var i = 0; i < header64.Length; i++)
            {
                if (header64[i] == 0x747865742E/*.text*/)
                {
                    textAddress += (int)(header64[i + 1] >> 32);
                    textSize = (uint)(header64[i + 1] & 0xffffffffL);
                    break;
                }
            }

            // 把整段 .text 讀進來，之後在本地 byte[] 上掃特徵碼。
            textSection = new byte[textSize];
            WinApi.ReadProcessMemory(hProcess, textAddress, textSection, textSection.Length, IntPtr.Zero);
        }

        // 解 sig 中第一個 null 位置的 4-byte RIP 相對位移，回傳目標絕對位址（取第一個命中）。
        // 只有在特徵碼夠唯一時才用；短特徵碼請改用 ResolveRipAll + 驗證。
        protected IntPtr ResolveRip(byte?[] sig)
        {
            var all = ResolveRipAll(sig);
            if (all.Count == 0) throw new NotSupportedException("特徵碼定位失敗（去 FFXIVClientStructs 抓新的）");
            return all[0];
        }

        // 回傳「全部」符合位置解出的目標。短特徵碼在 .text 常命中多處，
        // 呼叫端要用「解出的 instance 結構長得對不對」來挑正確那個。
        protected List<IntPtr> ResolveRipAll(byte?[] sig)
        {
            var dispIndex = Array.IndexOf(sig, null); // 對應 CS [StaticAddress(sig, dispIndex)] 的第二個參數
            var targets = new List<IntPtr>();
            for (var i = 0; i < textSection.Length - sig.Length; i++)
            {
                for (var j = 0; j < sig.Length; j++)
                    if (sig[j] != null && textSection[i + j] != sig[j]) goto Next;
                var disp = BitConverter.ToInt32(textSection, i + dispIndex);
                targets.Add(textAddress + i + dispIndex + 4 + disp); // 下一指令位址 + 位移
            Next:;
            }
            return targets;
        }

        public byte[] Read(IntPtr addr, int size)
        {
            var buf = new byte[size];
            WinApi.ReadProcessMemory(hProcess, addr, buf, size, IntPtr.Zero);
            return buf;
        }

        public T Read<T>(IntPtr addr) where T : unmanaged =>
            MemoryMarshal.Read<T>(Read(addr, Marshal.SizeOf<T>()));

        // 跟隨指標鏈：從 start 讀指標，再逐層 +offset。對應 CS 裡一連串的 T* 欄位。
        public IntPtr Follow(IntPtr start, params int[] offsets)
        {
            var addr = start;
            for (var i = 0; i < offsets.Length; i++)
                addr = (IntPtr)Read<ulong>(addr) + offsets[i];
            return addr;
        }
    }
}
