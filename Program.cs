using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace X20Guard
{
    
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // --- SISTEMA DE CAJA NEGRA (Captura errores fatales) ---
            Application.ThreadException += (s, e) => LogFatalError(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogFatalError(e.ExceptionObject as Exception);

            try
            {
                EstablecerPrioridadTiempoReal();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new X20ApplicationContext());
            }
            catch (Exception ex)
            {
                LogFatalError(ex);
            }
        }

        public static void LogFatalError(Exception? ex)
        {
            if (ex == null) return;
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string filePath = Path.Combine(desktopPath, "X20Guard_Error.txt");
                File.AppendAllText(filePath, $"[{DateTime.Now}] CRASHEO FATAL: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
            }
            catch { /* Evitar bucle de errores */ }
        }

        private static void EstablecerPrioridadTiempoReal()
        {
            try
            {
                using (Process p = Process.GetCurrentProcess())
                {
                    p.PriorityClass = ProcessPriorityClass.RealTime; // Prioridad Máxima
                }
            }
            catch
            {
                try
                {
                    using (Process p = Process.GetCurrentProcess())
                    {
                        p.PriorityClass = ProcessPriorityClass.High; // Fallback
                    }
                }
                catch { } // Silencioso si falta permisos de admin
            }
        }
    }

    public class X20ApplicationContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private Icon iconGreen;
        private Icon iconRed;
        private Icon iconGray;

        private System.Windows.Forms.Timer statusTimer;

        [DllImport("user32.dll")]
        public static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

        const int ENUM_CURRENT_SETTINGS = -1;

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
        }

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        private const byte VK_LWIN = 0x5B;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_SHIFT = 0x10;
        private const byte VK_B = 0x42;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public X20ApplicationContext()
        {
            iconGreen = CreateIcon(Color.LimeGreen);
            iconRed = CreateIcon(Color.Red);
            iconGray = CreateIcon(Color.Gray);

            trayIcon = new NotifyIcon()
            {
                Icon = iconGray,
                ContextMenuStrip = new ContextMenuStrip(),
                Visible = true,
                Text = "X20 Guard - Iniciando..."
            };

            trayIcon.ContextMenuStrip.Items.Add("Reiniciar Driver de Video", null, (s, e) => TriggerVideoReset());
            trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            trayIcon.ContextMenuStrip.Items.Add("Salir", null, Exit);

            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            statusTimer = new System.Windows.Forms.Timer();
            statusTimer.Interval = 2000;
            statusTimer.Tick += StatusTimer_Tick;
            statusTimer.Start();

            UpdateStatus();
        }

        private void StatusTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                UpdateStatus();
            }
            catch (Exception ex)
            {
                // Si la lectura de pantalla falla temporalmente, no queremos que se cierre.
                Program.LogFatalError(new Exception("Error no fatal en UpdateStatus", ex));
            }
        }

        private void UpdateStatus()
        {
            DEVMODE vDevMode = new DEVMODE();
            vDevMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref vDevMode))
            {
                int width = vDevMode.dmPelsWidth;
                int height = vDevMode.dmPelsHeight;
                int hz = vDevMode.dmDisplayFrequency;

                if (width == 1440 && height == 900)
                {
                    if (hz >= 97)
                    {
                        trayIcon.Icon = iconGreen;
                        trayIcon.Text = $"X20 Guard: OK ({width}x{height} @ {hz}Hz)";
                    }
                    else
                    {
                        trayIcon.Icon = iconRed;
                        trayIcon.Text = $"X20 Guard: ¡ALERTA! ({width}x{height} @ {hz}Hz)";
                    }
                }
                else
                {
                    trayIcon.Icon = iconGray;
                    trayIcon.Text = $"X20 Guard: En espera... ({width}x{height} @ {hz}Hz)";
                }
            }
        }

        private async void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                try
                {
                    await Task.Delay(4000);

                    DEVMODE vDevMode = new DEVMODE();
                    vDevMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

                    if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref vDevMode))
                    {
                        if (vDevMode.dmPelsWidth == 1440 && vDevMode.dmPelsHeight == 900 && vDevMode.dmDisplayFrequency < 97)
                        {
                            TriggerVideoReset();
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Si ocurre un error durante la reanudación de energía, queda registrado
                    // y el programa SOBREVIVE.
                    Program.LogFatalError(new Exception("Error al despertar de la suspensión", ex));
                }
            }
        }

        private void TriggerVideoReset()
        {
            try
            {
                keybd_event(VK_LWIN, 0, 0, 0);
                keybd_event(VK_CONTROL, 0, 0, 0);
                keybd_event(VK_SHIFT, 0, 0, 0);
                keybd_event(VK_B, 0, 0, 0);

                keybd_event(VK_B, 0, KEYEVENTF_KEYUP, 0);
                keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
                keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
            }
            catch (Exception ex)
            {
                Program.LogFatalError(new Exception("Fallo al ejecutar Win+Ctrl+Shift+B", ex));
            }
        }

        private Icon CreateIcon(Color color)
        {
            Bitmap bitmap = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                using (Brush brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, 2, 2, 12, 12);
                }
                using (Pen pen = new Pen(Color.Black, 1))
                {
                    g.DrawEllipse(pen, 2, 2, 12, 12);
                }
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        private void Exit(object? sender, EventArgs e)
        {
            statusTimer.Stop();
            trayIcon.Visible = false;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            Application.Exit();
        }
    }
}