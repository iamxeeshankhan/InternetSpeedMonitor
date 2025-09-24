using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace InternetSpeedMonitor
{
    public partial class App : Application
    {
        private static Mutex? _instanceMutex;
        private const string MutexName = "InternetSpeedMonitor_SingleInstance_Mutex";

        protected override void OnStartup(StartupEventArgs e)
        {
            // Try to create a named mutex to ensure only one instance runs
            bool createdNew;
            _instanceMutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                // Another instance is already running
                BringExistingInstanceToForeground();
                Environment.Exit(0);
                return;
            }

            base.OnStartup(e);
            MainWindow = new MainWindow();
            MainWindow.Show();
        }

        private static void BringExistingInstanceToForeground()
        {
            try
            {
                // Find the existing process
                var currentProcess = Process.GetCurrentProcess();
                var processes = Process.GetProcessesByName(currentProcess.ProcessName);
                
                foreach (var process in processes)
                {
                    if (process.Id != currentProcess.Id)
                    {
                        // Try to bring the window to foreground using multiple methods
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            // Method 1: Use ShowWindow and SetForegroundWindow
                            ShowWindow(process.MainWindowHandle, SW_RESTORE);
                            SetForegroundWindow(process.MainWindowHandle);
                            
                            // Method 2: Use AttachThreadInput for better reliability
                            var currentThreadId = GetCurrentThreadId();
                            var targetThreadId = GetWindowThreadProcessId(process.MainWindowHandle, out _);
                            
                            if (currentThreadId != targetThreadId)
                            {
                                AttachThreadInput(currentThreadId, targetThreadId, true);
                                SetForegroundWindow(process.MainWindowHandle);
                                AttachThreadInput(currentThreadId, targetThreadId, false);
                            }
                        }
                        break;
                    }
                }
            }
            catch (Exception)
            {
                // If we can't bring the existing instance to foreground, just exit silently
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();
            base.OnExit(e);
        }

        // Windows API declarations for bringing window to foreground
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        private const int SW_RESTORE = 9;
    }
}