using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pupstrap.Core;

namespace Pupstrap.Accounts;

public sealed class Account
{
    public long UserId { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string Alias { get; set; } = "";
    public DateTime LastUsed { get; set; }
    public string ProtectedCookie { get; set; } = "";

    public string Label => string.IsNullOrWhiteSpace(Alias) ? DisplayName : Alias;
    public string Handle => $"@{Username}";
}

public static class AccountStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Pupstrap.Accounts.v1");

    public static ObservableCollection<Account> Accounts { get; } = new();

    public static void Load()
    {
        Accounts.Clear();
        if (!File.Exists(Paths.AccountsFile)) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<Account>>(File.ReadAllText(Paths.AccountsFile)) ?? new();
            foreach (var account in list.OrderByDescending(a => a.LastUsed)) Accounts.Add(account);
        }
        catch (JsonException ex)
        {
            Logger.Error("Accounts file is corrupt", ex);
        }
    }

    public static void Save()
    {
        var temp = Paths.AccountsFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Accounts.ToList(), new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, Paths.AccountsFile, true);
    }

    public static string Protect(string cookie) =>
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(cookie), Entropy, DataProtectionScope.CurrentUser));

    public static string Unprotect(Account account) =>
        Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(account.ProtectedCookie), Entropy, DataProtectionScope.CurrentUser));

    public static string CleanCookie(string input)
    {
        var cookie = input.Trim().Trim('"');
        const string prefix = ".ROBLOSECURITY=";
        if (cookie.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) cookie = cookie[prefix.Length..];
        var semicolon = cookie.IndexOf(';');
        if (semicolon >= 0) cookie = cookie[..semicolon];
        return cookie.Trim();
    }

    public static Account Upsert(RobloxUser user, string cookie)
    {
        var existing = Accounts.FirstOrDefault(a => a.UserId == user.Id);
        var account = existing ?? new Account { UserId = user.Id };
        account.Username = user.Name;
        account.DisplayName = user.DisplayName;
        account.AvatarUrl = user.AvatarUrl;
        account.ProtectedCookie = Protect(cookie);
        if (existing is null) Accounts.Insert(0, account);
        Save();
        return account;
    }

    public static void Remove(Account account)
    {
        Accounts.Remove(account);
        Save();
    }
}
