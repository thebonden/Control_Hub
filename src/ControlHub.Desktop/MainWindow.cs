using System.Diagnostics;

namespace ControlHub.Desktop;

internal sealed class MainWindow : Form
{
    private readonly SettingsStore store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ControlHub"));
    private readonly HostSession host = new();
    private Settings settings;
    private readonly ComboBox language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly TextBox obsUrl = new() { Width = 440 };
    private readonly TextBox obsPassword = new() { Width = 440, UseSystemPasswordChar = true };
    private readonly Label heading = new() { AutoSize = true, Font = new Font("Segoe UI", 18, FontStyle.Bold) };
    private readonly Label introduction = new() { AutoSize = true, MaximumSize = new Size(700, 0) };
    private readonly Label urlLabel = new() { AutoSize = true };
    private readonly Label passwordLabel = new() { AutoSize = true };
    private readonly Label status = new() { AutoSize = true, MaximumSize = new Size(700, 0), Padding = new Padding(0, 12, 0, 12) };
    private readonly Button save = new() { AutoSize = true };
    private readonly Button copy = new() { AutoSize = true };
    private readonly Button start = new() { AutoSize = true };
    private readonly Button stop = new() { AutoSize = true };
    private readonly Button refresh = new() { AutoSize = true };
    private readonly Button install = new() { AutoSize = true };
    private readonly Button guide = new() { AutoSize = true };
    private bool busy, closing, canClose;
    private bool Danish => language.SelectedIndex != 1;
    private string T(string da, string en) => Danish ? da : en;

    public MainWindow()
    {
        settings = store.Load();
        store.Save(settings);
        Text = "Control Hub · 0.0.001";
        ClientSize = new Size(760, 590);
        MinimumSize = new Size(720, 600);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(24) };
        Controls.Add(layout);
        language.Items.AddRange(["Dansk", "English"]);
        language.SelectedIndex = settings.Language == "en" ? 1 : 0;
        obsUrl.Text = settings.ObsUrl;
        obsPassword.Text = settings.ObsPassword;
        layout.Controls.AddRange([heading, introduction, language, urlLabel, obsUrl, passwordLabel, obsPassword]);
        var settingsButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(700, 0) };
        settingsButtons.Controls.AddRange([save, copy]);
        layout.Controls.Add(settingsButtons);
        var hostButtons = new FlowLayoutPanel { AutoSize = true };
        hostButtons.Controls.AddRange([start, stop, refresh]);
        layout.Controls.Add(hostButtons);
        layout.Controls.Add(status);
        layout.Controls.Add(install);
        layout.Controls.Add(guide);
        language.SelectedIndexChanged += (_, _) => { Translate(); ShowStoppedOrRunning(); };
        save.Click += async (_, _) => await RunAsync(() => { SaveSettings(); status.Text = T("Indstillinger gemt.", "Settings saved."); return Task.CompletedTask; });
        copy.Click += async (_, _) => await RunAsync(() => { Clipboard.SetText(settings.Token); status.Text = T("Forbindelsesnøglen er kopieret. Indsæt den i Stream Deck-knappens tokenfelt, og tryk Gem.", "Connection key copied. Paste it into the Stream Deck key's token field, then click Save."); return Task.CompletedTask; });
        start.Click += async (_, _) => await RunAsync(async () => { SaveSettings(); await host.StartAsync(settings); await ShowStatusAsync(); });
        stop.Click += async (_, _) => await RunAsync(async () => { await host.StopAsync(); ShowStoppedOrRunning(); });
        refresh.Click += async (_, _) => await RunAsync(ShowStatusAsync);
        install.Click += async (_, _) => await RunAsync(() => OpenFile(Path.Combine("Plugins", "StreamDeck", "dk.thebonden.controlhub.streamDeckPlugin")));
        guide.Click += async (_, _) => await RunAsync(() => OpenFile(Path.Combine("docs", Danish ? "installation.da.html" : "installation.en.html")));
        FormClosing += async (_, e) =>
        {
            if (canClose) return;
            e.Cancel = true;
            if (closing) return;
            closing = true;
            Enabled = false;
            // Wait for the active UI operation before stopping its child.
            // Vent på den aktive UI-handling, før dens underproces stoppes.
            while (busy) await Task.Delay(50);
            try { await host.StopAsync(); }
            catch { closing = false; Enabled = true; status.Text = T("Kunne ikke stoppe hosten. Prøv Stop igen.", "Could not stop the host. Try Stop again."); return; }
            host.Dispose();
            canClose = true;
            Close();
        };
        Translate();
        ShowStoppedOrRunning();
    }

    private void Translate()
    {
        heading.Text = "Control Hub · 0.0.001";
        introduction.Text = T("Udviklingsversion · Stream Deck, PowerPoint og OBS Studio\nStart selv ét slideshow. Hold dette vindue åbent under brug.", "Development build · Stream Deck, PowerPoint and OBS Studio\nStart one slideshow manually. Keep this window open during use.");
        urlLabel.Text = T("OBS WebSocket-adresse (på denne computer)", "OBS WebSocket address (on this computer)");
        passwordLabel.Text = T("OBS WebSocket-adgangskode", "OBS WebSocket password");
        save.Text = T("Gem indstillinger", "Save settings");
        copy.Text = T("Kopiér forbindelsesnøgle", "Copy connection key");
        start.Text = T("Start Control Hub", "Start Control Hub");
        stop.Text = T("Stop Control Hub", "Stop Control Hub");
        refresh.Text = T("Kontrollér forbindelse", "Check connection");
        install.Text = T("Installér Stream Deck-plugin", "Install Stream Deck plugin");
        guide.Text = T("Åbn installationsvejledning", "Open installation guide");
        UpdateEnabled();
    }

    private void SaveSettings()
    {
        var updated = settings with { ObsUrl = obsUrl.Text.Trim(), ObsPassword = obsPassword.Text, Language = Danish ? "da" : "en" };
        store.Save(updated);
        settings = updated;
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (busy || closing) return;
        busy = true;
        UpdateEnabled();
        status.Text = T("Arbejder…", "Working…");
        try { await action(); }
        catch (InvalidDataException) { status.Text = T("Kontrollér OBS-adressen: brug ws://127.0.0.1:4455 eller din lokale OBS-port.", "Check the OBS address: use ws://127.0.0.1:4455 or your local OBS port."); }
        catch { status.Text = T("Handlingen mislykkedes. Kontrollér, om en anden Control Hub bruger port 5187, og se vejledningen.", "Operation failed. Check whether another Control Hub is using port 5187 and see the guide."); }
        finally { busy = false; UpdateEnabled(); }
    }

    private async Task ShowStatusAsync()
    {
        using var document = await host.StatusAsync();
        var lines = new List<string> { T("Control Hub svarer · ", "Control Hub responds · ") + document.RootElement.GetProperty("version").GetString() };
        foreach (var plugin in document.RootElement.GetProperty("plugins").EnumerateArray())
        {
            var id = plugin.GetProperty("id").GetString();
            if (id is not ("powerpoint" or "obs")) continue;
            lines.Add((id == "obs" ? "OBS Studio: " : "PowerPoint: ") + plugin.GetProperty("message").GetProperty(Danish ? "da" : "en").GetString());
        }
        status.Text = string.Join(Environment.NewLine, lines);
    }

    private void ShowStoppedOrRunning() => status.Text = host.IsRunning
        ? T("Control Hub kører. Kontrollér forbindelsen for aktuel status.", "Control Hub is running. Check connection for current status.")
        : T("Control Hub er stoppet. Gem indstillingerne, og tryk Start.", "Control Hub is stopped. Save settings, then click Start.");

    private void UpdateEnabled()
    {
        var running = host.IsRunning;
        start.Enabled = save.Enabled = obsUrl.Enabled = obsPassword.Enabled = !busy && !running;
        stop.Enabled = refresh.Enabled = !busy && running;
        copy.Enabled = install.Enabled = guide.Enabled = language.Enabled = !busy;
    }

    private static Task OpenFile(string relative)
    {
        Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, relative)) { UseShellExecute = true });
        return Task.CompletedTask;
    }
}
