# Empire Earth Launcher

A launcher for Empire Earth and The Art of Conquest: it finds your installations, starts the game, checks the game files and helps with settings, repair advice and support; setups and updates stay with the community setup.\
Coded in C# with the .NET Framework 4.8 and Krypton UI

![image](https://github.com/EE-modders/Empire-Earth-Launcher/blob/main/EEL_MainScreen.png)

*Screenshot of the original mock-up; the v2 pages differ (see [Why this fork?](#why-this-fork)).*

## Why this fork?

This fork builds on the Empire Earth Launcher by [EE-modders](https://github.com/EE-modders/Empire-Earth-Launcher) and
its contributors: their Krypton UI, their WON/NeoEE protocol code and their mod creator are the starting point.
Upstream `main` is early work in progress (last change in June 2022; its README says it is not available for download):
most controls are placeholders and the *Play* button has no function yet. The `main` branch of this fork is a rebuild on
the .NET Framework 4.8 that keeps the Krypton UI and the idea, moves the logic into a tested core library and shows only
features that work.

| | Upstream `main` | This fork (`main`) |
|---|---|---|
| *Play* button | No click handler yet | Starts Empire Earth or The Art of Conquest through Windows (compatibility settings and "Run as administrator" apply) and hands the foreground to the game window; blocked while a setup runs, refuses to start a game twice |
| Finding the game | Fixed paths (lobby file in the working directory, friends in `C:\Program Files (x86)\Neo Empire Earth\...`) | Finds community setups (since v2 and up to 1.7.2; admin, user, portable) by their records, and other installations (CD, GOG, copies) through the game's "Installed From" values, the launcher's own folder or a folder you pick; choose one on the *Launcher* page |
| Visible controls | Many placeholders (Repair CD-Keys, Clear Registry, DirectX wrapper, resolution, ...) | Only controls that work; a test keeps placeholders out ([ADR 0014](docs/adr/0014-only-working-features-in-the-ui.md)) |
| Damaged game files | Not implemented yet | For installations of the community setup since v2: read-only integrity check against the setup's file list, naming the missing or changed files |
| Repair and updates | Listed as planned | Repair advice and a version check on request; an installation of the suite package is sent to the package's release page, never to a setup that would undo its fixes; the launcher never downloads or starts a setup itself |
| Maintenance | Not implemented yet | *Tools* page: registry cleanup with `.reg` backup, WON login reset, VirtualStore check, saved games export/import, player name check |
| Support | Not implemented yet | Network check and a diagnostics report for forum posts, anonymized and never sent by the launcher |
| Game settings | Placeholder page | Recommended defaults, consistency hints, reset with `.reg` backup, compatibility options (Windows 8 and later) |
| Privacy | Unwired "collect diagnostic data" checkbox; the *Play* page polls the NeoEE server every 5 s | No telemetry; the player list only while a NeoEE installation is selected; everything else only on click: HTTPS to the update API, plus name lookups and one status request to the NeoEE server in the network check |
| Suite installer | None | Optional integration with "Empire Earth Community": its one desktop icon opens the launcher (the suite 1.0.0 icons with `--product=EE` or `NeoEE` still work), a second start brings the running launcher to the front, repair advice points to the suite and its release page |
| Languages | English only | English, German and French, with automated checks that all three have the same texts |
| Runtime | .NET Framework 4.0 | .NET Framework 4.8 for launcher and mod creator, one solution; Windows 7 SP1, 8.1, 10 (1607 or later) and 11 ([Requirements](#requirements)) |
| Log and settings | `log.txt` in the working directory; theme choice not saved; no handler for unexpected errors | Log and `settings.json` in `%LOCALAPPDATA%\Empire Earth Launcher`, damaged settings recovered, unexpected errors logged and shown |
| Tests and CI | None | More than 1,200 automated test methods (NUnit); a Windows workflow builds every pull request and push to `main` and runs all tests |
| Documentation | README | [Architecture](docs/ARCHITECTURE.md), [decision records](docs/adr/), [setup/launcher contract](docs/CONTRACT.md), [CHANGELOG](CHANGELOG.md), manual [test plan](docs/TEST-PLAN.de.md) (German) with more than 150 cases |

**For maintainers and security**

- UI-free core library (`Empire-Earth-Launcher-Core`): registry, files, processes, network and mutexes behind
  interfaces, tested with in-memory fakes; an architecture test keeps WinForms and Krypton out of the core.
- Registry writes only through an allow-list: the current user's hive only, exact keys, and for game settings exact
  value names; CD keys, install records and uninstall keys are protected under every alias. The launcher never asks
  for administrator rights.
- Web requests are HTTPS only, with certificate validation that tests forbid switching off, no redirects, a 10 s
  timeout and 4 KiB answers; download links are opened only for the project's own addresses. The NeoEE status server
  speaks plain TCP (a property of the server); its replies are treated as untrusted, checked and time-limited.
- The WON/NeoEE code lives in its own library; the lobby file and the server replies are parsed with strict bounds.
- In the launcher only one component starts programs, always through the Windows shell and never with a request for
  elevation (a game set to "Run as administrator" still shows the Windows prompt), and nothing ends a process.
- One written contract with the community setup ([docs/CONTRACT.md](docs/CONTRACT.md)), with byte samples the
  launcher's readers are tested against.

**What changed or was dropped**

- Not implemented: DirectX wrapper switch (the *Graphics* page shows the wrapper and tells how to change it in the
  setup), switching the dreXmod preset (the *Mods* page shows the presets and the choice), Discord presence, HD textures,
  auto-update and the auto-compatibility detector. They are listed under "Planned, not in v2" in the
  [Features](#-features).
- The mod creator remains a separate program; there is no mod system inside the launcher: the *Mods* page of 1.1.0 only
  shows the dreXmod presets that the setup installed.
- "Repair CD-Keys" is not a launcher function: re-running the community setup (with its NeoEE CD-key task) repairs CD
  keys, and the launcher never touches them.
- The "collect diagnostic data" checkbox is gone because the launcher collects none.
- Windows XP, Vista, 8.0 and Windows 10 1507/1511 are not supported (.NET 4.8); Windows 7 SP1 is supported but not
  tested yet. Upstream's goal was "Windows 98 to 11".
- Settings of earlier test builds (`user.config`) are not taken over. Saved games are exported into a folder; there is
  no zip export or import.

**Status**: version 1.1.0 (`SharedAssemblyInfo.cs`, CHANGELOG 2026-10-07) was released on 2026-10-07 as the tag `v1.1.0`
on the commit 5d256c8. It ships inside the private suite package "Empire Earth Community" 1.1.0; no binaries are published
here. The maintainer released it after session 1 of the laptop test only, which covers WP6-13, WP6-18, WP6-19 (a),
WP6-21 (a) to (d) and (g), WP11-01 to WP11-04 (a), WP11-06, WP11-10, WP12-01 (a), WP12-02, WP12-04, WP13-01, WP13-02,
WP13-06, WP13-07, WP14-01 and WP14-02. He reported only the overall verdict, "passed, the mouse works right after the start
without Alt+Tab" (WP6-21), and no results for single cases; session 2 was not run. Not run on real hardware are WP11-04 (b),
WP11-05, WP11-07 to WP11-09, WP12-01 (b) and (c), WP12-03, WP12-05 to WP12-09, WP13-03 to WP13-05, WP13-08, WP14-03,
WP6-19 (b) to (d), WP6-20 and WP6-21 (e), (f) and (h)
([docs/TEST-PLAN.de.md](docs/TEST-PLAN.de.md), section 4.1); they are to be run before or with 1.1.1, and a problem found there
is fixed in 1.1.1. The French translation review and the German proof-reading are still open
([docs/TRANSLATING.md](docs/TRANSLATING.md#status)). The fixes for 1.1.1 are on `main` and listed under *Unreleased* in the
[CHANGELOG](CHANGELOG.md).

## 🧾 Features

Launcher v2 was built in nine work packages (developed on the branch `v2`, now merged into `main`;
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), section 15; what each one did is in the [CHANGELOG](CHANGELOG.md)). All of them are done; the launcher is version 1.1.0
(one launcher for the four games, the *Graphics* and *Mods* pages, the optional parts for the suite installer "Empire Earth
Community" below, which 1.0.0 added). Session 1 of the test on a real Windows computer ran before the release of 1.1.0;
the next step is session 2, before or with 1.1.1 ([docs/TEST-PLAN.de.md](docs/TEST-PLAN.de.md), section 4.1 lists what
it covers). The UI shows only controls that work ([ADR 0014](docs/adr/0014-only-working-features-in-the-ui.md)): the placeholder
controls of the old mock-up were removed and the features behind them are listed below as planned.

**Available now**

- One list of the four games on the *Play* page (1.1.0): "Empire Earth", "Empire Earth - The Art of Conquest",
  "Neo Empire Earth" and "Neo Empire Earth - The Art of Conquest" (in English, German and French), then the *Play*
  button. A game that is not installed is shown greyed out; your choice is remembered, and it selects the installation
  of that product on every page (the NeoEE player list shows only for NeoEE). The folder you chose is remembered per
  product, so EE and NeoEE can each have their own
  ([ADR 0005](docs/adr/0005-own-settings-file-instead-of-user-config.md) and
  [ADR 0014](docs/adr/0014-only-working-features-in-the-ui.md), amendments of 2026-10-07)
- Play the chosen game of the selected installation: the launcher waits while a setup runs, does
  not start a game twice (and explains how to end a hanging one in the Task Manager), asks before starting the second
  game, keeps the "Installed From" values of the game in step, starts the program through Windows so that its
  compatibility settings apply (also "Run as administrator"), hands the foreground to the window of the game after the
  start and gives it one activation once its window has settled (1.1.0, see the [FAQ](#-faq-and-known-issues): the mouse
  was dead in the menu until the window was switched away and back), shows the file versions of both programs and logs every
  start
- Graphics page (1.1.0): choose the size of the game window from the usual 4:3, 5:4, 16:10 and 16:9 sizes that fit
  your screen, up to 1920x1200 (the game can crash above that; 1920x1200 only on a screen that is at least that tall), with
  a backup of the game settings first; only the two values of the window size are written, never a file, and a repair or
  update with the setup sets the recommended size again. The page tells you when `dgVoodoo.conf` still has the settings of a
  setup before 1.1.0 (problems with the mouse, the lobby or Alt+Tab) and that a repair or update with the
  setup replaces them (and with them any change you made to the file yourself). It also shows which DirectX wrapper the setup installed (native, DirectX 7, DirectX 9, dgVoodoo with its API
  level) and, for dgVoodoo, the keys of `dgVoodoo.conf` that decide the screen mode (read only: the launcher changes neither
  the wrapper nor that file), and tells how to change the wrapper in the setup
  ([ADR 0014](docs/adr/0014-only-working-features-in-the-ui.md), amendment of 2026-10-07)
- Mods page (1.1.0, only for an installation with dreXmod 3): lists the dreXmod presets of each game (the folders of
  `Data\dxm\mods` with the name, last edit and author from their `CREDITS` file and their size; the skeleton `template`
  is hidden unless you ask), marks the active mod and the active lobby theme that `dreXmod.config` names, opens the folder
  and the config (in the program Windows has for `.config`; with none, Windows asks which program to use), and tells how
  to switch a preset by hand. Read only: the launcher changes neither the config nor a preset
  and installs no mod; a setup run resets the choice but keeps presets you made yourself. Whether a mod has an effect in
  multiplayer or ranked games has not been verified
  ([ADR 0014](docs/adr/0014-only-working-features-in-the-ui.md), amendment of 2026-10-07)
- Integrity check of community installations since setup v2: at every start and after a setup, in the background, the
  launcher checks that every file of the setup's list is there and that the program files are unchanged; the *Play*
  page shows the state ("Files: OK", "damaged", "incomplete", ...), the new *Tools* page explains it, lists every
  missing or changed file and runs the full check of all game data on request (with progress and cancel). It only
  reads: it never changes, deletes, restores or downloads a game file, never blocks Play and gives way to a running
  setup (forum: antivirus programs that delete game files)
- Repair advice when a program is missing, files are damaged or an update is available: what to do with the community
  setup (antivirus exception first, same folder and install mode, keep the NeoEE CD-key task), the files concerned, and
  where to get the setup, shown at once and opened in the browser only on a click, without a request to the update API:
  - for an installation that the suite "Empire Earth Community" installed (its record lists the product), the release
    page of the package, https://github.com/DritteRippe/Empire-Earth-Community/releases/latest (1.1.1). This holds also
    when the unpacked folder of the package is gone: the advice is then to download the package again, unpack it and
    run "Empire Earth Community Setup", not the setup of empireearth.eu, which is a different build and would undo the
    fixes of the package. For an available update it says that the package updates only with a new release;
  - for other installations the download page of the product (`https://empireearth.eu/download/ee/` or `/neo/`,
    `https://empireearth.eu/download/` for a foreign installation);
  - the launcher never downloads, starts or elevates the setup itself
- Version check on request (*Play* page: the game; *Tools* page: game and setup) against the update API of the
  community setup, for community installations also of setups up to 1.7.2 (forum: version conflicts in multiplayer).
  "Check for updates" asks only about the game and the setup of the product (EE or NeoEE), not about the package
  "Empire Earth Community", and its result names the product ("Version 1.7.2 of the Empire Earth setup: up to date.").
  For new versions of the package, the button "Open release page" next to it opens the package's release page (1.1.1)
- Maintenance tools on the *Tools* page, none of which runs while a setup or a game runs:
  - Registry cleanup: cleanup of HKCU entries; HKLM entries are only shown, with advice. The launcher offers a key of
    your Windows account from its fixed list only when no installation of its product is found and the game folder it
    names is gone from a present, fixed, local drive, and it exports the selected keys to a `.reg` file in the backup
    folder before it deletes them. Keys for all users get the advice to export and delete them with the Registry
    Editor as administrator; `Software\Sierra` holds the NeoEE CD keys and is shown as "do not delete". When nothing
    can be deleted, the page says "nothing to clean up"
  - WON login reset: moves `_wonkver.pub` and `_wonlogin.ks` of both games (also their VirtualStore copies) into the
    backup folder, so that the game creates new ones at the next login (forum: login errors such as
    `WS_GetCert_InvalidPubKeyBlock`); files of the setup's list are never moved
  - VirtualStore check: for a game below `Program Files` it lists the copies Windows keeps in
    `%LOCALAPPDATA%\VirtualStore`, and warns when installed or program files are used from there instead of the
    game folder (forum: another version with and without administrator rights). A copy of `dgVoodoo.conf` that differs
    from the file in the game folder gets its own hint: the game reads the copy, so a hand edit of the real file has
    no effect (a possible reason why edits "do nothing"). The page then offers "Open VirtualStore folder"; the launcher
    never deletes or changes the copy, close the game and rename or delete it yourself
  - Saved games and scenarios: export of every `.ees` and `.scn` file of both games (also those in the VirtualStore)
    into a new folder you choose; import of single files into Empire Earth or The Art of Conquest where the game
    reads them, with checks (only `.ees` and `.scn`, plain names the game can read, at most 64 MiB) and a question
    before a file is replaced (the old one is kept in the backup folder). There is no zip export or import
  - Player names: a warning for lobby profile and player names with characters outside plain ASCII (forum: saved
    games that cannot be loaded, crashes) and the hint that the host of a multiplayer game needs its ports forwarded
    (33334 and 33336 TCP+UDP, 33335 TCP by default)
  - "Open backup folder"; the backup folder contains login data after a WON reset, never pass it on
  - Network check, only when you click "Check network" (or "Why? Check the network" below a player list that is "not
    available" on the *Play* page): the network adapters of this computer (VPN, Hamachi and other virtual adapters
    marked), the name lookup of the NeoEE servers, the update server, the NeoEE status server, `NeoEE.cfg` and
    `WONLobby.cfg` of both game folders (only read: RIP hosting, ports, `CDKeyCheck`) with the port forwarding table
    for hosting (33334 and 33336 TCP+UDP, 33335 TCP by default), and `upnp_info.txt`. The result says what is wrong,
    for example "Probably a server outage, not your computer" when the names resolve and the update server answers
    but the NeoEE server does not, and gives hints for offline computers, several or virtual adapters, Carrier-grade
    NAT, DS-Lite, a second router, RIP hosting switched off and `CDKeyCheck` (forum: lobby and hosting problems)
  - Diagnostics report: "Copy report" or "Save report..." gives one English text with what the launcher found
    (launcher and Windows version, screen and display adapter, installations, file versions, DirectX wrapper,
    integrity, game defaults and hints, VirtualStore, whether the CD keys exist, the network check) for a forum post.
    It contains no CD keys, login data, player names, MAC or public IP address and no user or computer name, and the
    launcher never sends it
- One launcher at a time (a second start brings the window of the running launcher to the front and ends without a
  message; started with `--product`, see below, it also hands the product to the running launcher)
- NeoEE online player list, with the lobby profiles and friends of the game folder (also when the game keeps them
  in the VirtualStore). It is asked only while the selected installation is NeoEE: with Empire Earth, a retail or GOG
  installation, or none, the launcher sends nothing to the status server
- Parts for the suite installer "Empire Earth Community" (launcher 1.0.0; the setup repository's suite installs the
  launcher and runs the EE and NeoEE setups in one go; all of it is optional, the launcher works as before without it;
  `docs/CONTRACT.md` revision 4):
  - The suite 1.1.0 creates one icon "Empire Earth Community" that starts the launcher without an argument; the Play
    page then shows the game you chose last
  - `Empire Earth Launcher.exe --product=EE` or `--product=NeoEE` (the argument of the shortcuts of suite 1.0.0 and of
    your own shortcuts) preselects the game of that product in the list for this session; the folder you chose for
    that product wins, nothing is saved, and anything else (another value, a product that is not installed, another
    argument) is ignored and written to `log.txt`
  - If the launcher is already running, a second start brings its window to the front, with `--product` it also
    selects the product there (not while a game is starting)
  - The suite counts as a running setup like the EE and NeoEE setups: while it runs, even in the moment between the two
    product setups, the launcher starts no game and changes nothing
  - If the suite installed a product and the folder you unpacked it to is still there, the repair advice says to run
    "Empire Earth Community Setup" again from that folder (with a button that opens the folder, never the program); the
    release page of the package is the second option, and without the folder it is the only download (launcher 1.1.1;
    before, the advice named the download page of the product setup). The launcher only reads the suite's record
    (`HKLM\SOFTWARE\Empire Earth Community\Suite`) for this
- Detection of every Empire Earth installation: community setups (current and up to 1.7.2, admin, user and
  portable), retail, GOG and copies with their real folders; the *Launcher* page lists them with their state
  (a missing `Empire Earth.exe` shows as damaged) and lets the player choose one or another folder
- Game settings for your Windows account: the recommended defaults of the community setup for a second Windows
  account or after a manual install, one question before your display settings are replaced, "Apply recommended
  display", a reset with a `.reg` backup that restores your previous settings on a double-click, hints when settings
  do not fit together (bit depths, 16 bit, renderer, window larger than the screen at 150 % scaling, screen lower
  than 768 pixels, a folder name the game cannot read), and the compatibility options for your account from Windows 8
  on (*Settings* page; the *Play* page shows a hint bar)
- Themes (Krypton palette files)
- English, German and French user interface, following Windows or chosen on the *Launcher* page
  ([docs/TRANSLATING.md](docs/TRANSLATING.md))
- Mod creator for `.eem` mod packages
- Windows 7 SP1, 8.1, 10 and 11 with the .NET Framework 4.8 (see [Requirements](#requirements))

**Planned, not in v2** (their placeholders were removed from the UI)

- Full mod system in the launcher: installing mods (`.eem`), "mods in use", `.eem` file association (the *Mods* page of
  1.1.0 only lists the dreXmod presets)
- dreXmod switch (choosing the active preset in the launcher: planned for 1.2 with an allow-list), Discord presence, HD
  textures, skip intro, game font customizer, lobby customizer
- Game languages (voices, lobby, campaigns) and the online ranking
- DirectX wrapper switch (DX 9/11/12): the setup's custom installation switches the wrapper, the launcher must not
  change game files ([ADR 0014](docs/adr/0014-only-working-features-in-the-ui.md)); the *Graphics* page and the
  diagnostics report show which one is installed
- Editing the screen mode in `dgVoodoo.conf` (and the dreXmod preset in `dreXmod.config`) from the launcher: planned for
  1.2 with an allow-list of files and keys; the launcher of 1.1.0 changes neither file
- Resolution above 1920x1200 (the *Graphics* page stops at the limit of contract 3.3)
- Auto-compatibility detector ("My game is working", "Auto-detect") and auto-update
- Writing the GPU driver version into the log (the diagnostics report names the display adapter)
- Checking the forwarded ports from outside (needs a service on the server; the launcher shows the forwarding table)

**Privacy, no telemetry**: the old checkbox "Allow us to collect diagnostic data" is gone. The launcher collects no
usage or diagnostic data. Its connections are the request for the NeoEE player list and, only when you ask for it
(version check, network check), an HTTPS request to `api.empireearth.eu` that sends nothing but the AppId of the
installation, the kind of the question (game or setup) and, for the version check, the version, and the name lookups of the network check; it asks no service
for your public address. The download page of the community setup (`empireearth.eu/download/ee/`, `/neo/` or
`/download/`) and the release page of the package "Empire Earth Community" on GitHub open in your browser only when you
click their button; the launcher sends no request for them. The
diagnostics report is
made only when you click, stays on your computer until you paste or send it yourself, and replaces your user and
computer name, public addresses, MAC addresses and player names; read it before you post it. CD keys are repaired by
re-running the community setup; the launcher never touches them (the report only says whether they exist).

## ❓ FAQ and known issues

What the launcher can do about a problem of the game itself is limited: it starts the game, shows what it finds and never
changes a game program. The NeoEE programs and their CD-key registration stay untouched, and the launcher of 1.1.0 changes
neither `dgVoodoo.conf` nor `dreXmod.config` (the *Graphics* and *Mods* pages only show them). The answers below say what is
known, what is only a probable cause and what has not been verified.

**The mouse does not work in the main menu until I switch away and back (Alt+Tab).**
Empire Earth takes its mouse and keyboard through DirectInput only when its window is activated. With the DirectX wrapper
dgVoodoo of the community setup no activation reaches the game after it has created these devices, unless the start
changed the display mode; on a screen of the game's own size it does not. Since 1.1.0 the launcher sends the main window
of a game it started **one** activation (`WM_ACTIVATE`) as soon as that window has been in front, unchanged, for five
seconds, at the latest three minutes after the start. That gives the game its mouse without Alt+Tab, so that a
click skips the intro videos; confirmed on 2026-10-07 on a laptop with Windows 11 (WP6-21; a real Alt+Tab also brings
the activation of the whole application and a real change of the foreground window, which one posted message cannot).
So: **start the game through the launcher** (the desktop icon "Empire Earth Community" of the suite opens it) and keep the
launcher open until the main menu shows; closing it earlier cancels the signal. A start of `Empire Earth.exe` or
`EE-AOC.exe` without the launcher still needs one Alt+Tab, out and back.
The launcher sends nothing while the lobby or the launcher itself is in front, nothing at all once you have switched to another
program after the game had been in front (Windows activates the game when you return), never minimizes, moves or closes
a window and never ends a process; `log.txt` says what it decided (`Activation signal ...`). A game that runs as
administrator refuses the message (`activation signal failed ... error 5`): then use Alt+Tab once.
Before the signal the launcher still hands the foreground to the game window after the start when the launcher itself
holds it (it never takes it from another program) and watches the window for at least a minute, read-only; those lines
help when a report comes in:

- right before the start it allows the game to take the foreground (`AllowSetForegroundWindow`); the start itself stays a
  start through the Windows shell;
- after the start it looks every 100 ms (for 60 s at most) for the first visible window of the game process and, if the
  launcher or the game owns the foreground, brings that window to the front (`SetForegroundWindow`); it looks again after
  about 2 s (a wrapper such as dgVoodoo may switch the mode late) and hands the window over again, at most three times,
  while the launcher has the foreground back;
- if another program is in front (you clicked into a browser while the game loads) the launcher changes nothing and
  never takes the foreground away from it.

If the mouse is dead although the log says `activation signal sent`, note whether Alt+Tab (or a click on the taskbar button
of the game) revives it, and send the lines of `log.txt` from `Game started:` on (see "What do I send with a report?").
The next step in that case is to send the application activation (`WM_ACTIVATEAPP`) as well; that would be a decision of a
later version ([ADR 0010](docs/adr/0010-game-start-and-mutex-probing.md), amendment of A1b).
A game with the compatibility setting "Run as administrator" may also refuse the foreground from the launcher, which does not
run as administrator (`SetForegroundWindow was refused` in the log); the launcher tries three more times and then gives up
without changing anything else.

**The game minimizes itself when another window or a notification appears.**
This is a behavior of the game program, not of the launcher or of a DirectX wrapper: `Empire Earth.exe` reacts to
the loss of the application's activation by minimizing its own window (`CloseWindow`), and an internal flag decides when
it does not (what sets that flag is not known). Any window that takes the foreground triggers it: the pop-up of a
browser, a chat or tray program, the software of a graphics card, mouse or keyboard maker, a security program, an updater
or an installer. A toast notification of Windows normally does not take the activation, a window that a program opens
does. The community forum has described the same for years, mostly while the game is loading. The NeoEE programs behave
alike. The launcher cannot change this: it never modifies game programs, the NeoEE programs are off limits, and a changed
program would be reported as damaged by the integrity check.

What helps, without any promise that the game never minimizes:

- Close tray programs that open windows by themselves (updaters, chat clients, vendor tools) before you play, and do not
  click into other programs while the game loads, the most sensitive moment.
- **Do not disturb**: in Windows 11 *Settings > System > Notifications > Do not disturb* (German: *Nicht stören*; in
  Windows 10 *Focus assist*, German *Fokus-Assistent*) keeps notification pop-ups away for the play session. Whether it is
  enough to keep the game from minimizing is not verified (test plan WP6-19). The automatic rules of Windows for games and
  full-screen programs only apply when Windows classifies the game as full screen; whether it does so with a DirectX
  wrapper is not known.
- If the game was minimized, restore it with its taskbar button or Alt+Tab. The aim is that it comes back reliably in full
  size and with a working mouse, not that it is never minimized. If it comes back too small, black, lagging or with a
  doubled cursor, note the Windows version, the display adapter and the wrapper of the *Graphics* page: the reports so far
  point at the display driver and the wrapper, and the dgVoodoo version and settings of the setup are tested separately;
  the launcher does not touch them.
- A second start of the launcher (the icon of the suite or a shortcut with `--product`) brings the window of the running
  launcher to the front, which takes the foreground from a running game and so minimizes it. Do not start the launcher
  again while a game runs.

**I changed `dgVoodoo.conf` by hand and nothing happened.**
A game below `Program Files` that Windows virtualizes reads a copy in `%LOCALAPPDATA%\VirtualStore\<game folder>` instead of
the file in the game folder, if one exists. The *Tools* page ("VirtualStore") warns when such a copy of `dgVoodoo.conf`
differs from the real file and offers "Open VirtualStore folder"; the launcher never deletes the copy: close the game and
rename or delete it yourself, then edit the file in the game folder again. The *Graphics* page shows `OutputAPI` and the
screen-mode keys of the file the game reads (the copy first). To see whether the file is read at all, set
`dgVoodooWatermark = true` in it: the watermark must appear in the game.

**Is the 2 GB limit a problem? Does the launcher offer a 4 GB patch?**
The game programs are 32-bit programs without the "large address aware" flag, so every game process can use 2 GB of
address space, whatever the RAM of the computer is; with the flag it would be 4 GB. None of the community's reports we
know describes running out of memory, and the launcher makes no change here: it never modifies game programs, the NeoEE
programs are off limits, and a patched program would lose the signature of the community certificate and be reported as
damaged by the integrity check (every run of the setup would also put the original back).
To measure what your games really use, read the peak values while a big game is still running (late in a long game with
many players), for example after switching to PowerShell with Alt+Tab (the game minimizes itself, see above) and back
with its taskbar button; the numbers vanish when the game ends:

```powershell
Get-Process 'Empire Earth', 'EE-AOC' -ErrorAction SilentlyContinue |
    Select-Object Name,
        @{n='PeakMB';e={[int]($_.PeakPagefileUsage/1MB)}},
        @{n='PeakAddressSpaceMB';e={[int]($_.PeakVirtualMemorySize64/1MB)}}
```

`PeakMB` is the most memory the game had committed, `PeakAddressSpaceMB` the most address space it used (the 2 GB limit is
about this number). A `PeakMB` below about 1200 MB makes the limit an unlikely cause, but the address space decides: if `PeakAddressSpaceMB` comes near
2000, send both numbers with the game, whether it was EE, AoC or NeoEE, and the number of players (test plan WP6-20).

**What do I send with a report?**

- The diagnostics report: *Tools* page, "Copy report" or "Save report..." (launcher and Windows version, screen and display
  adapter, installations, file versions, DirectX wrapper, integrity, game defaults and hints, VirtualStore, the network
  check). It contains no CD keys, login data, player names, MAC or public IP address and no user or computer name; read it
  before you post it, the launcher never sends it.
- The lines of `%LOCALAPPDATA%\Empire Earth Launcher\log.txt` around the start (every launcher start writes into this one
  file, so the lines of a second start that hands over to the running launcher are there too). `Game started: ..., pid <number>` is the
  start; the line about the hand-over of the foreground follows: `Game window 0x... of Empire Earth.exe (pid <number>)
  brought to the foreground after <N> ms (the foreground was pid <number> (the launcher)).` is the normal case.
  `... not brought to the foreground: skipped, user switched to pid <number>.` means another program was in front,
  `SetForegroundWindow was refused` that Windows said no, `No window of ... within 60 s` that the game showed no window of
  that process (NeoEE may create it elsewhere), and `giving up` that the launcher kept the foreground after three tries.
  `... already in the foreground, nothing to do.` means the game was in front when its window appeared. The window is the
  main window of the game (class `SSSI Empire Earth`), never the small start-up window `Loading Game Window`. For at least
  one minute after that the launcher watches and writes `Watch t+<seconds> s: ...` lines: which window is in front and the
  rectangle and styles of the game window, and each change with the time since the start. That measures when a
  DirectX wrapper such as dgVoodoo changes the window; the watch itself changes nothing (test plan WP6-18). The lines
  `Activation signal for ...: armed`, `... activation signal waits: ...`, `... activation signal sent: ...` (or `failed`, `not
  sent`) tell what the activation signal decided (test plan WP6-21); the watch lasts until it is decided, at most three
  minutes after the start.
- What you saw: whether the mouse worked without minimizing, which window or notification took the focus when the game
  minimized itself, whether the game comes back in full size, the Windows version and the display scaling.

## 🌐 Download

The launcher is not offered as a download of its own: it ships inside the package **Empire Earth Community**, which
installs the games, the community fixes and this launcher in one go. Get the package from its release page:
https://github.com/DritteRippe/Empire-Earth-Community/releases/latest. The releases of this repository are tags with
release notes; they have no binaries.

To try a change before a release, every green CI run keeps a test build (see *Test builds* under [Dev](#dev)). The
launcher needs the .NET Framework 4.8 (see [Requirements](#requirements)).

## Requirements

The launcher and the mod creator need the **.NET Framework 4.8** and run on these Windows versions:

| Windows | .NET Framework 4.8 | Status |
|---|---|---|
| Windows 11, Windows 10 version 1903 or later | built in | supported |
| Windows 10 versions 1607 to 1809 | install it once | supported |
| Windows 8.1 | install it once | supported |
| Windows 7 SP1 | install it once | supported, **not tested yet** |
| Windows 8.0, Windows 10 versions 1507 and 1511 | not available | **not supported**: update Windows (Windows 8.1 is a free update of 8.0) |
| Windows Vista, XP and older | not available | not supported |

- When the .NET Framework 4.8 is missing, Windows itself shows a message with a download link when the
  launcher starts. The installer is at https://dotnet.microsoft.com/download/dotnet-framework/net48 and needs
  administrator rights. To check what is installed, run in PowerShell
  `(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full').Release`: 528040 or higher
  means 4.8 or newer.
- Windows 7 SP1: install the current Windows updates first. For the offline installer, Microsoft requires the
  "Microsoft Root Certificate Authority 2011" in the computer's trusted root certificates
  ([Install the .NET Framework on Windows 7 SP1](https://learn.microsoft.com/en-us/previous-versions/dotnet/framework/install/on-windows-7)).
  Windows 7 is no longer supported by Microsoft and the launcher has not been tested on it; reports are welcome.
- The launcher does not ask for administrator rights. Its window is not DPI-aware: on screens with a scaling
  above 100 % Windows enlarges it (it may look slightly blurry). The window can be resized freely (its smallest size is
  the size it opens with) and the content of the pages grows with it.

## Dev
You just need to clone the repo and open `Empire-Earth.sln` with Visual Studio **2019** or newer (with the workload ".NET desktop development", which contains the .NET Framework 4.8 targeting pack)\
Some very critical parts of the Launcher can be censored like WON and NeoEE related important operation but most of the reverse WON C# implementation is available 💪

### Building

All projects target the **.NET Framework 4.8** and are pinned to **C# 8.0** (`LangVersion` in every `.csproj`),
so every contributor compiles the same language with Visual Studio 2019 or newer, MSBuild or Mono. Do not use
newer language features or APIs that do not exist in .NET Framework 4.8. `async`/`await`, `Task.Run` and
`HttpClient` are available; how the launcher uses them is described in
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#5-threading). The projects are classic project files with
`packages.config` (no SDK-style projects, no `Directory.Build.props`, see
[ADR 0002](docs/adr/0002-keep-classic-project-files-and-packages-config.md)); the settings they share
(`TargetFrameworkVersion` v4.8, `LangVersion` 8.0, `Deterministic`, AnyCPU with `Prefer32Bit` false for the
executables, the `lib` folders of the NuGet packages, the 4.8 runtime in `App.config`) are checked by the
architecture tests in `Empire-Earth-Launcher.Tests/Architecture/`. Change them in every project at once.

**Visual Studio (Windows)**: open `Empire-Earth.sln` and build. NuGet restores the packages into the root
`packages/` folder automatically.

**Command line (Windows, Developer PowerShell)**:

```powershell
nuget restore Empire-Earth.sln
msbuild Empire-Earth.sln /p:Configuration=Release
```

The output is in `Empire Earth Launcher\bin\Release\` and `Empire-Earth-Mod\Empire-Earth-Mod\bin\Release\`; the
launcher needs the libraries next to it (`Empire_Earth_Launcher_Core.dll`, `Empire_Earth_WON.dll`,
`Krypton.Toolkit.dll`), so copy the whole folder. If
MSBuild reports `MSB3644` (reference assemblies for `.NETFramework,Version=v4.8` not found, e.g. with the Build
Tools without the 4.8 targeting pack), build against Microsoft's reference assemblies from NuGet, exactly like
the CI does:

```powershell
nuget install Microsoft.NETFramework.ReferenceAssemblies.net48 -Version 1.0.3 -OutputDirectory $env:TEMP\refasm
$ref = "$env:TEMP\refasm\Microsoft.NETFramework.ReferenceAssemblies.net48.1.0.3\build"
msbuild Empire-Earth.sln /p:Configuration=Release "/p:TargetFrameworkRootPath=$ref" "/p:FrameworkPathOverride=$ref\.NETFramework\v4.8"
```

**Linux/macOS (Mono)**: useful as a compile check only, the WinForms/Krypton UI is meant to run on Windows.
With a Mono installation from mono-project.com (which ships `msbuild` and a Roslyn `csc`):

```sh
nuget restore Empire-Earth.sln        # or: mono nuget.exe restore Empire-Earth.sln
msbuild Empire-Earth.sln
```

Distribution packages that only ship `xbuild` and the old `mcs` compiler cannot compile C# 8. Point `xbuild`
at a Roslyn compiler from NuGet instead:

```sh
mono nuget.exe install Microsoft.Net.Compilers -Version 4.2.0 -OutputDirectory ~/.local/roslyn
printf '#!/bin/sh\nexec mono "%s/tools/csc.exe" "$@"\n' ~/.local/roslyn/Microsoft.Net.Compilers.4.2.0 \
  > ~/.local/roslyn/Microsoft.Net.Compilers.4.2.0/csc-mono
chmod +x ~/.local/roslyn/Microsoft.Net.Compilers.4.2.0/csc-mono
mono nuget.exe restore Empire-Earth.sln
xbuild Empire-Earth.sln /p:CscToolPath=$HOME/.local/roslyn/Microsoft.Net.Compilers.4.2.0 /p:CscToolExe=csc-mono
```

`xbuild` compiles against Mono's `4.8-api` reference assemblies and warns "TargetFrameworkVersion 'v4.8' not
supported by this toolset" (its own version list ends earlier; the warning is expected). It ignores
`ApplicationManifest` (the executables get the compiler's default manifest) and `FrameworkPathOverride`, so
its output is for compiling and running the tests, not for distribution; ship builds made with MSBuild.

**Tests**: `Empire-Earth-Launcher.Tests` is a console program built with the solution. It runs its NUnit
tests with NUnitLite (NUnit 3.14, the last NUnit 3 release, from its `net45` build; NUnit 4 would change the
classic assertions, see [ADR 0012](docs/adr/0012-test-strategy.md)) and exits with 0 only when every test
passed (otherwise with the number of failed tests). Run it after building:

```sh
Empire-Earth-Launcher.Tests\bin\Release\Empire-Earth-Launcher.Tests.exe          # Windows
mono Empire-Earth-Launcher.Tests/bin/Debug/Empire-Earth-Launcher.Tests.exe       # Mono
```

NUnitLite options can be passed, e.g. `--where "class =~ LobbyPersistentData"` to run some tests only or
`--result=TestResult.xml` to write an NUnit 3 result file (by default no result file is written). The tests
cover:

- the core library (`Core/`): the contract names against `docs/CONTRACT.md`, the Windows path rules
  (`WinPath`, including the manifest paths of contract 2.2), the file system and registry abstractions with their
  in-memory fakes, the canonical form of registry keys and the registry write policy, the mutex probe and the
  mutation guard, `settings.json` (including damaged, unreadable and newer files), the log format and trimming,
  the launcher's file locations, the lobby profiles, and the discovery of the installations (`Installations/`: the
  `install.ini` parser, the readers of the registry records, uninstall keys and "Installed From" values, a table
  test with a case for every rule of contract 1.4 and 1.5, real folders of foreign installations, every form of
  the user choice, one log line per dropped candidate, no write, no duplicates on 32-bit Windows, a discovery that
  does not block its caller, the VirtualStore paths, and the cases of the old game folder tests under their
  names), the `.reg` writer (golden files of every value type in `Core/Backup/Golden/`) and the backup folder, and the
  game settings (`GameSettings/`: the value table against contract 3.2, the computed values, the marker, the first
  run, the display question, the reset and the restore of its backup by import, the consistency checks at 100 % and
  150 % scaling, the hidden hints, the compatibility options and the launcher's write policy, every writing action
  blocked by a running setup and game), Play (`Play/`: the order of `GameStarter` with fakes and with the real game
  settings, every refusal and start error as a result, the setup watcher with a fake clock, the single instance, the
  file versions), the integrity check (`Integrity/`: the manifest reader with BOM, CRLF, uppercase hex, the binary
  marker and every unsafe path, the file classes against the table of contract 2.4, the state table of 2.5 with the
  uninstall key rule, NeoEE wording, two products in one folder, the counted cost of the quick and full check, a
  setup that starts during a check, and that the check never writes), the repair hand-off (`Repair/`: the advice, the
  URL policy with the 13 cases of the setup's `TestIsAllowedUpdateUrl` and the launcher's own, the download locator
  with every fallback, the version check), the maintenance tools (`Maintenance/`: the cleanup list against the table
  of ARCHITECTURE 4.6 with the evidence of every entry, the advice of every entry through the canonical form of the
  write policy, the cleanup with backup first and nothing deleted when the backup fails, the CD keys unchanged, the
  WON login reset, the VirtualStore check, the export and import of saved games and the name check; every writing
  action blocked by a running setup and game; moving, exporting and importing also with real files in a temporary
  folder) and the file backup (`Backup/`), the network check and the report (`Diagnostics/`: the readers of
  `NeoEE.cfg`, `WONLobby.cfg` and `upnp_info.txt`, the address classes, every combination of the outage verdict, the
  hints and the port table with a fake network, the privacy of the log lines, and the report as a golden file of a
  synthetic computer plus a negative test with a computer full of personal data and CD keys), the byte samples of
  `docs/contract-samples/` read by the launcher's readers (`Contract/`), the HTTPS client's settings and the network
  information (`Platform/`), the online player list poller (`Lobby/`) and the start information of the shell starter;
- the UI helpers of the launcher (`Launcher/`): `UiOperation`, the "unexpected error" message, the logging of
  unobserved task exceptions, the texts chosen for results (`Texts`), the UI language applied at start, the
  installation service (also while a setup runs), the models of the game settings pages (`GameSettingsModel`), of
  the Play page (`PlayModel`), of the integrity check (`IntegrityModel`: the quick check after every search and after a
  setup, never while one runs, cancel), of the update API (`UpdateModel`), of the maintenance tools
  (`MaintenanceModel`, and what the registry cleanup shows: `CleanupView`) and of the network check and the report
  (`DiagnosticsModel`: only on request, saving never into an installation), and the message of a second launcher;
- the WON lobby file parser, the NeoEE protocol framing, reply parsing and request deadline (`Won/`), and the mod
  library (`Mod/`: product folders, file types, versions, the working directory of the mod creator, `.eem`
  export/import including damaged and crafted archives, the paths of mod files, the size limit of the archives, and the
  local modifications of the vendored ZipStorer);
- architecture rules (`Architecture/`): the shared build settings of every project, the core's dependencies (no
  WinForms, `System.Drawing` or Krypton; only the BCL and the WON library), a table test that the registry
  write policy refuses `Software\Sierra\CDKeys`, the install records and the uninstall keys under every alias
  (`WOW6432Node`, registry VirtualStore, `/`, case) and every ancestor, for every operation, and the UI texts:
  the removed placeholder controls stay removed (`PlaceholderControlsTests`), every designer text is set again
  in `ApplyTexts()` (`ApplyTextsTests`), and English, German and French have the same texts, placeholders and
  built satellite assemblies (`ResourceParityTests`); and the test plan (`TestPlanTests`): unique case IDs, cases for
  every work package, every case named in the documents exists, the mapping of the requirements and forum test cases
  is complete with nothing left open, and every launcher item of the contract checklist has its cases; the ticked
  checklist of ARCHITECTURE 15 names only existing test classes (`ContractChecklistTests`); the launcher contacts only
  its three destinations (`NetworkDestinationTests`); the tests themselves touch no registry, network or launcher file
  (`TestIsolationTests`), and only the shell starter starts programs, never without the shell or elevated, and
  nothing ends a process (`ProcessRulesTests`); no source overrides the certificate check
  (`NoCertificateOverrideTests`), names an old or explicit TLS version or sets it outside `Program`
  (`TlsSettingTests`), every SHA-256 in the fixtures and the contract samples is the hash of a synthetic text
  (`FixtureProvenanceTests`), only the mod archive reader extracts ZIP entries (`ZipStorerUseTests`), and every action
  of the CI workflow is pinned to the commit of a release (`ProjectConventionsTests`).

Architecture tests read the project files, `packages.config`, `App.config`, `app.manifest`, the core's sources,
the launcher's designer files, code and `.resx` files, `docs/CONTRACT.md`, ADR 0014, the test plan and the other
documents, the CI workflow and the built satellite assemblies (the tests that read the documents carry the category
`SourceTree`); they find the source tree by walking up from the test program to
`Empire-Earth.sln`, so run the test program from its build folder inside the repository; a copy outside it (the
`Tests\` folder of the laptop package) runs the others with `--where "cat != SourceTree"`. The tests use fakes
(`Fakes/`: in-memory registry with both HKLM views and a 32-bit Windows mode, in-memory file system with Windows
path rules, mutex probe and owner, clock, logger, system information with Windows version, Wine, screen and code
page, process starter, process list, file versions, an HTTPS client that answers from a table (and, in `HttpsClientTests`,
a handler that answers the real client from memory, so no request leaves the test), network adapters and
name lookups from a table; `TestSupport/` also has `MappedFileSystem`, the real file system behind a drive letter that
stands for a temporary folder) and
only write below the temporary folder; they never contact a server, never touch the real registry or
`%LOCALAPPDATA%` and never show UI. Path logic is `WinPath` string logic, so the tests also run under Mono. The tests
of the category `WinForms` create controls and pages without showing them and paint them into a bitmap (the wrapping
labels after the palette disposed its fonts); under Mono they need a display (`xvfb-run -a mono ...`, without one they
are ignored), and the pages whose Krypton controls call Windows libraries, as well as the check of `KryptonWrapLabel`
on .NET Framework, run only on Windows (CI, `Tests\` of the laptop package). `PageLayoutTests` check the geometry of the
six pages (no overlap, nothing outside its page, no cut-off text, content that grows with the page) at four window sizes,
in English, German and French, with the system font and a 50 % larger one; the *Game settings*, *Graphics* and *Mods* pages are
driven through their real models in every state ([ADR 0012](docs/adr/0012-test-strategy.md), amendment of 2026-10-06). `PageScreenshotTests`
write PNG files of the pages when `EE_LAUNCHER_PAGE_PNG_DIR` names a folder (the CI build uploads them as the artifact
`page-pictures`). The core, the launcher and the WON library make their internal helpers
visible to the test assembly (`InternalsVisibleTo`).

**Real machine**: `Empire-Earth-Launcher.RealMachineTests` is a second NUnitLite program, for the end-to-end workflow of
the setup repository, which installs the real setups on a GitHub-hosted Windows runner and throws the runner away
afterwards ([ADR 0012](docs/adr/0012-test-strategy.md), amendment of the CI end-to-end test). After each step of a
scenario it runs the launcher core against the real installation: the discovery (contract 1.4), the quick and the full
integrity check (2.5), the status of the defaults and the consistency findings (3.3, 3.5, 3.7), on request the defaults
of the launcher start, a second start and the reset for the runner's account (3.4 to 3.6), and last the machine state.
Its fixtures are in the category `RealMachine` and explicit: a run without a filter (CI of this repository, the verify
script) runs only its self-tests (category `SelfTest`, the same checks on the in-memory fakes) and counts the fixtures as
skipped. Selected with `--where "cat == RealMachine"` they are still ignored unless `EE_LAUNCHER_REAL_MACHINE_TESTS=1`,
and they fail with that switch on a computer that is not Windows or not a GitHub-hosted runner (`RUNNER_ENVIRONMENT` is
not `github-hosted`), because the defaults checks write the game settings of the current Windows account. No project
references the program, so the `Tests\` folder of the laptop package never contains it. The setup workflow runs a fixed
commit of this repository (`LAUNCHER_COMMIT` in its `e2e-realdata.yml`), never the tip of a branch, and refuses a commit
that is not on the branch it names (`LAUNCHER_BRANCH`, today `main`): the program runs as administrator next to the game
data. A change of the checks therefore reaches that workflow only when the setup repository moves the pin, and that branch
must never be rewritten (no rebase, no force push), or the pinned commit stops being on it.

```powershell
$env:EE_LAUNCHER_REAL_MACHINE_TESTS = '1'
$step = "$env:RUNNER_TEMP\e2e\A\installed"      # the work folder of the step; no ';' in the paths
Empire-Earth-Launcher.RealMachineTests\bin\Release\Empire-Earth-Launcher.RealMachineTests.exe --where "cat == RealMachine" `
  "--params=expect=$step\expect.json" "--params=work=$step" "--result=$env:RUNNER_TEMP\e2e-report\A-installed.xml"
Empire-Earth-Launcher.RealMachineTests\bin\Release\Empire-Earth-Launcher.RealMachineTests.exe pick-targets `
  --root "C:\Program Files (x86)\Empire Earth" --product EE
```

The exit code is the number of failed tests (0: all passed); `pick-targets` only reads the manifest of an installation
and prints one `code`, `data` and `mutable` file of its EE folder as JSON, for the damage tests of the workflow. The
expectation file (UTF-8 JSON, schema 1) says what one step expects; only what it names is checked, and an unknown member
or name is an error. An example (after a setup v2 for all users, by the account that runs the checks):

```json
{
  "schema": 1,
  "scenario": "A",
  "step": "installed",
  "selectedRoot": "C:\\Program Files (x86)\\Empire Earth",
  "installations": [
    {
      "product": "EE",
      "root": "C:\\Program Files (x86)\\Empire Earth",
      "kind": "Community",
      "mode": "Admin",
      "appId": "4C0B46D8-E7EB-4B95-97D4-A578D9B914C6",
      "contractVersion": 1,
      "hasArtOfConquest": true,
      "state": "Ok",
      "missingPrograms": [],
      "sources": ["RegistryRecord", "UninstallKey", "InstalledFrom"],
      "integrity": {
        "quick": { "state": "Ok", "findings": [], "offersRepair": false },
        "full": { "state": "Ok", "findings": [] }
      },
      "defaultsStatus": { "EE": "Applied", "AoC": "Applied" },
      "consistency": { "expected": [], "allowed": ["ScreenTooLow", "WindowLargerThanScreen", "WindowFitsOnlyWithHighDpiAware"] },
      "defaultsAtStart": { "EE": "None", "AoC": "None" },
      "installedFromAtStart": { "EE": "Present", "AoC": "Present" }
    }
  ],
  "defaults": {
    "recordSetupValuesTo": "D:\\a\\_temp\\e2e\\A\\installed\\setup-values-EE.json",
    "expectNoWrites": true
  }
}
```

- File: `schema` (1, required), `scenario` and `step` (free text), `userChoice` (the folder chosen in the launcher),
  `exactInstallations` (default `true`: no other installation is found), `selectedRoot`, `watchRoots` (further folders
  whose files must stay as they are, e.g. the root of an installation that was just uninstalled), `installations`
  (required, may be empty) and `defaults`.
- Installation: `product` (`EE`, `NeoEE`) and `root` (required); `kind`, `mode`, `appId` (without braces),
  `contractVersion`, `hasArtOfConquest`, `state`, `missingPrograms` (`EE`, `AoC`), `sources` (exactly) or
  `sourcesInclude` (at least), `otherProductInRoot`, `gameVersion`, `setupVersion`; `integrity` with `quick` and `full`,
  each with `state` (required), `unknownReason`, `cancelReason`, `findings` (exactly these `path` of the manifest and
  `kind`) and `offersRepair`; `defaultsStatus` per game (before anything is applied); `consistency` with the codes that
  must appear (`expected`) and may appear (`allowed`, the screen of the runner is not known); with `defaults` also
  `defaultsAtStart` and `installedFromAtStart` per game. Names are those of the core (`Community`, `RegistryRecord`,
  `HashDiffers`, `FirstRun`, ...).
- `defaults` (only in a step that applies the defaults of the launcher start): `recordSetupValuesTo` (saves the values
  the setup wrote, a file below the work folder), `expectNoWrites`, `expectRecommendedValues`,
  `compareWithSetupValuesFrom` (a file an earlier step saved) with `allowedDifferences` (setting names, `Marker`,
  `GpuPreference`), `secondStartChangesNothing` (default `true`) and `reset`.

Every reading check gets read-only wrappers of the real registry and file system; the defaults write through the
launcher's write policy, and only the values of contract 3 of the installations' games in HKCU are accepted; backups and
saved values go below the work folder only. The last check compares the CD keys of every view (`Software\Sierra\CDKeys`,
reported without any name or value), the install records, the uninstall keys of Inno Setup, the compatibility layers,
the game settings keys in HKLM and every file below the roots with their state before the first check. The log of the
core goes to `core.log` in the work folder, never to the console, because it holds the hashes of the findings; the
console output and the result file hold no hash (every message is redacted) and no game data.

**Continuous integration**: `.github/workflows/build.yml` restores and builds the solution in Release on
`windows-latest` for every push to `main` and every pull request, then runs every `*Tests.exe` it finds in
the `bin/Release` folders (a test program reports failure through a non-zero exit code; finding no test
program fails the build): the unit tests and the self-tests of the real-machine checks, whose `RealMachine` fixtures
stay skipped there. The NUnit result files are kept as the `test-results` artifact. Dependabot (`.github/dependabot.yml`)
proposes a monthly pull request for the GitHub Actions the workflow uses, all of them in one pull request; NuGet packages
stay pinned on purpose. Every action in `build.yml` is pinned to the full commit SHA of a release with the version as a
comment (`uses: owner/repo@<SHA> # vX.Y.Z`), the form Dependabot updates, and an architecture test
(`ProjectConventionsTests`) fails on an action that is not pinned this way; the checkout keeps no credentials. On a fork,
version updates must be enabled once under *Insights* > *Dependency graph* > *Dependabot*.

**Test builds**: when all tests pass, the CI run also keeps the Release output of both applications for 30 days,
as the artifacts `Empire-Earth-Launcher-testbuild` and `Empire-Earth-Mod-Creator-testbuild` (open the run under
*Actions*, section *Artifacts*; downloading needs a GitHub login). Each zip contains the executables, their
libraries, the German and French resources (`de\`, `fr\`), the debug symbols (`.pdb`, for readable crash logs), `LICENSE`,
`THIRD-PARTY-NOTICES.md` and `THIRD-PARTY-LICENSES.txt` (from `licenses/`). Unzip and run the
`.exe`; the .NET Framework 4.8 is required (see [Requirements](#requirements)). These are prototype builds for
testing, not releases. The manual test on a real Windows computer is described in German in
[docs/TEST-PLAN.de.md](docs/TEST-PLAN.de.md).

**Versioning**: the version of all assemblies is maintained in one place, `SharedAssemblyInfo.cs`
(currently `1.1.0`, the version the suite installer 1.1.0 packages).

**Line endings**: `.gitattributes` stores text files with LF and checks C#, `.resx` and other Visual Studio
files out with CRLF, so no extra `core.autocrlf` configuration is needed.

**Code style**: `.editorconfig` defines the .NET naming conventions (shown as suggestions in Visual Studio and
Rider). Code comments are written in English.

**Localization**: every text of the launcher is a string of `Empire Earth Launcher/Properties/Resources.resx`
(English, the neutral language) with its German (`Resources.de.resx`) and French (`Resources.fr.resx`)
translation; each window sets its texts in `ApplyTexts()`, the designer texts are placeholders
([ADR 0009](docs/adr/0009-localization-with-resx-en-de-fr.md)). The build puts the translations next to the
program as `de\` and `fr\` satellite assemblies; copy them with the program. The mod creator has English and
French (`Properties/Resources*.resx` of its project). How to translate, add a text or a language:
[docs/TRANSLATING.md](docs/TRANSLATING.md).

### Project layout

```
Empire-Earth.sln                  Root solution containing every project
SharedAssemblyInfo.cs             Version information shared by all assemblies
CHANGELOG.md                      Changes of each version (Keep a Changelog)
THIRD-PARTY-NOTICES.md            Vendored code and NuGet dependencies with their licenses
docs/                             ARCHITECTURE.md (v2 target), CONTRACT.md (shared with the setup), adr/
                                  (decision records), TEST-PLAN.de.md (manual test on Windows, German),
                                  TRANSLATING.md (languages, how to translate), contract-samples/ (byte samples
                                  of install.ini, files.sha256 and the install record, shared with the setup)
Empire-Earth-Launcher-Core/       UI-free core library of the launcher (Empire_Earth_Launcher_Core.dll, ADR 0003):
│                                 only the BCL and the WON library, no WinForms, System.Drawing or Krypton
├─ Contract/                      Fixed names of docs/CONTRACT.md: products, games, keys, files, contract version;
│                                 CompatibilityLayers (the entries of contract 3.7 and the old values)
├─ Backup/                        RegFileWriter (.reg files like regedit's), RegistryExport, BackupLocations
│                                 (%LOCALAPPDATA%\Empire Earth Launcher\Backups, ADR 0007), FileBackup (files
│                                 copied into the backup and read back before they are removed)
├─ GameSettings/                  Contract 3: GameSettingsTable, ComputedValues, GameDefaultsService (marker, first
│                                 run, display question, Installed From, reset), ConsistencyChecker, HintVisibility,
│                                 CompatibilityOptions, LauncherWritePolicy (the launcher's allow-list)
├─ Integrity/                     Contract 2: ManifestReader (files.sha256), FileClassifier (code, mutable, data),
│                                 IntegrityChecker (quick and full check, cancelled by a setup), IntegrityReport
├─ Installations/                 Discovery of the installations (contract 1.4): install records, install.ini,
│                                 uninstall keys, "Installed From" values, launcher folder -> InstallationDiscovery
│                                 -> Installation with real EE/AoC folders (ADR 0015); EffectivePathResolver
│                                 (VirtualStore copies of game files, ADR 0016); SuiteRecordReader (the optional
│                                 record of the suite installer, contract 1.6, read-only)
├─ Platform/                      Windows behind interfaces (ADR 0006): IRegistry/WindowsRegistry (explicit views),
│                                 RegistryLocation, RegistryValue, RegistryPath (canonical form), RegistryWritePolicy
│                                 and PolicyCheckedRegistry (protected keys, allow-list, ADR 0007), IFileSystem/
│                                 LocalFileSystem (files, folders, drive kinds), WinPath (Windows path rules),
│                                 IMutexProbe/WindowsMutexProbe, IMutexOwner/WindowsMutexOwner (single instance),
│                                 IClock, ISystemInfo/WindowsSystemInfo (Windows version, Wine, screen size, code
│                                 page), IProcessStarter/ShellProcessStarter (shell execute, open a folder),
│                                 IProcessList, IFileVersionReader, IHttpsClient/HttpsClient (no redirects, 10 s,
│                                 4 KiB, certificate check of Windows, ADR 0008), INetworkInfo/WindowsNetworkInfo
│                                 (network adapters, name lookups with a 5 s limit)
├─ Diagnostics/                   The network check and the report: NetworkDiagnostics, OutageHint, AddressClassifier,
│                                 NeoEeConfigReader, WonLobbyConfigReader, UpnpInfoParser (all only read),
│                                 ReportAnonymizer (privacy rules), DiagnosticsReport
├─ Logging/                       ILogger, TraceFileLogger (log file with trimming)
├─ Maintenance/                   The maintenance tools: CleanupCandidates (the cleanup list of ARCHITECTURE 4.6),
│                                 CleanupAdvice, RegistryCleanup, WonLoginReset, VirtualStoreScanner, SavedGames
│                                 (export, import), NameChecks, ManifestFiles
├─ Settings/                      LauncherSettings, SettingsStore (settings.json, ADR 0005), LauncherPaths,
│                                 UiLanguage (the language setting, ADR 0009)
├─ Lobby/                         LobbyProfileRepository: lobby profiles and friends of the game folder (effective paths);
│                                 PlayerListPoller: the online player list (async loop, ADR 0004);
│                                 PlayerListPolling: polls it only while the selected installation is NeoEE
├─ Play/                          GameStarter (ADR 0010: setup, game, program, Installed From, first run, shell start),
│                                 RunningGameDetector, SetupWatcher (setup mutexes every 2 s), ProgramVersions,
│                                 SingleInstance, MutationGuard (no change while a setup or a game runs, ADR 0016),
│                                 LauncherArguments (--product), InstanceForwarding (the hand-over to a running launcher)
└─ Repair/                        RepairAdvice (the steps of contract 4.4), SetupDownloadPage (the three download
                                  pages, contract 4.3), UpdateApi (the query and failures of 4.5), UpdateChecker (4.5),
                                  SuiteRepairLocator (the folder of the suite for the advice, 4.4)
Empire Earth Launcher/            The launcher (WinForms + Krypton UI)
├─ Program.cs                     Entry point and composition root: creates and passes on the services
├─ app.manifest                   Application manifest: asInvoker, Windows 7 to 11, not DPI-aware
├─ UiOperation.cs                 Runs the async work of event handlers (ADR 0004); UnexpectedError.cs: error message
├─ Texts.cs                       Results -> texts in the UI language (ADR 0009); each window has its ApplyTexts()
├─ Properties/Resources*.resx     All UI texts: English (neutral), German (.de), French (.fr), and the images
├─ IThemeService.cs               Theme interface (implemented by KryptonThemeService.cs)
├─ InstallationService.cs         The installations found and the selected one (runs the core's discovery)
├─ GameSettingsModel.cs           State and actions of the game settings for the Settings and Play pages
├─ PlayModel.cs                   State and actions of the Play page (the four entries, versions, start)
├─ IntegrityModel.cs              The integrity check of the selected installation (quick check, full check)
├─ UpdateModel.cs                 The update API: version check, download of the repair advice
├─ MaintenanceModel.cs            The maintenance tools of the Tools page (scans, actions, backup folder)
├─ DiagnosticsModel.cs            The network check and the diagnostics report of the Tools page
├─ CleanupView.cs                 What the registry cleanup shows (summary, keys to select, advice, delete button)
├─ ToolsUserControl.cs            The Tools page (integrity, repair advice, version check, maintenance tools, network,
│                                 report)
├─ RepairAdviceDialog.cs          The repair advice window (built in code, wraps every language)
├─ LauncherWrapLabel.cs           Every wrapping text: palette font as a copy no palette can dispose, never a red X
├─ ScrollPageLayout.cs            Stacks the controls of the pages and of their group boxes for the width of the window
└─ Resources/                     Images and icon used by the UI
Empire-Earth-WON/                 WON/NeoEE library, no UI (used by the launcher)
├─ NeoApiClient.cs                Client for the NeoEE lobby server
├─ NeoServerEndpoint.cs           Server address and timeout
├─ DeadlineStream.cs              One time budget for a whole request/reply exchange
└─ LobbyPersistentData.cs         Parser for the WON lobby files (_wonlobbypersistent.dat, _wonuser*.dat)
Empire-Earth-Mod/
├─ Empire-Earth-Mod-Lib/          Mod library: ModData (mod description), ModAssets (icon/banners),
│                                 ModPackageBuilder/ModArchiveReader (.eem packages), EemFormat (layout and rules
│                                 of .eem archives), Windows version detection; ZipStorer.cs is the vendored
│                                 ZipStorer 3.7.0 with local fixes, see THIRD-PARTY-NOTICES.md
└─ Empire-Earth-Mod/              Mod creator (WinForms), uses Empire-Earth-Mod-Lib
Empire-Earth-Launcher.Tests/      Unit tests (NUnitLite console program), one folder per tested project:
├─ Architecture/                  Rules for the whole solution (project settings, core dependencies, registry aliases,
│                                 placeholder controls, ApplyTexts, resource parity, test plan, contract checklist,
│                                 test isolation, network destinations, process starts, TLS and certificate rules,
│                                 synthetic fixture hashes, ZIP extraction only in the mod archive reader)
├─ Core/                          The core library, one folder per area
├─ Launcher/                      UI helpers, installation service, page models, start of a second launcher
├─ Won/                           WON lobby files, NeoEE protocol
├─ Mod/                           Mod library and .eem archives
├─ Fakes/                         In-memory registry and file system, mutex probe, clock, logger, process starter,
│                                 process list, file versions, HTTPS client, network information (with tests)
└─ TestSupport/                   Temporary folders, chunked streams, repository root, project files, worlds of
                                  installations, MappedFileSystem (real files behind a drive letter)
Empire-Earth-Launcher.RealMachineTests/  Checks of the core against a real installation on a GitHub-hosted runner
├─ RealMachine/                   The fixtures of the category RealMachine (explicit, switched on by
│                                 EE_LAUNCHER_REAL_MACHINE_TESTS=1) and the gate
├─ Checks/                        Discovery, integrity, game settings and defaults against the expectation
├─ Expectations/, Json/           The expectation file (strict JSON reader)
├─ Harness/                       Session, read-only and recording wrappers, snapshot, gate, pick-targets;
│                                 RealAdapters, the only file that creates the real adapters
└─ SelfTest/                      The same checks on the in-memory fakes of the unit tests
packages/                         NuGet packages, restored on build (not committed)
.github/workflows/build.yml       CI build and test run
```

### Configuration and files

- **Installations** ([contract 1.4](docs/CONTRACT.md#14-discovery-by-the-launcher)): when its window is shown, the
  launcher searches every installation of Empire Earth in the background, from five sources: the folder chosen on
  the *Launcher* page; the install records of community setups since v2
  (`Software\Empire Earth Community\Installations\<NeoEE|EE>`, HKCU, then HKLM 64-bit, then 32-bit view); the
  uninstall keys `{<GUID>}_is1` whose publisher is exactly `Empire Earth Community` or
  `Empire Earth Community & NeoEE` (every community setup, also 1.7.2, except the suite's own uninstall key, contract
  revision 5); the `Installed From Volume` and `Installed From Directory` values of `Software\Neo\Empire Earth`, then
  `Software\SSSI\Empire Earth` (retail, GOG and older installations use the SSSI key), each in HKCU, HKLM 32-bit, HKLM
  64-bit view (the key comes before the hive); and the folder of the launcher or its parent. Entries for the same
  install folder are one installation; `_setupdata_<Product>\install.ini` tells community setups since v2 (also
  portable ones) apart from setups up to 1.7.2 and from other installations. Each installation keeps its real game
  folders, e.g. `C:\Games\EE` of a copy or `D:\Empire Earth` directly below a drive. The *Launcher* page lists them
  (product, install folder, Empire Earth folder, type, state) and uses the chosen one, else the first one found. An
  installation whose `Empire Earth.exe` (or `EE-AOC.exe`) is missing is listed as damaged, never as "not found"
  (antivirus programs often delete or quarantine game files). A hint says when several installations share one set of
  game settings (all EE installations, also retail and GOG, use `Software\SSSI\Empire Earth`), when EE and NeoEE are
  installed in the same folder, and when a setup is newer than the launcher. The search only reads; the log names
  every candidate and why it was used or left out.
- **Game folder**: the folder chosen with "..." on the *Launcher* page (or by picking an installation of the list)
  is saved in `settings.json` per product (`ProductFolders`, the Empire Earth folder of the installation, and
  `LastProduct`, the product chosen last; `GameDirectory` repeats the folder of the last product so that launcher
  1.0.0 reads the same installation); it may be the install
  folder, the Empire Earth folder or the Art of Conquest folder, and it stays chosen even if it no longer exists, so
  that the player sees it. *Auto-detect* removes the choice. The launcher reads the WON lobby files from the
  Empire Earth folder of the selected installation; for a game folder below `Program Files`, `ProgramData` or the
  Windows folder it reads the copy in `%LOCALAPPDATA%\VirtualStore\...` first when one exists, because the game
  (a program without a manifest) reads and writes there, the launcher not.
- **Contract with the setup**: [docs/CONTRACT.md](docs/CONTRACT.md) (shared with the
  [Empire Earth Setup](https://github.com/DritteRippe/Empire-Earth-Setup) repository; contract version 1, released,
  revision 7) specifies the install record, the integrity manifest, the per-user default game settings and the repair
  hand-off that the launcher is built on. `docs/contract-samples/` holds synthetic byte samples of `install.ini` (admin,
  user with `[MissingAfterInstall]`, portable), `files.sha256` and the install record as a `.reg` file, which the
  launcher's readers are tested against; the setup repository keeps the same folder and checks its writers against it.
- **Integrity check** ([contract 2](docs/CONTRACT.md#2-integrity-manifest)): after every search of the installations
  the launcher reads `_setupdata_<Product>\files.sha256` and `install.ini` of the selected community installation (setup
  v2 or later) and checks in the background that every listed file exists and that the program files (`exe dll asi
  ...`, the class `code` of contract 2.4) have their SHA-256; "Check all files" on the *Tools* page also hashes the game
  data. Changed `cfg ini conf config log` files are never reported. The states: OK; Modified (only information: mods,
  HD packs); Incomplete (a data file missing); Damaged (a program file missing or changed; for NeoEE worded "changed
  since the installation", because the NeoEE updater may replace files); Unknown (installed by a setup up to 1.7.2, a
  newer setup, an older setup ran later, the setup could not write its list); a foreign installation is not checked.
  The check opens every file at most once with sharing that lets a setup delete and rename it, does not start while a
  setup runs and stops when one starts; it never blocks Play and logs every finding with path, class, expected and
  actual hash.
- **Update API** ([contract 4.3, 4.5](docs/CONTRACT.md#4-repair-hand-off),
  [ADR 0008](docs/adr/0008-https-policy-and-update-api.md)): only on request, `GET
  https://api.empireearth.eu/setup/?product=<AppId>&type=game|setup&version=<version>` for the version check (and
  `&type=game` as the reference of the network check) over HTTPS with the certificate check of Windows, no redirects,
  10 seconds and at most 4 KiB; on Windows 7 TLS 1.2 is requested explicitly, elsewhere Windows chooses. The launcher
  no longer asks it for a download address: the repair window opens the download page of the product, or for an
  installation of the suite the release page of the package (contract 4.3, revision 7), without a request, and a failed
  version check says why in the window and the log. The answer is decoded by the launcher itself (the character set of
  the answer if Windows knows it, else the one of a byte order mark, else UTF-8), so an unusual `charset` is no error (1.1.1).
- **Architecture of v2**: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) describes the structure as built (UI-free
  core library, thin WinForms UI on .NET Framework 4.8), data flows, threading, error handling, logging,
  localization and tests, and ticks the launcher items of the contract checklist; the decisions behind it are recorded
  in [docs/adr/](docs/adr/README.md).
- **Play** ([ADR 0010](docs/adr/0010-game-start-and-mutex-probing.md)): *Play* starts the chosen game of the selected
  installation (the entry of the list: `LastProduct` and `LastGame` in `settings.json`) in this order: no setup may run (`EE_Setup`, `NeoEE_Setup`), the same
  game may not run (its mutex), the other game running asks first, the program must exist (else the repair advice),
  then the "Installed From" values are synchronized and the first run of the defaults is done, then the program
  starts through the Windows shell in its game folder. The log has one line per start with the process id. While a
  setup runs (checked every two seconds), nothing is started, changed or searched; the installations are searched
  again when it has ended. Only one launcher runs per Windows session (mutex `EmpireEarthCommunityLauncher`).
- **User settings** (game folder per product, last product, theme, custom theme file, UI language, hints hidden from the *Play* page, last game) are kept in
  `%LOCALAPPDATA%\Empire Earth Launcher\settings.json` (UTF-8 JSON with a `SchemaVersion`,
  [ADR 0005](docs/adr/0005-own-settings-file-instead-of-user-config.md)), so they survive moving or updating
  the launcher. The file is written as `settings.json.tmp` first and then swapped in, so a crash never leaves
  half a file. A missing file means the defaults. A damaged file (cut off, hand-edited, not JSON) is renamed
  to `settings.json.damaged` (replacing an older copy), logged, and the defaults are used. A file that
  cannot be read, or that a newer launcher wrote, is never overwritten: the launcher uses the defaults and
  changes apply to the running launcher only (logged). Members the launcher does not know are kept when it
  saves. Test builds before v2 kept these settings in .NET's `user.config`; they are not taken over.
- **Server settings**: `NeoServerHost`, `NeoServerPort`, `NeoTimeoutMilliseconds` and
  `PlayerListPollIntervalMilliseconds` are application settings in `Empire Earth Launcher.exe.config`
  (generated from `App.config`) and can be changed there without rebuilding. `NeoTimeoutMilliseconds` limits
  the connect and, separately, the whole request/reply exchange (not only each read). The NeoEE status
  service is plain TCP without TLS or server authentication (a property of the server); the launcher only reads public status
  data from it and validates every reply.
- **Language**: the launcher shows English, German or French: the language of Windows (any other Windows language
  shows English) or the one chosen on the *Launcher* page (`UiCulture` in `settings.json`: empty for Windows,
  `en`, `de` or `fr`), used from the next start on. Only the texts change; numbers and dates keep the Windows
  format. The log names the language in use.
- **Themes**: Krypton palette files (`*.xml`) in the `themes` folder next to the executable, or any file chosen
  with *Custom file...*. No theme files are shipped yet; without them the designer colors are used, shown as *Built-in
  colors*, the first item of the list (a missing default theme `Light` is only logged as information). Choosing
  *Built-in colors* while a theme file is applied saves `"ThemeName": "<built-in>"` and takes effect at the next start.
- **Log**: `%LOCALAPPDATA%\Empire Earth Launcher\log.txt` (the installation folder may be read-only). English
  messages with ISO time stamps; it never contains CD-key values, WON login data or the names the name check finds
  (ADR 0013), and the lines of the network check follow the privacy rules of the report (no MAC address, adapter
  name, public or IPv6 address, user or computer name). Errors of background tasks that nobody handled are logged
  there as well.
- **Registry**: the core opens every HKLM key with an explicit view (64- or 32-bit) and never depends on the
  launcher's own bitness (contract 0). Every change of the registry passes the write policy of
  [ADR 0007](docs/adr/0007-registry-write-scope-and-reg-backups.md): only HKCU keys of an allow-list with their
  value names (`LauncherWritePolicy`: the game settings keys of contract 3.1 with the values of 3.2, the defaults
  marker, the GPU preference and the compatibility values of the game programs), deleting only the eight HKCU keys of
  the cleanup list (ARCHITECTURE 4.6), and never `Software\Sierra\CDKeys` (the NeoEE CD keys), the install records or
  the uninstall keys, in no hive, view or alias. Nothing is changed while a setup or a game runs (ADR 0016). HKLM is
  only read; the cleanup shows HKLM leftovers with advice and never asks for administrator rights.
- **Game settings** ([contract 3](docs/CONTRACT.md#3-per-user-default-game-settings)): after each search the launcher
  sets up the recommended game settings for the Windows account that runs it, once per game and account (the
  marker `HKCU\Software\Empire Earth Community\GameDefaults\<NeoEE|EE>`, values `EE` and `AoC`), but only for an
  installation that is the only one using its settings key; it creates missing values only, and asks before it
  replaces display settings that differ. Values outside the table of the contract (player names and the like) are
  never touched. The *Settings* page shows the state, the hints and the compatibility options; its reset and
  "Apply recommended display" write a backup first.
- **Backups**: `%LOCALAPPDATA%\Empire Earth Launcher\Backups\<yyyy-MM-dd_HHmmss>_<action>\` with one `.reg` file per
  game (`<time>_<NeoEE|EE>_<EE|AoC>.reg`, or `<time>_Layers.reg` for the compatibility values), or one
  `<time>_registry-cleanup.reg` with every key the cleanup deleted. Double-click a file (or `reg import <file>`) to
  restore the settings exactly as they were before, including the removal of values the action created. The WON login
  reset (`_won-login-reset`) and an import that replaces saved games (`_import-saved-games`) keep the files in
  `EE\`, `AoC\`, `EE-VirtualStore\` or `AoC-VirtualStore\` with `moved-files.txt`, which names where each file was;
  copy a file back there to restore it. **The backup folder contains login data after a WON reset: never pass it on.**
  "Open backup folder" on the *Tools* page opens it. The launcher never deletes backups.
- **Network check** ([ADR 0008](docs/adr/0008-https-policy-and-update-api.md)): only on request. It reads the
  adapters from Windows, resolves the host of the NeoEE status server and the `Server` of each `NeoEE.cfg` (5 seconds
  each), asks the update API for the latest game version (`&type=game`, AppId of the selected installation, else of the
  first one with an AppId) and asks the status server for the player list, all at the same time; it reads `NeoEE.cfg`,
  `WONLobby.cfg` and `upnp_info.txt` of both game folders (the VirtualStore copy first) without changing them. It
  never connects to the ports 10002 and 10003 of NeoEE and asks no "what is my IP" service; the external address of
  `upnp_info.txt` is shown only as its class (public, CGNAT, private). The format of `upnp_info.txt` is not known for
  sure: an unknown layout is shown as "unknown format".
- **Diagnostics report**: built when you click "Copy report" or "Save report..." from the latest results of every
  page; saved as UTF-8 text into the file you choose (`Documents` is suggested, a game folder is refused). Paths below
  your profile start with `%USERPROFILE%` or `%LOCALAPPDATA%`, other occurrences of your user name and your computer
  name are replaced by `<user>` and `<computer>`, public and IPv6 addresses by their class, player and profile names by
  "EE lobby profile 1: characters outside printable ASCII"; CD keys appear only as "exists" or "missing". The log
  records only that the report was copied or where it was saved.
- **Exports of saved games**: a new folder `Empire Earth saves <yyyy-MM-dd_HHmmss>` in the folder you choose (not a
  game folder), with `EE\Saved Games`, `EE\Scenarios`, `AoC\Saved Games` and `AoC\Scenarios`. When the same file
  name is in the game folder and in its VirtualStore copy, the export takes the copy (the one the game uses) and the
  page names the other. Imported files go to `Data\Saved Games` or `Data\Scenarios` of the game, or into its
  VirtualStore folder when Windows does not let a standard user write into a game folder below `Program Files`.

## 🔨 Contributing
Pull requests are welcome; work happens on a short-lived branch with a pull request into `main`.\
Bugs go into an [issue](https://github.com/DritteRippe/Empire-Earth-Launcher/issues/new/choose) (the form asks for the logs); security problems are reported privately,
see [SECURITY.md](SECURITY.md).\
For major changes, please open an issue first to discuss what you would like to change or discuss with us on Discord.

## 📖 License
[GNU General Public License v3.0](LICENSE)

Third-party components are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). The launcher neither contains nor downloads
dgVoodoo (dgVoodoo's terms do not allow bundling it in launchers); the community setup installs it into the game folders.
