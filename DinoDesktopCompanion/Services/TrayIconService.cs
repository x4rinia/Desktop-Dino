using System.Drawing;
using Forms = System.Windows.Forms;
using DinoDesktopCompanion.Configuration;
using DinoDesktopCompanion.GPU;

namespace DinoDesktopCompanion.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Icon _dinoIcon;
    private MainWindow _window;
    private readonly Action _exit;
    private readonly Action _openProfiles;

    public TrayIconService(MainWindow window, ConfigurationService configuration, OllamaClient ollama, Action exit, Action openProfiles)
    {
        _window = window;
        _exit = exit;
        _openProfiles = openProfiles;
        
        _dinoIcon = AppIconService.LoadDrawingIcon();
        _icon = new Forms.NotifyIcon { Icon = _dinoIcon, Text = "Dino Desktop Companion", Visible = true };
        RebuildMenu();
        _icon.DoubleClick += (_, _) => _window.Dispatcher.Invoke(_window.OpenDinoMenu);
    }
    
    public void UpdateWindow(MainWindow newWindow)
    {
        _window = newWindow;
        RebuildMenu();
    }
    
    private void RebuildMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Dino-Menü öffnen", null, (_, _) => _window.Dispatcher.Invoke(_window.OpenDinoMenu));
        menu.Opening += (_, _) => { };
        
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => _window.Dispatcher.Invoke(_exit));
        _icon.ContextMenuStrip = menu;
    }
    
    public void Dispose() { _icon.Visible = false; _icon.Dispose(); _dinoIcon.Dispose(); }
}
