<p align="center">
  <img src="Assets/pupstrap.png" width="128" alt="Pupstrap">
</p>

<h1 align="center">Pupstrap</h1>

<p align="center">Basically Fishstrap but better. A Roblox bootstrapper with mods, fast flags, a live fast flag editor, multi-account launching and a custom launch window.</p>

> [!WARNING]
> I do not condone cheating or using the live fflag editor for cheating purposes.

## Features

- **Installs and updates Roblox** straight from Roblox's servers, with version pinning and channel switching.
- **Fast Flags** with presets (FPS cap up to 1000, rendering API, lighting, MSAA, texture quality, graphics level and more) plus a full flag editor with JSON import/export.
- **Live Flags**: edit *any* fast flag inside a running Roblox, including flags `ClientAppSettings.json` ignores. Comes with ultra-low and ultra-high profiles and can apply a profile automatically every launch.
- **Multi-instance**: run several Roblox windows at once.
- **Account manager**: save multiple accounts and launch them into the same game or server side by side.
- **Mods**: custom font, death sound and cursor, plus a mods folder for anything else.
- **Integrations**: Discord Rich Presence with a join button and server location notifications.
- **Appearance**: themes, accent colours and six launch window styles (Pupstrap, Fluent, Compact, Minimal, Classic, Hidden), with custom title, icon, background and position.

## Download

1. Go to [Releases](../../releases) and download `Pupstrap.exe`.
2. Run it. Windows will say **"Windows protected your PC"** because the exe isn't code-signed. Click **More info**, then **Run anyway**.
3. Go to **Behaviour** and click **Install**. This copies Pupstrap to `%LocalAppData%\Pupstrap\App`, makes it the Roblox launcher and adds it to the Start menu.

Needs 64-bit Windows 10 or 11. Nothing else to install: .NET is built into the exe.

## Tutorial

### Playing

- Press **Launch Roblox** on the **Home** page, or open **Pupstrap** from the Start menu.
- After installing, the **Play** button on roblox.com also goes through Pupstrap. To undo that, go to **Behaviour** and click **Restore Roblox**.
- To join a specific game, paste a place ID or game link into **Quick join**.

### Fast Flags

1. Open **Fast Flags**.
2. Pick presets, such as an FPS limit of `1000`. Turn off VSync in-game to go above your monitor's refresh rate.
3. To add your own flags, use **Import** or **Paste** with JSON like `{"DFIntTaskSchedulerTargetFps": "1000"}`.
4. Launch Roblox. Flags are written every launch.

Roblox only reads allowlisted flags from `ClientAppSettings.json`. For any other flag, use Live Flags.

### Live Flags

Live Flags writes flag values directly into a running Roblox. It needs an offset dump for **your exact Roblox version**.

1. Find your version on the **Home** page under **Installed**. It looks like `version-11f155f3450f4c8e`.
2. Get the FFlag offsets for that version from **im theo's offsets: https://offsets.imtheo.lol/**. Save the FFlags file (`.hpp` or `.json`).
3. Open **Live Flags**, click **Browse** and pick the file. The line under it shows the version and how many flags loaded.
4. Start Roblox, then click **Attach**.
5. Then either:
   - **Load profile** and pick `ultra-low` or `ultra-high`, or
   - search for a flag, type a value and click **Apply**.
6. Graphics flags mostly take effect when a place loads, so apply them on the home screen and then join a game.

To have a profile applied every time automatically, pick it under **Apply on launch**.

Notes:
- Every Roblox update needs a new dump. Pupstrap refuses to write if the dump doesn't match the running version.
- Values reset when Roblox closes.
- **Save profile** stores your changes as a `.flags` file you can share.

### Multiple accounts

1. Go to **Behaviour** and turn on **Multi-instance**. Close all Roblox windows first.
2. Go to **Accounts** and paste a `.ROBLOSECURITY` cookie. To find it, open roblox.com in a browser where you're signed in, then go to DevTools → Application → Cookies.
3. Tick the accounts you want, enter a game (and optionally a server job ID), and press **Launch selected**.

Saved accounts use real Roblox cookies. They're encrypted with your Windows login and stay on that PC. Never share `Accounts.json` or paste your cookie anywhere else.

### Mods

- **Mods** page: pick a custom font, death sound or cursor.
- Anything you put in the mods folder (**Open folder**) is copied over Roblox's files on launch. For example, `content\textures\...` replaces that texture.
- Mods are undone automatically when you remove them.

### Launch window

Go to **Appearance**, then **Launch window**, to choose:
- the style,
- the title, icon and background image,
- where it appears on screen,
- whether it shows the percentage and a cancel button,
- whether it stays open until Roblox's window appears.

Press **Preview** to see it.

## Where your stuff is stored

Everything lives in `%LocalAppData%\Pupstrap`:

| Path | What |
|---|---|
| `Settings.json` | All your settings |
| `State.json` | Installed version and the original Roblox launcher, for Restore |
| `Accounts.json` | Saved accounts (encrypted cookies) |
| `Versions\` | Roblox itself |
| `Downloads\` | Download cache, which makes updates faster |
| `Mods\` | Your mods |
| `Profiles\` | Live Flags profiles |
| `Logs\` | Launch logs, useful for bug reports |

Delete the folder to reset Pupstrap completely.

## Building from source

Needs the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```powershell
dotnet publish Pupstrap.csproj -c Release -o release
```

The result is `release\Pupstrap.exe`.
