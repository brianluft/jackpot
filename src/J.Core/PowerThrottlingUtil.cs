using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace J.Core;

public static partial class PowerThrottlingUtil
{
    public static bool DisablePowerThrottling(Process process)
    {
        try
        {
            return DisablePowerThrottlingCore(process);
        }
        catch (Exception ex)
        {
            throw new Exception(
                "Failed to configure the CPU power throttling mode. The internal error was: " + ex.Message,
                ex
            );
        }
    }

    private static bool DisablePowerThrottlingCore(Process process)
    {
        IntPtr hProcess = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_SET_INFORMATION | NativeMethods.PROCESS_QUERY_INFORMATION,
            false,
            process.Id
        );

        if (hProcess == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            // From the example at:
            // https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-setprocessinformation
            //    "HighQoS
            //    Turn EXECUTION_SPEED throttling off.
            //    ControlMask selects the mechanism and StateMask is set to zero as mechanisms should be turned off."
            NativeMethods.PROCESS_POWER_THROTTLING_STATE powerThrottling =
                new()
                {
                    Version = NativeMethods.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                    ControlMask = NativeMethods.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                    StateMask = 0,
                };

            var success = NativeMethods.SetProcessInformation(
                hProcess,
                (int)NativeMethods.PROCESS_INFORMATION_CLASS.ProcessPowerThrottling,
                ref powerThrottling,
                Marshal.SizeOf<NativeMethods.PROCESS_POWER_THROTTLING_STATE>()
            );

            if (!success)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(hProcess);
        }
    }

    private static partial class NativeMethods
    {
        public const int PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
        public const int PROCESS_SET_INFORMATION = 0x0200;
        public const int PROCESS_QUERY_INFORMATION = 0x0400;
        public const int PROCESS_CREATION_MITIGATION_POLICY_INFORMATION = 9;
        public const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_POWER_THROTTLING_STATE
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetProcessInformation(
            IntPtr hProcess,
            int ProcessInformationClass,
            ref PROCESS_POWER_THROTTLING_STATE ProcessInformation,
            int ProcessInformationSize
        );

        [LibraryImport("kernel32.dll", SetLastError = true)]
        public static partial IntPtr OpenProcess(
            int dwDesiredAccess,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
            int dwProcessId
        );

        public enum PROCESS_INFORMATION_CLASS
        {
            ProcessMemoryPriority,
            ProcessMemoryExhaustionInfo,
            ProcessAppMemoryInfo,
            ProcessInPrivateInfo,
            ProcessPowerThrottling,
        }
    }
}
