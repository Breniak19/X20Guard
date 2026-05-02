using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management; // Necesario para buscar la Tarjeta Gráfica

namespace X20Guard
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.ThreadException += (s, e) => LogFatalError(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogFatalError(e.ExceptionObject as Exception);

            try
            {
                using (Process p = Process.GetCurrentProcess())
                {
                    p.PriorityClass = ProcessPriorityClass.High;
                }

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
            catch { /* Evitar bucle */ }
        }
    }

    public class X20ApplicationContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private Icon iconGreen;
        private Icon iconRed;
        private Icon iconGray;
        private System.Windows.Forms.Timer statusTimer;

        // Banderas de seguridad MUY IMPORTANTES para el reinicio de GPU
        private bool isProcessingChange = false;
        private DateTime lastEventTime = DateTime.MinValue;

        // --- P/Invoke para LEER la pantalla ---
        [DllImport("user32.dll")]
        public static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

        const int ENUM_CURRENT_SETTINGS = -1;

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion; public short dmDriverVersion; public short dmSize;
            public short dmDriverExtra; public int dmFields; public int dmPositionX;
            public int dmPositionY; public int dmDisplayOrientation; public int dmDisplayFixedOutput;
            public short dmColor; public short dmDuplex; public short dmYResolution;
            public short dmTTOption; public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels; public int dmBitsPerPel; public int dmPelsWidth;
            public int dmPelsHeight; public int dmDisplayFlags; public int dmDisplayFrequency;
        }

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

            trayIcon.ContextMenuStrip.Items.Add("Reiniciar GPU (Estilo Restart64)", null, async (s, e) => await ForceGPURestart());
            trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            trayIcon.ContextMenuStrip.Items.Add("Salir", null, Exit);

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            statusTimer = new System.Windows.Forms.Timer();
            statusTimer.Interval = 2000;
            statusTimer.Tick += StatusTimer_Tick;
            statusTimer.Start();

            UpdateStatus();
        }

        private void StatusTimer_Tick(object? sender, EventArgs e)
        {
            if (!isProcessingChange)
            {
                try { UpdateStatus(); }
                catch (Exception ex) { Program.LogFatalError(new Exception("Error en UpdateStatus", ex)); }
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

        private async void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            // SISTEMA ANTI-REBOTE EXTENDIDO: Reiniciar la GPU dispara MUCHOS eventos de pantalla
            if (isProcessingChange || (DateTime.Now - lastEventTime).TotalSeconds < 10) return;

            isProcessingChange = true;
            lastEventTime = DateTime.Now;

            try
            {
                // Le damos tiempo a Windows para que asimile la suspensión
                await Task.Delay(4000);

                DEVMODE vDevMode = new DEVMODE();
                vDevMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

                if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref vDevMode))
                {
                    // Si el monitor despertó a 60Hz...
                    if (vDevMode.dmPelsWidth == 1440 && vDevMode.dmPelsHeight == 900 && vDevMode.dmDisplayFrequency < 97)
                    {
                        await ForceGPURestart();
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogFatalError(new Exception("Error al detectar cambio de pantalla", ex));
            }
            finally
            {
                lastEventTime = DateTime.Now;
                isProcessingChange = false;
            }
        }

        // --- NUESTRO PROPIO RESTART64 NATIVO ---
        private async Task ForceGPURestart()
        {
            try
            {
                string? gpuPnpId = null;

                // 1. Encontrar el ID de Hardware de la Tarjeta Gráfica (Intel)
                using (var searcher = new ManagementObjectSearcher("SELECT PNPDeviceID, Name FROM Win32_VideoController"))
                {
                    foreach (var device in searcher.Get())
                    {
                        string name = device["Name"]?.ToString() ?? "";
                        // Filtramos por Intel (o tomamos la que esté si solo hay una)
                        if (name.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            gpuPnpId = device["PNPDeviceID"]?.ToString();
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(gpuPnpId))
                {
                    // 2. Ejecutar pnputil para reiniciar LA TARJETA GRÁFICA (Igual que Restart64)
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "pnputil.exe",
                        Arguments = $"/restart-device \"{gpuPnpId}\"",
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = false
                    };

                    using (Process? p = Process.Start(psi))
                    {
                        if (p != null)
                        {
                            await Task.Run(() => p.WaitForExit());
                        }
                    }

                    // 3. Pausa larga para que Windows termine de reconstruir el driver WDDM
                    await Task.Delay(8000);
                }
                else
                {
                    Program.LogFatalError(new Exception("No se detectó la tarjeta gráfica Intel en WMI."));
                }
            }
            catch (Exception ex)
            {
                Program.LogFatalError(new Exception("Fallo al intentar reiniciar la GPU", ex));
            }
        }

        private Icon CreateIcon(Color color)
        {
            Bitmap bitmap = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                using (Brush brush = new SolidBrush(color)) { g.FillEllipse(brush, 2, 2, 12, 12); }
                using (Pen pen = new Pen(Color.Black, 1)) { g.DrawEllipse(pen, 2, 2, 12, 12); }
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        private void Exit(object? sender, EventArgs e)
        {
            statusTimer.Stop();
            trayIcon.Visible = false;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            Application.Exit();
        }
    }
}