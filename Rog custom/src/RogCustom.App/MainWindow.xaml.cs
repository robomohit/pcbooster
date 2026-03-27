using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace RogCustom.App;

public partial class MainWindow : Window
{
    public static MainWindow? Instance { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        Instance = this;
        InitializeWebViewAsync();
    }

    private async void InitializeWebViewAsync()
    {
        // Must ensure core is ready
        await webView.EnsureCoreWebView2Async(null);

        // Inject the C# hardware bridge into JavaScript
        webView.CoreWebView2.AddHostObjectToScript("backend", new InteropWrapper());

        // Lock down the browser so it feels like a native app
        webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
        
        // Load the web UI file by searching upwards dynamically
        string currentDir = AppDomain.CurrentDomain.BaseDirectory;
        string? htmlPath = null;
        
        DirectoryInfo? dir = new DirectoryInfo(currentDir);
        while (dir != null)
        {
            string testPath = Path.Combine(dir.FullName, "index.html");
            if (File.Exists(testPath))
            {
                // Verify this is the Rog custom folder by checking for script.js too
                if (File.Exists(Path.Combine(dir.FullName, "script.js")))
                {
                    htmlPath = testPath;
                    break;
                }
            }
            dir = dir.Parent;
        }
        
        if (htmlPath != null)
        {
            webView.Source = new Uri(htmlPath);
        }
        else
        {
            MessageBox.Show($"Could not find index.html anywhere above {currentDir}", "UI Missing", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2)
                ToggleMaximize();
            else
                DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
    
    public async void ShowToast(string message, int durationMs = 3000)
    {
        try
        {
            if (webView?.CoreWebView2 != null)
            {
                // Escape the message for safe JavaScript injection
                var escaped = message.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n");
                await webView.CoreWebView2.ExecuteScriptAsync(
                    $"if (typeof showToast === 'function') {{ showToast('{escaped}', {durationMs}); }}");
            }
        }
        catch
        {
            // WebView2 may not be ready yet -- silently ignore
        }
    }
}
