using System.Diagnostics;
using System.Runtime.InteropServices;

namespace J.Core;

public static class ApplicationSubProcesses
{
    private static readonly IntPtr _hjob;

    static ApplicationSubProcesses()
    {
        var hjob = NativeMethods.CreateJobObject(IntPtr.Zero, IntPtr.Zero);
        if (hjob == IntPtr.Zero)
            throw new Exception("CreateJobObject() failed.");

        var jeli_buf = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION>());
        try
        {
            var jeli = new NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            jeli.BasicLimitInformation.LimitFlags = NativeMethods.JOBOBJECTLIMIT.KillOnJobClose;
            Marshal.StructureToPtr(jeli, jeli_buf, false);
            if (
                !NativeMethods.SetInformationJobObject(
                    hjob,
                    NativeMethods.JOBOBJECTINFOCLASS.ExtendedLimitInformation,
                    jeli_buf,
                    (uint)Marshal.SizeOf(jeli)
                )
            )
            {
                throw new Exception("SetInformationJobObject() failed.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(jeli_buf);
        }

        _hjob = hjob;
    }

    public static void Add(Process p)
    {
        // Ignore errors. There's nothing we can do.
        _ = NativeMethods.AssignProcessToJobObject(_hjob, p.Handle);
    }

    private static class NativeMethods
    {
        [Flags]
        public enum JOBOBJECTLIMIT : uint
        {
            // Basic Limits
            Workingset = 0x00000001,
            ProcessTime = 0x00000002,
            JobTime = 0x00000004,
            ActiveProcess = 0x00000008,
            Affinity = 0x00000010,
            PriorityClass = 0x00000020,
            PreserveJobTime = 0x00000040,
            SchedulingClass = 0x00000080,

            // Extended Limits
            ProcessMemory = 0x00000100,
            JobMemory = 0x00000200,
            DieOnUnhandledException = 0x00000400,
            BreakawayOk = 0x00000800,
            SilentBreakawayOk = 0x00001000,
            KillOnJobClose = 0x00002000,
            SubsetAffinity = 0x00004000,

            // Notification Limits
            JobReadBytes = 0x00010000,
            JobWriteBytes = 0x00020000,
            RateControl = 0x00040000,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public Int64 PerProcessUserTimeLimit;
            public Int64 PerJobUserTimeLimit;
            public JOBOBJECTLIMIT LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public UInt32 ActiveProcessLimit;
            public Int64 Affinity;
            public UInt32 PriorityClass;
            public UInt32 SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IO_COUNTERS
        {
            public UInt64 ReadOperationCount;
            public UInt64 WriteOperationCount;
            public UInt64 OtherOperationCount;
            public UInt64 ReadTransferCount;
            public UInt64 WriteTransferCount;
            public UInt64 OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        public enum JOBOBJECTINFOCLASS
        {
            AssociateCompletionPortInformation = 7,
            BasicLimitInformation = 2,
            BasicUIRestrictions = 4,
            EndOfJobTimeInformation = 6,
            ExtendedLimitInformation = 9,
            SecurityLimitInformation = 5,
            GroupInformation = 11,
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, IntPtr lpName);

        [DllImport("kernel32.dll")]
        public static extern bool SetInformationJobObject(
            IntPtr hJob,
            JOBOBJECTINFOCLASS JobObjectInfoClass,
            IntPtr lpJobObjectInfo,
            uint cbJobObjectInfoLength
        );

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);
    }
}
