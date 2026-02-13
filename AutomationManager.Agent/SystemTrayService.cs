using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace AutomationManager.Agent;

[SupportedOSPlatform("windows6.1")]
public class SystemTrayService : IHostedService, IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;
    private const int SW_RESTORE = 9;
    private const int SW_SHOWNOACTIVATE = 4;
    private const int SW_SHOWNA = 8;

    private static IntPtr _consoleWindow = IntPtr.Zero;

    private readonly ILogger<SystemTrayService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private readonly string _agentName;
    private Thread? _trayThread;

    public SystemTrayService(
        ILogger<SystemTrayService> logger,
        IConfiguration configuration,
        IHostApplicationLifetime applicationLifetime)
    {
        _logger = logger;
        _configuration = configuration;
        _applicationLifetime = applicationLifetime;
        _agentName = configuration["AgentName"] ?? Environment.MachineName;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting System Tray Service");

        // Capture console window handle at startup
        _consoleWindow = GetConsoleWindow();
        if (_consoleWindow != IntPtr.Zero)
        {
            GetWindowThreadProcessId(_consoleWindow, out uint processId);
            bool isVisible = IsWindowVisible(_consoleWindow);
            _logger.LogInformation("Console window captured: Handle={Handle}, ProcessId={ProcessId}, CurrentProcessId={CurrentProcessId}, IsVisible={IsVisible}", 
                _consoleWindow, processId, Environment.ProcessId, isVisible);
            
            // Hide console by default when running from .exe (not from 'dotnet run')
            // Check if parent process is not a terminal/powershell
            ShowWindow(_consoleWindow, SW_HIDE);
            _logger.LogInformation("Console window hidden by default (use system tray to show)");
        }
        else
        {
            _logger.LogWarning("No console window found at startup");
        }

        // Create tray icon on STA thread
        _trayThread = new Thread(() =>
        {
            try
            {
                _notifyIcon = new System.Windows.Forms.NotifyIcon
                {
                    Icon = CreateDefaultIcon(),
                    Text = $"AutomationManager Agent - {_agentName}",
                    Visible = true
                };

                var contextMenu = new System.Windows.Forms.ContextMenuStrip();
                
                var configurationItem = new System.Windows.Forms.ToolStripMenuItem("Configuration");
                configurationItem.Click += (s, e) => ShowConfiguration();
                
                var separator1 = new System.Windows.Forms.ToolStripSeparator();
                
                var showConsoleItem = new System.Windows.Forms.ToolStripMenuItem("Show Console");
                showConsoleItem.Click += (s, e) => ShowConsole();
                
                var hideConsoleItem = new System.Windows.Forms.ToolStripMenuItem("Hide Console");
                hideConsoleItem.Click += (s, e) => HideConsole();

                var separator2 = new System.Windows.Forms.ToolStripSeparator();

                var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
                exitItem.Click += (s, e) =>
                {
                    _logger.LogInformation("Exit requested from system tray");
                    System.Windows.Forms.Application.Exit();
                    _applicationLifetime.StopApplication();
                };

                contextMenu.Items.Add(configurationItem);
                contextMenu.Items.Add(separator1);
                contextMenu.Items.Add(showConsoleItem);
                contextMenu.Items.Add(hideConsoleItem);
                contextMenu.Items.Add(separator2);
                contextMenu.Items.Add(exitItem);

                _notifyIcon.ContextMenuStrip = contextMenu;

                _notifyIcon.DoubleClick += (s, e) =>
                {
                    ShowConsole();
                };

                _logger.LogInformation("System tray icon created successfully");

                // Start message loop
                System.Windows.Forms.Application.Run();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in system tray thread");
            }
        });

        _trayThread.SetApartmentState(ApartmentState.STA);
        _trayThread.IsBackground = true;
        _trayThread.Start();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping System Tray Service");

        if (_notifyIcon != null && _trayThread != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            System.Windows.Forms.Application.ExitThread();
        }

        return Task.CompletedTask;
    }

    private void ShowConsole()
    {
        try
        {
            // Get current console window handle
            var currentHandle = GetConsoleWindow();
            
            if (currentHandle == IntPtr.Zero)
            {
                // No console exists, allocate one
                _logger.LogInformation("No console window found, allocating new console");
                if (AllocConsole())
                {
                    _consoleWindow = GetConsoleWindow();
                    _logger.LogInformation("Console allocated successfully. Handle={Handle}", _consoleWindow);
                }
                else
                {
                    _logger.LogError("Failed to allocate console");
                    return;
                }
            }
            else
            {
                _consoleWindow = currentHandle;
            }

            bool wasVisible = IsWindowVisible(_consoleWindow);
            _logger.LogInformation("ShowConsole: Handle={Handle}, WasVisible={WasVisible}", _consoleWindow, wasVisible);
            
            if (_consoleWindow != IntPtr.Zero)
            {
                // Try multiple approaches to ensure window is shown
                ShowWindow(_consoleWindow, SW_RESTORE);
                ShowWindow(_consoleWindow, SW_SHOW);
                BringWindowToTop(_consoleWindow);
                SetForegroundWindow(_consoleWindow);
                
                bool isNowVisible = IsWindowVisible(_consoleWindow);
                _logger.LogInformation("Console show complete. IsNowVisible={IsNowVisible}", isNowVisible);
            }
            else
            {
                _logger.LogWarning("Cannot show console - no valid window handle");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing console");
        }
    }

    private void HideConsole()
    {
        try
        {
            // Refresh the console window handle in case it changed
            var currentHandle = GetConsoleWindow();
            if (currentHandle != IntPtr.Zero)
            {
                _consoleWindow = currentHandle;
            }

            bool wasVisible = IsWindowVisible(_consoleWindow);
            _logger.LogInformation("HideConsole: Handle={Handle}, WasVisible={WasVisible}", _consoleWindow, wasVisible);
            
            if (_consoleWindow != IntPtr.Zero)
            {
                ShowWindow(_consoleWindow, SW_HIDE);
                
                bool isNowVisible = IsWindowVisible(_consoleWindow);
                _logger.LogInformation("Console hide complete. IsNowVisible={IsNowVisible}", isNowVisible);
            }
            else
            {
                _logger.LogWarning("Cannot hide console - no valid window handle");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hiding console");
        }
    }

    private void ShowConfiguration()
    {
        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            using var configForm = new ConfigurationForm(configPath);
            configForm.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening configuration form");
            System.Windows.Forms.MessageBox.Show(
                $"Error opening configuration: {ex.Message}",
                "Error",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
        }
    }

    private Icon CreateDefaultIcon()
    {
        // Create a simple icon with text "AM" (AutomationManager)
        var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.DodgerBlue);
            using (var font = new Font("Arial", 6, FontStyle.Bold))
            {
                g.DrawString("AM", font, Brushes.White, new PointF(1, 3));
            }
        }

        IntPtr hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
    }
}
