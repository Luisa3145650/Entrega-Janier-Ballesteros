using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace loginavicola.Helpers
{
    /// <summary>
    /// Gestiona un Job Object de Windows para vincular la vida del proceso servidor Python (servidor_api.py)
    /// al proceso principal C#. Si la aplicación C# se cierra abruptamente (ej. Shift+F5 en Visual Studio o crash),
    /// el Kernel de Windows mata automáticamente todos los procesos dentro del Job Object.
    /// </summary>
    public static class ChildProcessTracker
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryLimit;
            public UIntPtr PeakJobMemoryLimit;
        }

        private enum JOBOBJECTINFOCLASS
        {
            JobObjectExtendedLimitInformation = 9
        }

        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(
            IntPtr hJob,
            JOBOBJECTINFOCLASS JobObjectInfoClass,
            IntPtr lpJobObjectInfo,
            uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private static IntPtr s_jobHandle = IntPtr.Zero;
        private static readonly object s_lock = new object();

        static ChildProcessTracker()
        {
            Inicializar();
        }

        private static void Inicializar()
        {
            lock (s_lock)
            {
                if (s_jobHandle != IntPtr.Zero) return;

                // Crear Job Object anónimo
                s_jobHandle = CreateJobObject(IntPtr.Zero, null);
                if (s_jobHandle == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"⚠️ [JobObject] CreateJobObject falló con error Win32 {err}");
                    return;
                }

                // Configurar JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE (0x2000)
                var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                    {
                        LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                    }
                };

                int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                IntPtr infoPtr = Marshal.AllocHGlobal(length);

                try
                {
                    Marshal.StructureToPtr(extendedInfo, infoPtr, false);
                    if (!SetInformationJobObject(s_jobHandle, JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation, infoPtr, (uint)length))
                    {
                        int err = Marshal.GetLastWin32Error();
                        Debug.WriteLine($"⚠️ [JobObject] SetInformationJobObject falló con error Win32 {err}");
                        CloseHandle(s_jobHandle);
                        s_jobHandle = IntPtr.Zero;
                        return;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(infoPtr);
                }

                Debug.WriteLine("✅ [JobObject] Creado e inicializado correctamente con KILL_ON_JOB_CLOSE.");
            }
        }

        /// <summary>
        /// Agrega un proceso secundario al Job Object.
        /// </summary>
        public static bool AddProcess(Process process)
        {
            if (process == null || process.HasExited) return false;

            lock (s_lock)
            {
                if (s_jobHandle == IntPtr.Zero)
                {
                    Inicializar();
                    if (s_jobHandle == IntPtr.Zero) return false;
                }

                try
                {
                    bool result = AssignProcessToJobObject(s_jobHandle, process.Handle);
                    if (!result)
                    {
                        int err = Marshal.GetLastWin32Error();
                        Debug.WriteLine($"⚠️ [JobObject] AssignProcessToJobObject para PID {process.Id} falló con error Win32 {err}");
                        return false;
                    }

                    Debug.WriteLine($"✅ [JobObject] Proceso PID {process.Id} asignado exitosamente al Job Object.");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"⚠️ [JobObject] Excepción al asignar proceso: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Cierra explícitamente el handle del Job Object durante el apagado normal ordenado.
        /// </summary>
        public static void Close()
        {
            lock (s_lock)
            {
                if (s_jobHandle != IntPtr.Zero)
                {
                    CloseHandle(s_jobHandle);
                    s_jobHandle = IntPtr.Zero;
                    Debug.WriteLine("✅ [JobObject] Handle liberado y cerrado correctamente.");
                }
            }
        }
    }
}
