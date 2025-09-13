using System.Diagnostics;
using System.Runtime.InteropServices;

public class Driver
{
    [DllImport("Kernel32.dll")]
    private static extern bool ReadProcessMemory(nint hProcess, nint lpBaseAddress, [Out] nint lpBuffer, int nSize, nint lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern unsafe bool WriteProcessMemory(nint hProcess, nint lpBaseAddress, void* lpBuffer, int nSize, nint lpNumberOfBytesWritten);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, nuint dwExtraInfo);

    private Process proc;

    public Driver(string procName)
    {
        proc = Process.GetProcessesByName(procName)[0];
    }

    public nint GetModuleBase(string moduleName)
    {
        if (moduleName.Contains(".exe") && proc.MainModule != null)
            return proc.MainModule.BaseAddress;

        foreach (ProcessModule module in proc.Modules)
            if (module.ModuleName == moduleName)
                return module.BaseAddress;

        return nint.Zero;
    }

    public unsafe T ReadMemory<T>(nint address) where T : unmanaged
    {
        T value = default;
        ReadProcessMemory(proc.Handle, address, (nint)(&value), sizeof(T), nint.Zero);
        return value;
    }

    public unsafe void WriteMemory<T>(nint address, T value) where T : unmanaged
    {
        WriteProcessMemory(proc.Handle, address, (void*)(&value), sizeof(T), nint.Zero);
    }
}