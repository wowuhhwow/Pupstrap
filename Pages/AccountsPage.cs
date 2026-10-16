using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pupstrap.Accounts;
using Pupstrap.Core;
using Pupstrap.UI;
using Wpf.Ui.Controls;
using Image = System.Windows.Controls.Image;
using PasswordBox = Wpf.Ui.Controls.PasswordBox;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Pupstrap.Pages;

public sealed class AccountsPage : PupPage
{
    private readonly StackPanel _list = new();
    private readonly HashSet<long> _selected = new();
    private readonly ProgressPanel _progress = new();
    private readonly PasswordBox _cookie = new() { PlaceholderText = ".ROBLOSECURITY cookie", MinWidth = 320, MaxWidth = 420 };
    private readonly Wpf.Ui.Controls.TextBox _place;
    private readonly Wpf.Ui.Controls.TextBox _jobId = new() { PlaceholderText = "Server job ID (optional)", MinWidth = 300, MaxWidth = 420 };

    public AccountsPage()
    {
        var settings = Store.Settings;
        _place = Ui.Text(settings.LastPlaceInput, v => { settings.LastPlaceInput = v; Store.SaveSettings(); }, "Place ID, game link or private server link", 300);

        var add = Ui.AsyncButton("Add", AddAsync, ControlAppearance.Primary, SymbolRegular.PersonAdd24);
        var launchSelected = Ui.AsyncButton("Launch selected", LaunchSelectedAsync, ControlAppearance.Primary, SymbolRegular.Play24);

        Content = Ui.Page("Accounts", "Save several Roblox accounts and launch them side by side.",
            Ui.Info("Cookies are full account access",
                "Pupstrap encrypts saved cookies with your Windows login (DPAPI) and only sends them to roblox.com. Never paste your cookie anywhere else, and never share Accounts.json.",
                InfoBarSeverity.Warning),
            Ui.Card("Add account", "In a browser signed in to roblox.com, open DevTools > Application > Cookies and copy the .ROBLOSECURITY value.",
                Ui.Row(_cookie, add), SymbolRegular.PersonAdd24),
            Ui.Section("Join"),
            Ui.Card("Game", "Leave the job ID empty to join any server. A private server link joins that server.",
                new StackPanel { Children = { _place, Spacer(6), _jobId } }, SymbolRegular.Games24),
            Ui.Card("Launch", "Starts every ticked account. More than one at a time needs multi-instance (Behaviour).",
                launchSelected, SymbolRegular.Rocket24),
            _progress,
            Ui.Section("Saved accounts"),
            _list);

        Rebuild();
    }

    private static Border Spacer(double h) => Ui.Spacer(h);

    private void Rebuild()
    {
        _list.Children.Clear();
        if (AccountStore.Accounts.Count == 0)
        {
            _list.Children.Add(Ui.Secondary("No accounts yet."));
            return;
        }
        foreach (var account in AccountStore.Accounts) _list.Children.Add(BuildRow(account));
    }

    private UIElement BuildRow(Account account)
    {
        var check = new CheckBox { IsChecked = _selected.Contains(account.UserId), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        check.Checked += (_, _) => _selected.Add(account.UserId);
        check.Unchecked += (_, _) => _selected.Remove(account.UserId);

        var avatar = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(22), ClipToBounds = true, Margin = new Thickness(0, 0, 12, 0) };
        avatar.SetResourceReference(Border.BackgroundProperty, "ControlFillColorSecondaryBrush");
        if (Uri.TryCreate(account.AvatarUrl, UriKind.Absolute, out var url))
            avatar.Child = new Image { Source = new BitmapImage(url), Stretch = Stretch.UniformToFill };

        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(new TextBlock { Text = account.Label, FontSize = 14, FontWeight = FontWeights.Medium });
        names.Children.Add(Ui.Secondary(account.LastUsed == default ? account.Handle : $"{account.Handle}  ·  last used {account.LastUsed:g}"));

        var left = Ui.Row(check, avatar, names);

        var buttons = Ui.Row(
            Ui.AsyncButton("Open app", () => LaunchAccountAsync(account, null), ControlAppearance.Secondary, SymbolRegular.Open24),
            Ui.AsyncButton("Refresh", () => RefreshAsync(account), ControlAppearance.Secondary, SymbolRegular.ArrowSync24),
            Ui.Button("Remove", () =>
            {
                if (!Ui.Confirm($"Remove {account.Label} ({account.Handle})?")) return;
                _selected.Remove(account.UserId);
                AccountStore.Remove(account);
                Rebuild();
            }, ControlAppearance.Danger, SymbolRegular.Delete24));

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(buttons, 1);
        grid.Children.Add(left);
        grid.Children.Add(buttons);

        var card = new Border { Padding = new Thickness(14, 10, 14, 10), CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 0, 6), Child = grid };
        card.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        return card;
    }

    private async Task AddAsync()
    {
        var cookie = AccountStore.CleanCookie(_cookie.Password);
        if (cookie.Length < 100) throw new InvalidOperationException("That doesn't look like a full .ROBLOSECURITY cookie.");
        var user = await RobloxApi.GetUserAsync(cookie);
        AccountStore.Upsert(user, cookie);
        _cookie.Password = "";
        Logger.Info($"Saved account {user.Id}");
        Rebuild();
    }

    private async Task RefreshAsync(Account account)
    {
        var user = await RobloxApi.GetUserAsync(AccountStore.Unprotect(account));
        AccountStore.Upsert(user, AccountStore.Unprotect(account));
        Rebuild();
    }

    private async Task LaunchSelectedAsync()
    {
        var accounts = AccountStore.Accounts.Where(a => _selected.Contains(a.UserId)).ToList();
        if (accounts.Count == 0) throw new InvalidOperationException("Tick at least one account first.");

        var target = RobloxApi.ParseJoinTarget(_place.Text, _jobId.Text)
                     ?? throw new InvalidOperationException("Enter a place ID or a roblox.com/games/... link.");
        Store.Settings.LastPlaceInput = _place.Text;

        if ((accounts.Count > 1 || Launching.Bootstrapper.RunningRoblox().Length > 0) && !Store.Settings.MultiInstance)
        {
            if (!Ui.Confirm("Running more than one Roblox window needs multi-instance. Turn it on now?")) return;
            Store.Settings.MultiInstance = true;
        }
        Store.SaveSettings();

        for (var i = 0; i < accounts.Count; i++)
        {
            await LaunchAccountAsync(accounts[i], target);
            if (i + 1 < accounts.Count)
            {
                _progress.Report($"Waiting before the next account ({i + 1}/{accounts.Count} launched)", 0);
                await Task.Delay(TimeSpan.FromSeconds(6));
            }
        }
        _progress.Done($"Launched {accounts.Count} account{(accounts.Count == 1 ? "" : "s")}.");
    }

    private async Task LaunchAccountAsync(Account account, JoinTarget? target)
    {
        _progress.Report($"Getting a launch ticket for {account.Handle}", 0);
        var cookie = AccountStore.Unprotect(account);
        var ticket = await RobloxApi.GetAuthTicketAsync(cookie);
        var uri = target is null ? RobloxApi.BuildAppUri(ticket) : RobloxApi.BuildLaunchUri(ticket, target);
        await App.LaunchFromMenuAsync(uri, _progress.Progress, CancellationToken.None);
        account.LastUsed = DateTime.Now;
        AccountStore.Save();
        _progress.Done($"Launched {account.Handle}.");
        Rebuild();
    }
}
