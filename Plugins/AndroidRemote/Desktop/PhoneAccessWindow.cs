using System.Net;
using System.Text.Json;
using ControlHub.Contracts;

namespace ControlHub.Plugins.AndroidRemote.Desktop;

public sealed class PhoneAccessWindow : Form
{
    private readonly IRemoteOwnerClient owner;
    private readonly bool danish;
    private readonly TextBox name = new() { Width = 400, MaxLength = 80 };
    private readonly CheckedListBox permissions = new() { Width = 400, Height = 138, CheckOnClick = true };
    private readonly ListView list = new() { Width = 650, Height = 155, View = View.Details,
        FullRowSelect = true, MultiSelect = false, HideSelection = false };
    private readonly Label feedback = new() { AutoSize = true, MaximumSize = new Size(650, 0) };
    private readonly Button create = new() { AutoSize = true }, copy = new() { AutoSize = true },
        remove = new() { AutoSize = true }, all = new() { AutoSize = true }, refresh = new() { AutoSize = true };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 4000 };
    private readonly CancellationTokenSource lifetime = new();
    private PairingOffer? offer;
    private bool busy, loaded, closed;
    private sealed record Row(string Id, bool Pending, string Name);
    private string T(string da, string en) => danish ? da : en;
    private string PermissionLabel(string id) => id switch
    {
        RemotePermissions.Status => T("Læs status", "Read status"),
        RemotePermissions.PowerPoint => T("Styr slides", "Control slides"),
        RemotePermissions.Scenes => T("Skift OBS-scene", "Switch OBS scene"),
        RemotePermissions.Recording => T("Start og stop optagelse", "Start and stop recording"),
        RemotePermissions.Streaming => T("Start og stop streaming", "Start and stop streaming"),
        RemotePermissions.Audio => T("Slå OBS-lyd til og fra", "Mute and unmute OBS audio"),
        _ => T("Ukendt rettighed", "Unknown permission")
    };

    public PhoneAccessWindow(IRemoteOwnerClient owner, bool danish)
    {
        this.owner = owner;
        this.danish = danish;
        Text = T("AndroidRemote · Telefonadgang", "AndroidRemote · Phone access");
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        ClientSize = new Size(720, 690);
        MinimumSize = new Size(550, 490);
        StartPosition = FormStartPosition.CenterParent;
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false,
            FlowDirection = FlowDirection.TopDown, Padding = new Padding(18) };
        Controls.Add(flow);
        var heading = LabelFor(T("Telefonadgang", "Phone access"));
        heading.Font = new Font(Font, FontStyle.Bold);
        flow.Controls.AddRange([heading, LabelFor(T(
            "Du kan oprette og fjerne parringsbeviser her. Android-appen og telefonforbindelsen er endnu ikke klar.",
            "You can create and remove pairing invitations here. The Android app and phone connection are not available yet.")),
            LabelFor(T("Telefonens navn", "Phone name")), name,
            LabelFor(T("Hvad må telefonen gøre?", "What may this phone do?")), permissions]);
        name.AccessibleName = T("Telefonens navn", "Phone name");
        permissions.AccessibleName = T("Telefonens rettigheder", "Phone permissions");
        foreach (var id in RemotePermissions.All) permissions.Items.Add(PermissionLabel(id), false);
        create.Text = T("Opret parringsbevis", "Create invitation");
        copy.Text = T("Kopiér nyt bevis", "Copy new invitation");
        remove.Text = T("Fjern valgt adgang", "Remove selected access");
        all.Text = T("Fjern al adgang", "Remove all access");
        refresh.Text = T("Opdater listen", "Refresh list");
        flow.Controls.Add(Buttons(create, copy));
        flow.Controls.Add(LabelFor(T("Parringsbeviser og parrede telefoner", "Invitations and paired phones")));
        list.AccessibleName = T("Telefonadgang", "Phone access");
        list.Columns.Add(T("Navn", "Name"), 165);
        list.Columns.Add(T("Type", "Type"), 110);
        list.Columns.Add(T("Rettigheder", "Permissions"), 250);
        list.Columns.Add(T("Udløber", "Expires"), 90);
        flow.Controls.Add(list);
        flow.Controls.Add(Buttons(refresh, remove, all));
        flow.Controls.Add(feedback);
        var close = new Button { AutoSize = true, Text = T("Luk", "Close") };
        close.Click += (_, _) => Close();
        flow.Controls.Add(close);
        CancelButton = close;

        name.TextChanged += (_, _) => UpdateEnabled();
        permissions.ItemCheck += (_, _) => { if (IsHandleCreated) BeginInvoke(UpdateEnabled); };
        list.SelectedIndexChanged += (_, _) => UpdateEnabled();
        create.Click += async (_, _) => await RunAsync(async ct =>
        {
            var selected = permissions.CheckedIndices.Cast<int>().Select(i => RemotePermissions.All[i]).ToArray();
            offer = null;
            var created = await owner.BeginAsync(name.Text.Trim(), selected, ct);
            if (closed) return;
            offer = created;
            await ReloadAsync(ct);
            if (!closed) feedback.Text = T("Beviset gælder i to minutter. Listen viser ikke den hemmelige nøgle.",
                "The invitation lasts two minutes. The list does not show its secret.");
        });
        copy.Click += async (_, _) => await RunAsync(async ct =>
        {
            await ReloadAsync(ct);
            if (closed) return;
            if (offer is null) { feedback.Text = T("Beviset er ikke længere aktivt.", "The invitation is no longer active."); return; }
            Clipboard.SetText(JsonSerializer.Serialize(offer, Wire.Json));
            feedback.Text = T("Parringsbevis kopieret. Del det kun med den tiltænkte telefon.",
                "Invitation copied. Share it only with the intended phone.");
        });
        remove.Click += async (_, _) =>
        {
            if (Selection() is not Row row) return;
            if (!Confirm(T($"Fjern adgangen for »{row.Name}«?", $"Remove access for “{row.Name}”?"))) return;
            await RunAsync(async ct => { await owner.RemoveAsync(row.Id, row.Pending, ct); await ReloadAsync(ct);
                if (!closed) feedback.Text = T("Adgangen er fjernet.", "Access removed."); });
        };
        all.Click += async (_, _) =>
        {
            if (!Confirm(T("Fjern alle telefoner og ventende parringsbeviser?", "Remove all phones and pending invitations?"))) return;
            await RunAsync(async ct => { await owner.RevokeAllAsync(ct); await ReloadAsync(ct);
                if (!closed) feedback.Text = T("Al telefonadgang er fjernet.", "All phone access removed."); });
        };
        refresh.Click += async (_, _) => await RunAsync(ReloadAsync);
        Shown += async (_, _) => { await RunAsync(ReloadAsync); if (!closed) timer.Start(); };
        timer.Tick += async (_, _) => await RunAsync(ReloadAsync);
        UpdateEnabled();
    }

    private Row? Selection() => list.SelectedItems.Count == 1 ? list.SelectedItems[0].Tag as Row : null;
    private static Label LabelFor(string value) => new() { Text = value, AutoSize = true, MaximumSize = new Size(650, 0),
        Margin = new Padding(3, 8, 3, 3) };
    private static FlowLayoutPanel Buttons(params Button[] buttons)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        panel.Controls.AddRange(buttons);
        return panel;
    }
    private bool Confirm(string text) => MessageBox.Show(this, text, Text, MessageBoxButtons.YesNo,
        MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    private async Task ReloadAsync(CancellationToken ct)
    {
        var snapshot = await owner.GetAsync(ct);
        if (closed) return;
        var selected = Selection();
        list.BeginUpdate();
        try
        {
            list.Items.Clear();
            foreach (var p in snapshot.PendingPairings) AddRow(new(p.Id, true, p.Name), p.Permissions, p.ExpiresAt, selected);
            foreach (var d in snapshot.Devices) AddRow(new(d.Id, false, d.Name), d.Permissions, d.ExpiresAt, selected);
        }
        finally { list.EndUpdate(); }
        if (offer is not null && !snapshot.PendingPairings.Any(p => p.Id == offer.Id))
        {
            offer = null;
            feedback.Text = T("Det seneste bevis er ikke længere aktivt.", "The latest invitation is no longer active.");
        }
        if (!loaded) feedback.Text = list.Items.Count == 0 ? T("Ingen telefoner eller ventende beviser.", "No phones or pending invitations.")
            : T("Listen er opdateret.", "List refreshed.");
        loaded = true;
    }

    private void AddRow(Row row, IReadOnlyList<string> scopes, DateTimeOffset expires, Row? selected)
    {
        var item = new ListViewItem(new[] { row.Name, row.Pending ? T("Venter", "Pending") : T("Parret", "Paired"),
            string.Join(", ", scopes.Select(PermissionLabel)), expires.ToLocalTime().ToString("T") }) { Tag = row };
        list.Items.Add(item);
        item.Selected = selected == row;
    }

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (busy || closed) return;
        busy = true;
        UpdateEnabled();
        try { await action(lifetime.Token); }
        catch (Exception) when (closed) { }
        catch (HttpRequestException e) when (!closed && e.StatusCode == HttpStatusCode.Conflict)
        {
            feedback.Text = T("Der er ikke plads til flere beviser. Fjern et gammelt bevis først.",
                "Invitation limit reached. Remove an older invitation first.");
        }
        catch (Exception) when (!closed)
        {
            loaded = false;
            feedback.Text = T("Handlingen kunne ikke bekræftes. Opdater listen, før du prøver igen.",
                "The operation could not be confirmed. Refresh the list before trying again.");
        }
        finally { busy = false; if (!closed) UpdateEnabled(); }
    }
    private void UpdateEnabled()
    {
        if (closed) return;
        create.Enabled = !busy && loaded && !string.IsNullOrWhiteSpace(name.Text) && permissions.CheckedItems.Count > 0;
        copy.Enabled = !busy && loaded && offer is not null;
        remove.Enabled = !busy && loaded && Selection() is not null;
        all.Enabled = !busy && loaded && list.Items.Count != 0;
        refresh.Enabled = !busy;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !closed)
        {
            closed = true;
            timer.Dispose();
            lifetime.Cancel();
            lifetime.Dispose();
            offer = null;
        }
        base.Dispose(disposing);
    }
}
