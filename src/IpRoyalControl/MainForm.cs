using System.ServiceProcess;
using IpRoyalService;

namespace IpRoyalControl;

public sealed class MainForm : Form
{
    private readonly ControlConfigStore store = new(Path.Combine(AppContext.BaseDirectory, "config.json"));
    private readonly TextBox server = new();
    private readonly ComboBox protocol = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown serverPort = NumberInput(1, 65535, 1080);
    private readonly TextBox username = new();
    private readonly TextBox password = new() { UseSystemPasswordChar = true };
    private readonly NumericUpDown reservePort = NumberInput(1, 65533, 2080);
    private readonly Label serviceValue = ValueLabel();
    private readonly Label connectionValue = ValueLabel();
    private readonly Label protocolValue = ValueLabel();
    private readonly Label message = new() { AutoSize = true, ForeColor = Color.DimGray };
    private readonly Button start = new() { Text = "Start / Connect", AutoSize = true };
    private readonly Button stop = new() { Text = "Stop / Disconnect", AutoSize = true };
    private readonly Button restart = new() { Text = "Restart / Reconnect", AutoSize = true };
    private readonly RichTextBox logs = new() { ReadOnly = true, WordWrap = false, DetectUrls = false, Dock = DockStyle.Fill, Font = new Font("Consolas", 9), BackColor = Color.White };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 3000 };
    private bool busy;

    public MainForm()
    {
        Text = "IPRoyal Proxy Control";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 620);
        Size = new Size(900, 720);
        Font = new Font("Segoe UI", 9);
        Icon = SystemIcons.Shield;
        protocol.Items.AddRange(new object[] { "HTTP", "SOCKS4", "SOCKS5" });

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 4, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);
        root.Controls.Add(BuildStatusPanel(), 0, 0);
        root.Controls.Add(BuildConfigurationPanel(), 0, 1);
        root.Controls.Add(BuildActionsPanel(), 0, 2);
        root.Controls.Add(BuildLogsPanel(), 0, 3);

        start.Click += async (_, _) => await RunServiceAction("Starting service...", ServiceManager.Start);
        stop.Click += async (_, _) => await RunServiceAction("Stopping service...", ServiceManager.Stop);
        restart.Click += async (_, _) => await RunServiceAction("Restarting service...", ServiceManager.Restart);
        timer.Tick += (_, _) => RefreshView();
        Shown += (_, _) => { LoadConfiguration(); RefreshView(); timer.Start(); };
        FormClosed += (_, _) => timer.Stop();
    }

    private Control BuildStatusPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 6, Padding = new Padding(8) };
        panel.Controls.Add(new Label { Text = "Service:", AutoSize = true }, 0, 0); panel.Controls.Add(serviceValue, 1, 0);
        panel.Controls.Add(new Label { Text = "Connection:", AutoSize = true, Margin = new Padding(24, 3, 3, 3) }, 2, 0); panel.Controls.Add(connectionValue, 3, 0);
        panel.Controls.Add(new Label { Text = "Protocol:", AutoSize = true, Margin = new Padding(24, 3, 3, 3) }, 4, 0); panel.Controls.Add(protocolValue, 5, 0);
        return new GroupBox { Text = "Current status", Dock = DockStyle.Top, AutoSize = true, Controls = { panel } };
    }

    private Control BuildConfigurationPanel()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Padding = new Padding(8) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        AddField(grid, 0, "Protocol", protocol); AddField(grid, 0, "Proxy server", server, 2);
        AddField(grid, 1, "Server port", serverPort); AddField(grid, 1, "Reserved/local port", reservePort, 2);
        AddField(grid, 2, "Username / SOCKS4 user ID", username); AddField(grid, 2, "Password (not used by SOCKS4)", password, 2);
        var save = new Button { Text = "Save configuration and restart", AutoSize = true, Margin = new Padding(3, 10, 3, 3) };
        save.Click += async (_, _) => await SaveConfiguration();
        grid.Controls.Add(save, 2, 2); grid.SetColumnSpan(save, 2);
        var box = new GroupBox { Text = "Proxy configuration", Dock = DockStyle.Top, AutoSize = true };
        box.Controls.Add(grid); return box;
    }

    private Control BuildActionsPanel()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 8, 0, 8) };
        panel.Controls.Add(start); panel.Controls.Add(stop); panel.Controls.Add(restart); panel.Controls.Add(message);
        return panel;
    }

    private Control BuildLogsPanel()
    {
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        var refresh = new Button { Text = "Refresh logs", AutoSize = true };
        var clear = new Button { Text = "Clear displayed view", AutoSize = true };
        refresh.Click += (_, _) => RefreshLogs(); clear.Click += (_, _) => logs.Clear();
        buttons.Controls.Add(new Label { Text = "Recent service logs", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 8, 16, 3) });
        buttons.Controls.Add(refresh); buttons.Controls.Add(clear);
        outer.Controls.Add(buttons, 0, 0); outer.Controls.Add(logs, 0, 1); return outer;
    }

    private void LoadConfiguration()
    {
        try
        {
            var c = store.Load(); server.Text = c.Server; serverPort.Value = c.ServerPort;
            username.Text = c.Username; password.Text = c.Password; reservePort.Value = c.ReservePort;
            protocol.SelectedItem = c.TryGetProtocol(out var selected) ? selected.ToConfigValue() : null;
            if (protocol.SelectedIndex < 0) message.Text = "Select a protocol before saving this older configuration.";
        }
        catch (Exception e) { ShowError("Could not load config.json", e); }
    }

    private async Task SaveConfiguration()
    {
        try
        {
            SetBusy(true, "Saving protected configuration...");
            if (!ProxyProtocolNames.TryParse(protocol.SelectedItem?.ToString(), out var selected)) throw new InvalidOperationException("Select HTTP, SOCKS4, or SOCKS5.");
            var config = new ProxyConfig(null, null, server.Text, (int)serverPort.Value, (int)reservePort.Value, username.Text, password.Text, selected.ToConfigValue());
            store.Save(config);
            var state = ServiceManager.GetStatus();
            if (state is not null && state != ServiceControllerStatus.Stopped) await Task.Run(ServiceManager.Restart);
            message.Text = state == ServiceControllerStatus.Stopped ? "Configuration saved. Start the service when ready." : "Configuration saved and service restarted.";
        }
        catch (Exception e) { ShowError("Configuration was not saved", e); }
        finally { SetBusy(false); RefreshView(); }
    }

    private async Task RunServiceAction(string progress, Action action)
    {
        try { SetBusy(true, progress); await Task.Run(action); message.Text = "Service operation completed."; }
        catch (Exception e) { ShowError("Service operation failed", e); }
        finally { SetBusy(false); RefreshView(); }
    }

    private void RefreshView()
    {
        if (busy) return;
        var state = ServiceManager.GetStatus();
        serviceValue.Text = state?.ToString() ?? "Not installed";
        var snapshot = StatusReader.Read(ApplicationPaths.StatusFile);
        var status = StatusPresenter.Map(state, snapshot, DateTimeOffset.UtcNow);
        connectionValue.Text = status.Text; protocolValue.Text = status.Protocol;
        if (state == ServiceControllerStatus.Stopped) message.Text = "The Windows service is stopped.";
        else if (snapshot is not null) message.Text = snapshot.Message;
        connectionValue.ForeColor = status.State switch { ProxyConnectionState.Connected => Color.DarkGreen, ProxyConnectionState.Connecting or ProxyConnectionState.Reconnecting => Color.DarkOrange, _ => Color.Firebrick };
        start.Enabled = state == ServiceControllerStatus.Stopped;
        stop.Enabled = state is ServiceControllerStatus.Running or ServiceControllerStatus.Paused;
        restart.Enabled = state is ServiceControllerStatus.Running or ServiceControllerStatus.Paused;
        RefreshLogs();
    }

    private void RefreshLogs()
    {
        try { logs.Text = LogTailReader.Read(ApplicationPaths.LogFile, password.Text, username.Text); logs.SelectionStart = logs.TextLength; logs.ScrollToCaret(); }
        catch (Exception e) { logs.Text = "Could not read service logs: " + e.Message; }
    }

    private void SetBusy(bool value, string? text = null)
    {
        busy = value; UseWaitCursor = value;
        if (text is not null) message.Text = text;
        start.Enabled = stop.Enabled = restart.Enabled = !value;
    }

    private void ShowError(string title, Exception e)
    {
        message.Text = title + ".";
        MessageBox.Show(this, e.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static NumericUpDown NumberInput(int min, int max, int value) => new() { Minimum = min, Maximum = max, Value = value, Width = 110 };
    private static Label ValueLabel() => new() { Text = "Checking...", AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
    private static void AddField(TableLayoutPanel grid, int row, string label, Control input, int column = 0)
    {
        input.Dock = DockStyle.Fill; input.Margin = new Padding(3, 4, 12, 4);
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, column, row);
        grid.Controls.Add(input, column + 1, row);
    }
}
