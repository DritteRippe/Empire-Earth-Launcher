# Testplan Launcher v2 (Test auf einem echten Windows-Rechner)

Dieser Testplan beschreibt den manuellen Test von Empire Earth Launcher v2 und dem Mod-Creator auf einem echten
Windows-Rechner (dem Laptop-Test). Er prüft, was die automatischen Tests unter Mono nicht prüfen können:
echte Registry-Ansichten, Mutexe, Programmstart, Bildschirm und Skalierung, TLS unter Windows, die Darstellung
von Krypton und die Texte der Oberfläche ([ADR 0012](adr/0012-test-strategy.md)).

| | |
|---|---|
| Stand | Fälle von L-WP1 bis L-WP9; der Plan ist vollständig: Jede Anforderung und jeder Forum-Testfall ist zugeordnet (Abschnitt 7), kein Paket ist offen |
| Sprache | Deutsch (die Programmtexte gibt es auf Englisch, Deutsch und Französisch) |
| Gehört zu | [ARCHITECTURE.md](ARCHITECTURE.md), Abschnitt 11 und 15 |

Inhalt: [1. Voraussetzungen](#1-voraussetzungen) · [2. Paket holen und prüfen](#2-paket-holen-und-prüfen) ·
[3. Wo der Launcher Dateien ablegt](#3-wo-der-launcher-dateien-ablegt) ·
[4. Ergebnisse festhalten](#4-ergebnisse-festhalten) · [5. Testfälle je Arbeitspaket](#5-testfälle-je-arbeitspaket) ·
[6. Optional: Windows 7 SP1 in einer VM](#6-optional-windows-7-sp1-in-einer-vm) ·
[7. Zuordnung der Anforderungen und Forum-Testfälle](#7-zuordnung-der-anforderungen-und-forum-testfälle)

## 1. Voraussetzungen

- **Windows**: Windows 10 (Version 1607 oder neuer) oder Windows 11 für den Haupttest. Windows 8.1 ist
  unterstützt, aber kein Pflichtfall. Windows 7 SP1 gilt als „unterstützt, aber ungetestet“ und hat einen eigenen,
  optionalen Abschnitt (6). Windows 8.0 und Windows 10 1507/1511 werden nicht unterstützt (dort gibt es kein
  .NET Framework 4.8).
- **.NET Framework 4.8**: in Windows 10 ab Version 1903 und in Windows 11 enthalten. Prüfen in PowerShell:

  ```powershell
  (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full').Release
  ```

  Ein Wert ab **528040** bedeutet 4.8 oder neuer. Ohne PowerShell (Eingabeaufforderung):
  `reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Release` – der Hex-Wert muss mindestens
  `0x80ea8` sein. Fehlt 4.8: https://dotnet.microsoft.com/download/dotnet-framework/net48 (braucht Adminrechte).
- **Windows-Version notieren**: `Win+R` → `winver` (Version und Build), dazu 32 oder 64 Bit
  (Einstellungen → System → Info).
- **Konto**: den Test mit einem **normalen Benutzerkonto** (ohne Adminrechte) ausführen, wenn möglich; der
  Launcher darf nie nach Adminrechten fragen. Wo ein Fall ein zweites Konto oder Adminrechte braucht, steht es
  dabei.
- **Empire Earth**: eine Installation mit dem Community-Setup ist ab L-WP4 nötig (Erkennung, Spieleinstellungen,
  Spielstart). Für L-WP1 reicht der Launcher allein.
- **Sicherung vor schreibenden Tests** (ab L-WP5): die Spieleinstellungen in der Registry vorher exportieren,
  z. B. `reg export "HKCU\Software\Neo" "%USERPROFILE%\Desktop\neo-vorher.reg"` und dasselbe für
  `HKCU\Software\SSSI` und `HKCU\Software\Mad Doc Software`; ab L-WP8 auch die Ordner `Data\Saved Games` und
  `Data\Scenarios` beider Spiele kopieren. Den Schlüssel `Software\Sierra\CDKeys` nie ändern oder löschen.
- **Netzwerk**: Der Launcher fragt im Hintergrund die Online-Spielerliste beim NeoEE-Statusserver ab
  (`titan.empireearth.eu`, Port 10005, eingestellt in `Empire Earth Launcher.exe.config`). Ab L-WP7 fragt er nur auf
  Wunsch (Reparatur-Hinweise, „Version prüfen“, „Nach Updates suchen“) per HTTPS `api.empireearth.eu` nach dem
  Download des aktuellen Setups und nach den Versionen; gesendet werden nur die AppId der Installation und die Version
  (WP7-10 bis WP7-12). Ab L-WP9 kommt nur auf Wunsch die Netzwerkprüfung dazu: Namensauflösung (DNS) der NeoEE-Server,
  dieselbe Anfrage an den Update-Server und an den Statusserver; kein Dienst für die öffentliche Adresse, nichts an die
  Ports 10002/10003 (WP9-01). Weitere Verbindungen gibt es nicht.

## 2. Paket holen und prüfen

### 2.1 Laptop-Paket

Das Laptop-Paket entsteht aus dem lokalen Release-Build, der den CI-Build nachbildet (gleiche
Referenz-Assemblys `Microsoft.NETFramework.ReferenceAssemblies.net48` 1.0.3, Release, alle Tests grün). Es
kommt nicht ins Repository. Es besteht aus zwei Dateien:

- `Empire-Earth-Launcher-v2-<Stand>.zip` mit den Ordnern `Empire-Earth-Launcher\` (Programm, `.exe.config`,
  Bibliotheken – ab L-WP2 auch `Empire_Earth_Launcher_Core.dll` –, Sprachordner `de\` und `fr\`) und `Empire-Earth-Mod-Creator\`, dazu `LICENSE`,
  `THIRD-PARTY-NOTICES.md` und `THIRD-PARTY-LICENSES.txt`;
- `Empire-Earth-Launcher-v2-<Stand>.zip.sha256` mit der SHA-256-Prüfsumme.

Pakete ab L-WP6 enthalten zusätzlich den Ordner `Tests\` mit dem Testprogramm `Empire-Earth-Launcher.Tests.exe` und
seinen Bibliotheken (ohne Quelltexte), damit die automatischen Tests einmal unter dem echten .NET Framework 4.8
laufen (WP1-11; [ADR 0012](adr/0012-test-strategy.md), Ergänzung nach der Planprüfung). Das Paket für den Laptop-Test nach
L-WP9 heißt `Empire-Earth-Launcher-v2-L-WP9.zip`; sein Inhalt wird in WP9-15 geprüft.

Beide Dateien kommen als Dateianhang aus der Claude-Sitzung (oder per USB-Stick) auf den Laptop. Vor dem
Entpacken die Prüfsumme vergleichen:

```powershell
Get-FileHash .\Empire-Earth-Launcher-v2-<Stand>.zip -Algorithm SHA256
Get-Content .\Empire-Earth-Launcher-v2-<Stand>.zip.sha256
```

Unter Windows 7 ohne `Get-FileHash`: `certutil -hashfile Empire-Earth-Launcher-v2-<Stand>.zip SHA256`. Die
beiden Werte müssen gleich sein (Groß-/Kleinschreibung egal). Sind sie verschieden: **nicht starten**, Paket
neu holen.

Danach:

1. Rechtsklick auf die Zip-Datei → Eigenschaften → unten **„Zulassen“** ankreuzen → OK (sonst markiert Windows
   jede entpackte Datei als „aus dem Internet“). Alternativ in PowerShell `Unblock-File .\<Datei>.zip`.
2. In einen eigenen Ordner entpacken, z. B. `C:\EE-Launcher-Test\` – nicht nach `C:\Program Files` und nicht
   in den Spielordner (beides wird in späteren Fällen gezielt getestet).
3. Die Programme sind nicht signiert. Zeigt Windows SmartScreen „Der Computer wurde durch Windows geschützt“:
   **Weitere Informationen** → **Trotzdem ausführen**.

### 2.2 Alternative: selbst unter Windows bauen

- Visual Studio 2019 oder 2022 mit der Workload **.NET-Desktopentwicklung** (enthält das Targeting Pack für
  .NET Framework 4.8), alternativ die Build Tools für Visual Studio plus `nuget.exe`.
- Quellstand: Branch `v2` des Launcher-Repositorys (zum Beispiel als Git-Bundle:
  `git clone Empire-Earth-Launcher-v2.bundle -b v2 Empire-Earth-Launcher`).
- Bauen und testen in der „Developer PowerShell for VS“ im Repository-Ordner:

  ```powershell
  nuget restore Empire-Earth.sln
  msbuild Empire-Earth.sln /p:Configuration=Release
  .\Empire-Earth-Launcher.Tests\bin\Release\Empire-Earth-Launcher.Tests.exe
  ```

  Erwartet: `Overall result: Passed`, `Failed: 0`, Exit-Code 0 (`$LASTEXITCODE`). Unter Windows laufen auch die
  Tests mit, die unter Mono übersprungen werden (`Skipped: 0`).
- Meldet MSBuild `MSB3644` (Referenz-Assemblys für .NET Framework 4.8 fehlen): die Befehle mit
  `Microsoft.NETFramework.ReferenceAssemblies.net48` aus dem README (Abschnitt *Building*) verwenden.
- Ergebnis: `Empire Earth Launcher\bin\Release\` und `Empire-Earth-Mod\Empire-Earth-Mod\bin\Release\`. Diese
  Ordner an einen eigenen Ort kopieren und von dort testen.

### 2.3 Automatischer Teil auf GitHub (keine Fälle dieses Plans)

Der End-to-End-Workflow des Setup-Repositorys installiert die echten Setups auf einem GitHub-Runner (Windows Server,
wird nach dem Lauf verworfen) und prüft dort nach jedem Schritt mit dem eigenen Testprogramm
`Empire-Earth-Launcher.RealMachineTests` den Kern des Launchers gegen die echte Installation: Erkennung, schnelle und
vollständige Prüfung, Stand der Standardwerte, Hinweise, auf Wunsch die Standardwerte beim Start, einen zweiten Start und
das Zurücksetzen, und zuletzt, dass CD-Keys, Einträge, Deinstallationsschlüssel und Spieldateien unverändert sind
(README, Abschnitt Tests, „Real machine“). Das deckt Teile von WP4-05, WP4-13, WP5-01, WP5-08, WP7-01, WP7-02, WP7-07 und
WP7-09 ohne Oberfläche ab; diese Fälle bleiben trotzdem Fälle dieses Plans. Nicht abgedeckt sind die Oberfläche, ein
zweites Windows-Konto, der Spielstart, Windows-Client-Versionen, Skalierung und das Netz. Das Programm gehört nicht zum
Laptop-Paket und wird hier nicht ausgeführt; WP1-11 betrifft nur `Empire-Earth-Launcher.Tests.exe`.

## 3. Wo der Launcher Dateien ablegt

Alle Dateien des Launchers liegen pro Benutzer unter `%LOCALAPPDATA%\Empire Earth Launcher\` (öffnen mit
`Win+R` → `%LOCALAPPDATA%\Empire Earth Launcher`). Neben das Programm schreibt der Launcher nie.

| Was | Ort | seit |
|---|---|---|
| Protokoll | `log.txt`, ältere Einträge in `log.txt.old` (ab 1 MiB gekürzt) | heute |
| Einstellungen (Spielordner, Theme, ab L-WP3 Sprache, ab L-WP5 ausgeblendete Hinweise, ab L-WP6 zuletzt gewähltes Spiel `LastGame`) | `settings.json`; eine beschädigte Datei wird zu `settings.json.damaged`, beim Speichern entsteht kurz `settings.json.tmp` | L-WP2 |
| Sicherungen (`.reg`-Dateien; ab L-WP8 auch die verschobenen WON-Login-Dateien mit `moved-files.txt` und ersetzte Spielstände) | `Backups\<yyyy-MM-dd_HHmmss>_<was>\` mit `<was>` = `reset-game-settings` (L-WP5), `registry-cleanup`, `won-login-reset` oder `import-saved-games` (L-WP8), z. B. `Backups\2026-10-02_153012_reset-game-settings\2026-10-02_153012_NeoEE_EE.reg` | ab L-WP5 |
| Arbeitsordner des Mod-Creators | `Mod Creator\` | heute |

Testpakete vor L-WP2 speicherten die Einstellungen in einer `user.config` in einem von .NET angelegten
Unterordner von `%LOCALAPPDATA%` (Ordnername mit Hash). Der Launcher liest sie nicht mehr; finden und danach
löschen:

```powershell
Get-ChildItem $env:LOCALAPPDATA -Recurse -Filter user.config -ErrorAction SilentlyContinue |
  Where-Object FullName -like '*Empire*' | Select-Object FullName
```

Der Export der Spielstände (ab L-WP8) schreibt nur in den Ordner, den man wählt (neuer Unterordner
`Empire Earth saves <yyyy-MM-dd_HHmmss>`), nie unter `%LOCALAPPDATA%\Empire Earth Launcher\`.

Datenschutz: `log.txt` enthält Pfade mit dem Benutzernamen; vor dem Weitergeben ansehen. Der Ordner `Backups`
enthält ab L-WP8 Login-Daten des Spiels (WON-Dateien) und wird nie weitergegeben. Für das Forum ist ab L-WP9 der
Diagnosebericht gedacht (*Werkzeuge* → „Bericht kopieren“ oder „Bericht speichern ...“): Er ersetzt Benutzer- und
Rechnernamen, zeigt keine MAC- oder öffentliche IP-Adresse, keine Spielernamen und keine CD-Keys (WP9-11). Gespeichert wird
er nur in die Datei, die man wählt, nie in einen Spielordner.

## 4. Ergebnisse festhalten

Für jeden Fall: **OK**, **Fehler** oder **nicht geprüft**, bei Fehlern eine kurze Beschreibung, ein Screenshot
und `log.txt`. Einmal pro Testlauf notieren:

- Windows-Version und Build (`winver`), 32/64 Bit, Bildschirmauflösung und Skalierung (Einstellungen → System →
  Bildschirm);
- `.NET`-Release-Wert (Abschnitt 1);
- Stand des Pakets (Name der Zip-Datei und Prüfsumme) oder der Commit beim eigenen Build;
- Antivirus-Programm, falls nicht nur Microsoft Defender.

Vorlage:

```
Fall | Ergebnis | Notiz
WP1-01 | OK |
```

## 5. Testfälle je Arbeitspaket

### L-WP1 – .NET Framework 4.8, Anwendungsmanifest, Release-Build

| Fall | Schritte | Erwartet |
|---|---|---|
| WP1-01 | .NET-Wert prüfen (Abschnitt 1). | Release-Wert ab 528040. |
| WP1-02 | Paket nach 2.1 holen, Prüfsumme vergleichen, entsperren, entpacken (oder selbst bauen nach 2.2). | Prüfsummen gleich; beim eigenen Build alle Tests grün. |
| WP1-03 | `Empire Earth Launcher.exe` als normaler Benutzer per Doppelklick starten. | Keine Rückfrage der Benutzerkontensteuerung (UAC), kein Fehlerdialog, das Hauptfenster erscheint. Alle Seiten der Navigation lassen sich öffnen. |
| WP1-04 | `log.txt` öffnen (Abschnitt 3). | Neue Zeilen `Starting Empire Earth Launcher v0.1.0-alpha` und `Starting Empire Earth Launcher Form`. Keine Zeile mit `Error`, außer `The online player list of … is unavailable` (Statusserver nicht erreichbar, kein Fehler von L-WP1). `Warning : No Empire Earth installation found` ist ohne installiertes Spiel normal. |
| WP1-05 | Launcher läuft. Task-Manager → Details → Rechtsklick auf eine Spaltenüberschrift → Spalten auswählen → „UAC-Virtualisierung“ und „Plattform“ bzw. „Architektur“ (je nach Windows-Version) einblenden. | Beim Launcher: UAC-Virtualisierung **Deaktiviert**; auf 64-Bit-Windows „64 Bit“ bzw. „x64“, nicht 32 Bit. |
| WP1-06 | Rechtsklick auf `Empire Earth Launcher.exe` → Eigenschaften → Details. | Dateiversion 0.1.0.0, Produktversion 0.1.0-alpha. |
| WP1-07 | Darstellung bei Skalierung 100 % ansehen und einen Screenshot machen. | Krypton-Oberfläche vollständig: Bilder, goldene Schaltflächen, Kontrollkästchen und Texte wie bisher; nichts abgeschnitten. |
| WP1-08 | Skalierung auf 125 % oder 150 % stellen (Einstellungen → System → Bildschirm), Launcher neu starten, Screenshot. | Fenster von Windows vergrößert (darf etwas unscharf sein), Anordnung wie bei 100 %, keine überlappenden oder abgeschnittenen Elemente. Danach Skalierung zurückstellen. |
| WP1-09 | Mod-Creator `Empire_Earth_Mod.exe` starten. | Startet ohne UAC-Rückfrage; oben steht „You are using: “ mit der **richtigen** Windows-Version (Windows 10 bzw. Windows 11, nicht „Windows 8“). |
| WP1-10 | Launcher und Mod-Creator schließen. | Beide Prozesse sind im Task-Manager verschwunden. |
| WP1-11 | Nur mit einem Paket, das den Ordner `Tests\` enthält (sonst „nicht geprüft“): PowerShell im entpackten Ordner `Tests\` öffnen und `.\Empire-Earth-Launcher.Tests.exe --where "cat != SourceTree" --result=tests-windows.xml` ausführen, danach `$LASTEXITCODE` anzeigen. Die Datei `tests-windows.xml` dem Protokoll beifügen. | `Failed: 0`, Exit-Code 0. Die Tests legen nur Dateien im Temp-Ordner und Mutexe mit Zufallsnamen an; Registry, Netz und die Launcher-Dateien unter `%LOCALAPPDATA%` bleiben unberührt. Die Tests der Kategorie `SourceTree` brauchen die Quelltexte und laufen hier nicht. |

### L-WP2 – Core-Grundlage (settings.json, Protokoll, Mutation Guard)

Die Einstellungen liegen jetzt in `settings.json` (Abschnitt 3). Die Fälle brauchen kein installiertes Spiel.
Vor WP2-01 eine vorhandene `settings.json` (und `settings.json.damaged`) löschen. Zum Bearbeiten der Datei den
Launcher immer erst schließen; Editor: Notepad. Die Fälle nennen die englischen Texte; ab L-WP3 zeigt der Launcher
auf einem deutschen Windows deutsche Texte: „Auto-detect“ heißt dann „Automatisch“, „Chosen manually“ „Von Hand
gewählt“, die Seite *Launcher* bleibt *Launcher*. Wer die Fälle wörtlich nachvollziehen will, stellt vorher die
Sprache auf „English“ (WP3-10 und WP3-11).

| Fall | Schritte | Erwartet |
|---|---|---|
| WP2-01 | Launcher starten. Seite *Launcher*: mit „…“ neben „Empire Earth folder“ einen Spielordner wählen (ein beliebiger Ordner, die Rückfrage „enthält nicht Empire Earth.exe“ mit Ja beantworten). Launcher schließen. | `settings.json` existiert, enthält `"SchemaVersion": 1` und den gewählten Ordner (`\\` statt `\` ist in JSON richtig); keine `settings.json.tmp`. |
| WP2-02 | Launcher neu starten, Seite *Launcher* öffnen. | Der gewählte Ordner steht wieder da („Chosen manually“). `log.txt`: `Launcher settings loaded from …\settings.json.` |
| WP2-03 | Den entpackten Launcher-Ordner verschieben oder umbenennen und den Launcher von dort starten. | Der gewählte Ordner ist weiterhin eingestellt (mit `user.config` gingen die Einstellungen dabei verloren). |
| WP2-04 | Mit „Auto-detect“ zurückstellen, Launcher schließen und neu starten. | Automatische Erkennung aktiv; in `settings.json` ist `"GameDirectory": ""`. |
| WP2-05 | `settings.json` öffnen, den Inhalt durch `{"SchemaVersion":1,"ThemeName":"Da` ersetzen (abgeschnitten), speichern. Launcher starten. | Startet normal mit den Standard-Einstellungen, kein Fehlerdialog. Neben `log.txt` liegt `settings.json.damaged` mit genau diesem Inhalt, `settings.json` fehlt. `log.txt`: `Error : The launcher settings were damaged (…) and have been reset to their defaults. The damaged file was kept as …\settings.json.damaged.` |
| WP2-06 | Danach wieder einen Spielordner wählen, Launcher schließen und neu starten. | Neue `settings.json` mit dem Ordner; `settings.json.damaged` unverändert. |
| WP2-07 | In `settings.json` `"SchemaVersion": 1` in `"SchemaVersion": 2` ändern, speichern. Launcher starten, auf der Seite *Launcher* „Auto-detect“ klicken, Launcher schließen. | Standard-Einstellungen. `log.txt`: Warnung `… were written by a newer launcher (schema 2 …)` und beim Klick `The launcher settings are not saved …`. Die Datei ist unverändert (`"SchemaVersion": 2`). Danach die Datei löschen. |
| WP2-08 | Eine gültige `settings.json` in Notepad mit „Speichern unter“ → Codierung **UTF-8 mit BOM** speichern, Launcher starten. | Einstellungen werden gelesen, keine `settings.json.damaged`. |
| WP2-09 | Nur wenn eine `user.config` eines älteren Testpakets existiert (Abschnitt 3): Launcher starten. | Kein Fehler; der Launcher ignoriert die Datei (die Einstellungen von dort werden nicht übernommen). |
| WP2-10 | Im entpackten Ordner `Empire-Earth-Launcher\` nachsehen; Rechtsklick auf `Empire_Earth_Launcher_Core.dll` → Eigenschaften → Details. | Die Datei liegt neben `Empire Earth Launcher.exe` (ohne sie startet der Launcher nicht); Dateiversion 0.1.0.0, Produktversion 0.1.0-alpha. |
| WP2-11 | Nach allen Fällen `log.txt` durchsehen. | Keine Zeile `Unhandled exception` und keine `A background task failed`; `Error`-Zeilen nur die erwarteten aus WP2-05 und die des Statusservers (WP1-04). |

Was L-WP2 sonst noch enthält, hat noch keine Oberfläche und wird mit den Paketen getestet, die es benutzen: das
Registry-Schreibverbot für `Software\Sierra\CDKeys` samt aller Schreibweisen (`WOW6432Node`, VirtualStore) und der
Schutz vor Änderungen, während ein Setup oder das Spiel läuft (ab L-WP5 und L-WP6), sowie der Zugriff auf die echte
Registry mit 32- und 64-Bit-Ansicht (ab L-WP4).

### L-WP3 – Oberfläche aufräumen, Übersetzung Deutsch und Französisch

Ab L-WP3 zeigt der Launcher nur noch Bedienelemente, die etwas tun; die Attrappen der alten Oberfläche (Liste in
[ADR 0014](adr/0014-only-working-features-in-the-ui.md)) sind entfernt. Alle Texte gibt es auf Englisch, Deutsch und
Französisch; der Launcher folgt der Anzeigesprache von Windows (jede andere Sprache zeigt Englisch), außer auf der
Seite *Launcher* ist eine Sprache gewählt. Die Fälle brauchen kein installiertes Spiel.

Erwartete Lücken bis zu späteren Paketen: Auf der Seite *Spielen* ist die Gruppe „Spiel“ unter der Spielauswahl leer
und die Schaltfläche „Spielen“ startet noch nichts (L-WP4 bis L-WP6); die Seite *Einstellungen* ist nach dem
Bestätigen des Hinweises leer (die Optionen kommen mit L-WP5). Das ist kein Fehler dieses Pakets. Screenshots bitte
pro Seite und Sprache, Dateiname z. B. `WP3-de-Spielen.png`.

| Fall | Schritte | Erwartet |
|---|---|---|
| WP3-01 | Windows mit deutscher Anzeigesprache; in `settings.json` kein `UiCulture` oder `"UiCulture": ""`. Launcher starten. | Texte deutsch. Navigation: „Spielen“, „Einstellungen“, „Launcher“ untereinander ohne Lücke; keine Schaltfläche „Mods“. Titel „Empire Earth Launcher“. `log.txt`: `UI language: de-DE (Windows)` (bzw. die Windows-Sprache, z. B. `de-AT`). |
| WP3-02 | Seite *Spielen* ansehen, Screenshot. | Gruppe „Spiel“ mit „Empire Earth“ und „The Art of Conquest“; goldene Schaltfläche „Spielen“; rechts „Spieler online (...)“, danach „Spieler online (nicht verfügbar)“ oder „Spieler online (Anzahl)“; unten „Profil:“ mit Liste bzw. „Keine Empire-Earth-Installation gefunden“. **Nicht** mehr da: Gruppe „Language Settings“ (Language, Fallback, Game Text, Lobby, Voices), „No mods in use“, „Online Ranking“. |
| WP3-03 | Alle deutschen Texte der Seite *Spielen* lesen (gegenlesen). | Verständlich, richtig geschrieben, nichts abgeschnitten oder überlappend. Jede Abweichung mit Screenshot und Verbesserungsvorschlag notieren. |
| WP3-04 | Seite *Einstellungen* öffnen, Screenshot. | Nur der Kompatibilitätshinweis: Buch-Bild, drei Absätze deutscher Text, vollständig sichtbar, darunter „Bestätigen“. **Nicht** mehr da: „Magic Button“ (Repair CD-Keys, Reset the Game, Clear Registry), „Compatibility“, „Windows“, „DirectX“, „Advanced Settings“. |
| WP3-05 | „Bestätigen“ klicken; Text des Dialogs lesen; „OK“. | Dialog „Kompatibilitätsoptionen“ mit deutschem Text (zwei Sätze) und „OK“. Danach ist die Seite leer (siehe oben). Texte gegenlesen wie in WP3-03. |
| WP3-06 | Seite *Launcher* öffnen, Screenshot; Texte gegenlesen. | Überschrift „Launcher-Einstellungen“; „Design“ mit Liste (erster Eintrag „Eigene Datei...“); „Sprache“ mit „Windows-Sprache“ ausgewählt; „Empire-Earth-Ordner:“ mit „...“ und „Automatisch“, darunter die Herkunft (z. B. „Nicht gefunden, bitte den Ordner von Empire Earth.exe wählen“). **Nicht** mehr da: „Allow us to collect diagnostic data …“, „When starting the game“, „When closing the game“, „Associate Empire Earth Mod files …“. |
| WP3-07 | Liste „Sprache“ aufklappen. | Vier Einträge: „Windows-Sprache“, „English“, „Deutsch“, „Français“ (die Sprachen immer in ihrer eigenen Sprache). |
| WP3-08 | In der Liste „Design“ „Eigene Datei...“ wählen, Dateityp-Liste im Dialog ansehen, abbrechen. | Dateidialog mit „Design-Datei (*.xml)“ und „Alle Dateien (*.*)“; nach Abbrechen ist wieder das vorherige Design ausgewählt. |
| WP3-09 | „...“ neben dem Ordner klicken; einen Ordner **ohne** `Empire Earth.exe` wählen; Frage lesen, „Nein“. | Ordnerdialog mit „Wählen Sie den Empire-Earth-Ordner (den Ordner von Empire Earth.exe).“; dann „Dieser Ordner enthält kein Empire Earth.exe:“, der Ordner, „Trotzdem verwenden?“ mit Ja/Nein. Nach „Nein“ bleibt alles wie vorher. |
| WP3-10 | Sprache „English“ wählen. | Unter der Liste erscheint „Die neue Sprache gilt ab dem nächsten Start des Launchers.“; die Texte bleiben bis zum Neustart deutsch. Nach dem Schließen enthält `settings.json` `"UiCulture": "en"`. Wieder „Windows-Sprache“ gewählt: der Hinweis verschwindet. |
| WP3-11 | „English“ wählen, Launcher neu starten, alle drei Seiten ansehen (Screenshots), auf *Einstellungen* „Confirm“ klicken. | Alles englisch: „Play“, „Settings“, „Launcher“; „Game“, „Online Players (...)“, „Profile:“; Hinweis und „Confirm“, Dialog „Compatibility options“ mit „OK“; „Launcher Settings“, „Theme“, „Language“ („English“ ausgewählt), „Empire Earth folder:“, „Auto-detect“. `log.txt`: `UI language: en (launcher setting)`. |
| WP3-12 | „Français“ wählen, neu starten, alle drei Seiten ansehen (Screenshots). | Alles französisch: „Jouer“, „Paramètres“, „Launcher“; „Jeu“, „Joueurs en ligne (...)“, „Profil :“; „Confirmer“; „Paramètres du launcher“, „Thème“, „Langue“, „Dossier d'Empire Earth :“, „Détecter“. Französisch nicht gegenlesen (Review offen, `docs/TRANSLATING.md`); nur abgeschnittene oder überlappende Texte notieren. |
| WP3-13 | „Windows-Sprache“ wählen, neu starten. | Wieder deutsch wie in WP3-01; `settings.json`: `"UiCulture": ""`. |
| WP3-14 | Launcher schließen, in `settings.json` `"UiCulture": "es"` eintragen, Launcher starten, Seite *Launcher* ansehen. | Startet normal in der Windows-Sprache; „Sprache“ zeigt „Windows-Sprache“. `log.txt`: Warnung `The UI language "es" of the launcher settings is unknown, the Windows language is used.` Danach den Eintrag wieder auf `""` setzen. |
| WP3-15 | Im entpackten Ordner `Empire-Earth-Launcher\` nachsehen. Dann den Ordner `de` testweise in `de-weg` umbenennen, Launcher starten, danach zurück umbenennen. | Neben `Empire Earth Launcher.exe` liegen `de\` und `fr\` mit je `Empire Earth Launcher.resources.dll`. Ohne `de\` startet der Launcher normal und zeigt auf deutschem Windows Englisch. |
| WP3-16 | Skalierung 125 % oder 150 % (wie WP1-08), deutsche Oberfläche, Screenshot jeder Seite. Danach Skalierung zurückstellen. | Keine abgeschnittenen deutschen Texte, besonders „Empire-Earth-Ordner:“, „Automatisch“, „Profil:“, „Bestätigen“ und der Kompatibilitätshinweis. |
| WP3-17 | Nach allen Fällen `log.txt` durchsehen. | Keine `Unhandled exception`, keine `A background task failed`; Warnungen nur die aus WP3-14. |

### L-WP4 – Installationserkennung (Vertrag 1)

Der Launcher sucht beim Start im Hintergrund alle Installationen (Vertrag 1.4) und zeigt sie auf der Seite *Launcher*
in einer Liste (Produkt, Installationsordner, Empire-Earth-Ordner, Art, Zustand). Die Spalte „Art“ unterscheidet
„Community-Setup“ (Setup ab v2), „Älteres Setup“ (Community-Setup bis 1.7.2) und „Andere“ (CD, GOG, Kopie). Die Suche
liest nur; sie schreibt nichts in die Registry und keine Datei.

Vorbereitung: die offiziellen Community-Setups 1.7.2 (EE und NeoEE) von empireearth.eu; falls vorhanden ein Testbuild
des Setups v2 aus dem Setup-Repository; optional eine GOG- oder CD-Installation. Vor jeder Registry-Änderung (nur in
WP4-15) den betroffenen Schlüssel mit dem Registrierungs-Editor exportieren (Rechtsklick → Exportieren).
`Software\Sierra\CDKeys` nie ändern oder löschen. Die Fälle nennen die deutschen Texte; `log.txt` ist englisch.

| Fall | Schritte | Erwartet |
|---|---|---|
| WP4-01 | Nur wenn kein Empire Earth installiert ist (sonst „nicht geprüft“): Launcher starten, Seite *Launcher* und *Spielen* ansehen. | Liste „Gefundene Installationen:“ leer; unter dem Ordner „Nicht gefunden, bitte den Ordner von Empire Earth.exe wählen“. *Spielen*: „Keine Empire-Earth-Installation gefunden“. `log.txt`: `Warning : No Empire Earth installation found. Choose the game folder in the launcher settings.` |
| WP4-02 | Mit installiertem Spiel Launcher starten, sofort die Seite *Launcher* öffnen. | Das Fenster erscheint ohne Verzögerung; kurz steht „Suche nach Empire-Earth-Installationen ...“ (bei schnellen Rechnern kaum sichtbar), dann die Liste. „Automatisch“ ist während der Suche ausgegraut. `log.txt`: je Fund eine Zeile `Discovery: found the install root … through …`, je Installation `Discovery: installation …`, dann `Empire Earth folder: … (source …)`. |
| WP4-03 | NeoEE-Setup 1.7.2 „für alle Benutzer“ (Standardordner) installieren, Launcher starten. | Eine Zeile: Produkt „NeoEE“, Installationsordner `C:\Program Files (x86)\Neo Empire Earth`, Empire-Earth-Ordner `…\Neo Empire Earth\Empire Earth`, Art „Älteres Setup“, Zustand „OK“; Herkunft „Aus der Spielinstallation erkannt“. `log.txt`: Funde über `the uninstall key HKLM64\Software\Microsoft\Windows\CurrentVersion\Uninstall\{…}_is1` und über `the "Installed From" values of HKCU\Software\Neo\Empire Earth`; die Installationszeile enthält `NeoEE community-legacy (admin)` und `sources 3,4`. |
| WP4-04 | EE-Setup 1.7.2 „nur für mich“ installieren, Launcher neu starten. | Zusätzliche Zeile „Empire Earth“, Installationsordner unter `%LOCALAPPDATA%\Programs\…`, Art „Älteres Setup“. `log.txt`: Fund über `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\{…}_is1`, Installationszeile `EE community-legacy (user)`. Die NeoEE-Installation steht als erste Zeile (NeoEE vor EE). |
| WP4-05 | Nur mit einem Testbuild des Setups v2 (sonst „nicht geprüft“): installieren, Launcher neu starten. | Art „Community-Setup“; im Installationsordner gibt es `_setupdata_<Produkt>\install.ini`. `log.txt`: Fund über `the install record HKLM64\Software\Empire Earth Community\Installations\…` (bzw. `HKCU\…` bei „nur für mich“), Installationszeile mit `community`, `contract 1` und `sources 2,3,4`. |
| WP4-06 | Mit der Installation aus WP4-03: mit einem zweiten Windows-Konto (Standardbenutzer) anmelden, den Launcher-Ordner an einen für dieses Konto lesbaren Ort kopieren, Launcher starten. | Die NeoEE-Installation wird gefunden (über den Deinstallationsschlüssel in HKLM, `sources 3` bzw. `2,3` mit v2), obwohl dieses Konto keine „Installed From“-Werte hat. Die „nur für mich“-Installation aus WP4-04 fehlt; das ist richtig (sie gehört dem anderen Konto). |
| WP4-07 | Fremde Installation mit anderem Ordnernamen: den Ordner `Empire Earth` der EE-Installation aus WP4-04 nach `C:\Games\EE` kopieren. Auf der Seite *Launcher* mit „...“ `C:\Games\EE` wählen. | Keine Rückfrage. Neue Zeile mit Art „Andere“, Installationsordner `C:\Games`, Empire-Earth-Ordner `C:\Games\EE` (der echte Name, nicht `…\Empire Earth`), ausgewählt, oben in der Liste; Herkunft „Von Hand gewählt“. `settings.json`: `"GameDirectory": "C:\\Games\\EE"`. |
| WP4-08 | Nur mit GOG- oder CD-Installation (sonst „nicht geprüft“): das Spiel einmal starten und beenden, dann den Launcher starten. | Die Installation steht mit Art „Andere“ und ihren echten Ordnern in der Liste (Fund über `Software\SSSI\Empire Earth`). Ordnernamen und Zustand notieren; bei „Beschädigt“ den Tooltip der Spalte „Zustand“ abschreiben. |
| WP4-09 | Laufwerksstamm: den Ordner `Empire Earth` nach `D:\Empire Earth` kopieren (oder auf einen USB-Stick, z. B. `E:\Empire Earth`). Mit „...“ das Laufwerk `D:\` wählen, danach `D:\Empire Earth`. | Beide Male dieselbe Zeile: Installationsordner `D:\`, Empire-Earth-Ordner `D:\Empire Earth`, Art „Andere“. |
| WP4-10 | Mit der Installation aus WP4-03 nacheinander mit „...“ den Installationsordner, den Ordner `Empire Earth` und den Ordner `Empire Earth - The Art of Conquest` wählen; danach „Automatisch“. | Jedes Mal ist dieselbe Zeile ausgewählt, es entsteht keine zusätzliche Zeile, Herkunft „Von Hand gewählt“. Nach „Automatisch“: Herkunft „Aus der Spielinstallation erkannt“, in `settings.json` `"GameDirectory": ""`. |
| WP4-11 | Mit mindestens zwei Installationen eine andere Zeile der Liste anklicken; Launcher schließen und neu starten. | Die angeklickte Installation ist ausgewählt, das Textfeld zeigt ihren Empire-Earth-Ordner, Herkunft „Von Hand gewählt“; `settings.json` enthält diesen Empire-Earth-Ordner. Nach dem Neustart steht sie oben und ist ausgewählt. |
| WP4-12 | Zwei Installationen desselben Produkts, z. B. EE aus WP4-04 und die Kopie aus WP4-07. Seite *Launcher* ansehen. | Unter der Liste: „2 Installationen von Empire Earth verwenden dieselben Spieleinstellungen (HKCU\Software\SSSI\Empire Earth): Die Einstellungen, auch der dort gespeicherte Spielordner, gelten für alle.“ Der Hinweis ist vollständig lesbar (nicht abgeschnitten). |
| WP4-13 | In einer Installation `Empire Earth.exe` in `Empire Earth.exe.bak` umbenennen, auf der Seite *Launcher* „Automatisch“ klicken (bzw. Launcher neu starten). Danach zurück umbenennen. | Die Installation bleibt in der Liste mit Zustand „Beschädigt“; Tooltip „Fehlt: Empire Earth.exe“. Ist sie ausgewählt, steht darunter „Empire Earth.exe fehlt in … Antivirenprogramme löschen oder isolieren oft Spieldateien: …“. Nach dem Zurückbenennen und „Automatisch“ wieder „OK“. |
| WP4-14 | Die Kopie `C:\Games\EE` aus WP4-07 wählen, Launcher schließen, den Ordner in `C:\Games\EE-weg` umbenennen, Launcher starten. Danach zurück umbenennen. | Zeile mit Empire-Earth-Ordner `C:\Games\EE`, Zustand „Nicht gefunden“, weiter ausgewählt; Herkunft „Von Hand gewählt, aber der Ordner existiert nicht“. `log.txt`: `Warning : Discovery: the chosen folder C:\Games\EE does not exist; it stays selected.` |
| WP4-15 | Optional, nur mit Administratorrechten (Forenbericht Abschnitt 8, Testfall 8, „Schlüssel vor Hive“): zwei Kopien des Ordners `Empire Earth` anlegen, `C:\Games\EE` und `C:\Games\NeoCopy\Empire Earth` (diese aus einer NeoEE-Installation, mit `neoee.dll`). Vorher `HKCU\Software\SSSI\Empire Earth` und, falls vorhanden, `HKLM\SOFTWARE\WOW6432Node\Neo\Empire Earth` exportieren. Im Registrierungs-Editor als Zeichenfolgen setzen: in `HKCU\Software\SSSI\Empire Earth` `Installed From Volume` = `C:` und `Installed From Directory` = `\GAMES\EE\`; in `HKLM\SOFTWARE\WOW6432Node\Neo\Empire Earth` `Installed From Volume` = `C:` und `Installed From Directory` = `\GAMES\NEOCOPY\Empire Earth\`. Auf der Seite *Launcher* „Automatisch“ klicken. Danach die exportierten Dateien wieder importieren (Doppelklick) und einen vorher nicht vorhandenen Schlüssel `…\WOW6432Node\Neo\Empire Earth` löschen. | In der Liste steht die NeoEE-Kopie (Installationsordner `C:\GAMES\NEOCOPY`, Produkt „NeoEE“) vor der EE-Kopie (`C:\GAMES`), obwohl ihr Wert in HKLM und der andere in HKCU steht (der Schlüssel geht vor der Hive). Community-Installationen stehen davor, weil Deinstallationsschlüssel vor „Installed From“ kommen. `log.txt`: der Fund über `HKLM32\Software\Neo\Empire Earth` steht vor dem über `HKCU\Software\SSSI\Empire Earth`. |
| WP4-16 | VirtualStore: bei einer Installation unter `C:\Program Files (x86)` (z. B. WP4-03) nachsehen, ob `%LOCALAPPDATA%\VirtualStore\Program Files (x86)\Neo Empire Earth\Empire Earth\_wonlobbypersistent.dat` existiert (Explorer, Adresszeile). Falls ja: Launcher starten, Seite *Spielen*. | Ergebnis notieren (vorhanden/nicht vorhanden; Forenbericht Abschnitt 8, Testfall 1). Falls vorhanden: die Profile der Lobby erscheinen unter „Profil:“, `log.txt` enthält `The game uses the VirtualStore copy … of …`. |
| WP4-17 | Seite *Launcher* mit mindestens zwei Installationen auf Deutsch, Englisch und Französisch ansehen (Sprache wie in WP3-10 bis WP3-12), je ein Screenshot; einmal mit Skalierung 125 % oder 150 %. Tooltips der Spalten „Art“ und „Zustand“ ansehen. | Spaltenköpfe lesbar („Produkt“, „Installationsordner“, „Empire-Earth-Ordner“, „Art“, „Zustand“); lange Pfade sind abgekürzt, der Tooltip zeigt sie ganz; Liste und Hinweis überlappen nichts und sind nicht abgeschnitten. Deutsche Texte gegenlesen wie in WP3-03. |
| WP4-18 | Nach allen Fällen `log.txt` durchsehen. | Keine `Unhandled exception`, keine `A background task failed`, kein `Error` aus `Discovery:`; jede verworfene Fundstelle hat genau eine `Warning`-Zeile mit Grund. |

### L-WP5 – Spieleinstellungen (Vertrag 3)

Der Launcher richtet die empfohlenen Spieleinstellungen des Setups für das **eigene** Windows-Konto ein (Vertrag 3,
[ADR 0015](adr/0015-game-settings-target-folders-and-write-timing.md)): beim Start nur für eine Installation, die ihren
Einstellungsschlüssel allein nutzt (oder von Hand gewählt ist), „Installed From“ nur, wenn beide Werte fehlen, und nie,
solange ein Setup oder das Spiel läuft. Die Seite *Einstellungen* zeigt den Stand je Spiel, die Hinweise, „Empfohlene
Anzeige übernehmen“, „Spieleinstellungen zurücksetzen“ (mit `.reg`-Sicherung) und hinter dem Kompatibilitätshinweis die
Kompatibilitätsoptionen; die Seite *Spielen* zeigt unter der Spielauswahl eine Infoleiste (Rückfrage oder Hinweis).
WP3-04 und WP3-05 gelten ab jetzt nur noch für den Hinweis im Abschnitt „Kompatibilitätsoptionen“ der Seite.

Vorbereitung: vor jedem Fall die Schlüssel sichern (Abschnitt 1, `reg export` von `HKCU\Software\Neo`,
`HKCU\Software\SSSI`, `HKCU\Software\Mad Doc Software`, dazu `HKCU\Software\Empire Earth Community` und
`HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers`). Werte im Registrierungs-Editor ansehen
(`Win+R` → `regedit`). „Marker löschen“ heißt: unter `HKCU\Software\Empire Earth Community\GameDefaults\<Produkt>` den
Wert `EE` (bzw. `AoC`) löschen; dann gilt das Konto als neu. `Software\Sierra\CDKeys` nie ändern. Die Fälle nennen die
deutschen Texte.

| Fall | Schritte | Erwartet |
|---|---|---|
| WP5-01 | Eigenes Konto mit Installation aus WP4-03 oder WP4-05; Marker löschen; Launcher starten, Seite *Einstellungen*. | „Standardwerte des Spiels“: je Spiel „Die empfohlenen Einstellungen sind für Ihr Windows-Konto eingerichtet.“ Im Registrierungs-Editor `GameDefaults\NeoEE`: `EE` = 1 (und `AoC` = 1). Vorhandene Werte (Spielernamen, Lautstärke) unverändert. `log.txt`: `Game defaults: first run of NeoEE EE …` und je neu angelegtem Wert `Game defaults: created …`. |
| WP5-02 | Zweites Windows-Konto (Standardbenutzer), das das Spiel noch nie gestartet hat (Forenbericht Abschnitt 8, Testfälle 1 und 7): Launcher starten, Seite *Einstellungen* ansehen, Launcher schließen. Dann **The Art of Conquest über die Desktop-Verknüpfung** starten (nicht über den Launcher). | Im HKCU dieses Kontos stehen `Installed From Volume`/`Installed From Directory` für EE und AoC (Ordner der Installation, Pfad ohne Laufwerk in Großbuchstaben, Ordnername wie er ist), `Rasterizer Name`, `Game Window Width/Height`, Bit-Tiefen 32 und unter Windows 10/11 `UserGpuPreferences` für beide Programme (`GpuPreference=2;`, nur wenn das Setup mit „Kompatibilitätsmodus für ältere Windows-Versionen“ lief). AoC startet, ohne dass vorher Empire Earth gestartet wurde. |
| WP5-03 | Zwei Installationen desselben Produkts (WP4-12), keine von Hand gewählt; Marker löschen; Launcher starten. | Seite *Einstellungen*: „Noch nicht eingerichtet, weil sich mehrere Installationen diese Einstellungen teilen. …“; im Registrierungs-Editor hat sich nichts geändert (vorher/nachher exportieren und vergleichen). `log.txt`: `Game defaults: nothing written at the start for … share its game settings …`. Danach eine Installation in der Liste der Seite *Launcher* anklicken: nun wird sie eingerichtet. |
| WP5-04 | Marker löschen, `Game Bit Depth` (Zeichenfolge oder DWORD) auf 16 setzen; Launcher starten, Seite *Spielen*. Zuerst „Meine behalten“, dann Marker löschen, Launcher neu starten, „Übernehmen“. | Infoleiste unter der Spielauswahl: „Ihre Anzeigeeinstellungen weichen von den empfohlenen ab: Empire Earth Game Bit Depth 16 statt 32. …“. „Meine behalten“: Wert bleibt 16, Marker = 1, die Rückfrage kommt beim nächsten Start nicht wieder. „Übernehmen“: Ordner `Backups\…_display-settings\` mit einer `.reg`-Datei, `Game Bit Depth` = 32, Marker = 1. Das Spiel wurde dabei nie blockiert. |
| WP5-05 | `Rasterizer Name` auf `Direct3D` setzen (Installation ohne DirectX-Wrapper). Launcher starten; Seite *Spielen*, dann *Einstellungen*. Auf *Spielen* „Ausblenden“; Launcher neu starten; dann den Wert auf `Software` setzen und den Launcher neu starten. | Infoleiste und Liste „Hinweise“: „… Als Renderer ist „Direct3D“ eingestellt; empfohlen für diese Installation ist „Direct3D Hardware TnL“ …“. Nach „Ausblenden“ ist die Infoleiste leer, auch nach dem Neustart; auf *Einstellungen* steht der Hinweis weiter, Häkchen „Seite Spielen“ aus; `settings.json` enthält `HiddenHints` mit `RasterizerMismatch`. Mit dem geänderten Wert `Software` ist der Hinweis wieder in der Infoleiste. Danach den Wert auf `Direct3D Hardware TnL` zurücksetzen. |
| WP5-06 | Windows 10/11 (Forenbericht Abschnitt 8, Testfall 4): `Game Bit Depth` und `Texture Bit Depth` auf 16. Launcher starten, Seite *Einstellungen*; Spiel starten und notieren, ob es einfriert; dann „Empfohlene Anzeige übernehmen“. | Hinweis „16-Bit-Farben lassen das Spiel unter Windows 8 und neuer oft einfrieren …“. Ergebnis des Spielstarts notieren (Bezug t=10931, t=5848). Nach dem Knopf: „Erledigt. Ihre vorherigen Einstellungen sind gesichert in: …“, beide Werte 32, Hinweis weg. |
| WP5-07 | O4: Skalierung 150 % (Einstellungen → System → Bildschirm), anmelden neu. Auf *Einstellungen* den Hinweis bestätigen, „Keine Skalierung durch Windows (HIGHDPIAWARE)“ **aus**, „Empfohlene Anzeige übernehmen“. Spiel über die Verknüpfung starten, Fenster ansehen. Dann HIGHDPIAWARE **ein**, Spiel erneut starten. Danach HIGHDPIAWARE wieder ausschalten. | Ohne HIGHDPIAWARE: Hinweis „Das Spielfenster (…) passt nur mit der Kompatibilitätsoption HIGHDPIAWARE auf den Bildschirm …“ (kein Zurücksetzen-Rat); im Spiel ist das Fenster zu groß bzw. abgeschnitten (notieren). Mit HIGHDPIAWARE: Hinweis weg, Fenster passt. Beim Ausschalten erscheint vorher die Rückfrage „Der Bildschirm ist auf 150 % skaliert …“ mit „Ausschalten“/„Abbrechen“. Fensterwerte notieren und mit den Werten vergleichen, die das Setup bei 150 % schreibt (Setup-Testplan, O4). Skalierung danach zurückstellen. |
| WP5-08 | Einige Werte ändern (`Music Volume`, `Game Bit Depth`, `Game Options\Map Size`) und einen eigenen Wert anlegen (`Test` = 1). Seite *Einstellungen* → „Spieleinstellungen zurücksetzen“, Text lesen, „Jetzt zurücksetzen“. | Bestätigung auf der Seite nennt die Spiele und den Ordner `…\Empire Earth Launcher\Backups`. Danach „Erledigt. Ihre vorherigen Einstellungen sind gesichert in: …\Backups\<Datum>_reset-game-settings“; dort je Spiel eine `.reg`-Datei (`…_NeoEE_EE.reg`, `…_NeoEE_AoC.reg`). Die Tabellenwerte stehen auf den Empfehlungen, `Test` und Spielernamen unverändert, Marker = 1. |
| WP5-09 | Wiederherstellung: eine `.reg`-Datei aus WP5-08 doppelklicken, die Rückfrage des Registrierungs-Editors bestätigen. | Die Werte vor WP5-08 stehen wieder da (mit dem Export vor WP5-08 vergleichen); Werte, die der Reset neu angelegt hatte, sind wieder weg; der Marker ist wie vorher. Leere Schlüssel, die der Reset angelegt hatte, dürfen bleiben. |
| WP5-10 | Empire Earth starten (über die Verknüpfung), im Hauptmenü lassen; im Launcher „Spieleinstellungen zurücksetzen“ → „Jetzt zurücksetzen“ und einen Kompatibilitätsschalter umlegen. Danach das Spiel beenden. | „Nicht möglich, solange Empire Earth.exe läuft.“; kein neuer Sicherungsordner, keine Änderung in der Registry. `log.txt`: `Not allowed to reset the game settings now: Empire Earth.exe is running …`. |
| WP5-11 | Ein Community-Setup starten und auf der ersten Seite stehen lassen (nicht installieren); im Launcher „Empfohlene Anzeige übernehmen“. Setup abbrechen. | „Nicht möglich, solange das Setup von NeoEE läuft.“ (bzw. Empire Earth); nichts geändert. |
| WP5-12 | Windows 10/11, Seite *Einstellungen*, Kompatibilitätshinweis „Bestätigen“. Nacheinander jede der vier Optionen ein- und wieder ausschalten; nach dem Einschalten von HIGHDPIAWARE Rechtsklick auf `Empire Earth.exe` → Eigenschaften → Kompatibilität. | Vier Schalter (DWM8And16BitMitigation, HIGHDPIAWARE, HeapClearAllocation, WIN7RTM), Text „Die Optionen gelten für Empire Earth.exe, EE-AOC.exe und Ihr Windows-Konto.“ In `HKCU\…\AppCompatFlags\Layers` steht je Programm z. B. `~ HIGHDPIAWARE`; andere Einträge (z. B. ein selbst gesetzter Haken im Eigenschaften-Dialog) bleiben erhalten; nach dem Ausschalten ohne weitere Einträge ist der Wert gelöscht. Nie `WINXPSP3` oder `RUNASADMIN`. Der Eigenschaften-Dialog zeigt die Einstellung. |
| WP5-13 | Installation „für alle Benutzer“ mit Setup v2 und Standardaufgaben (HKLM-Wert `~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WIN7RTM`), Seite *Einstellungen* nach „Bestätigen“. | Unter den Schaltern „Vom Setup für alle Benutzer gesetzt (nur das Setup ändert das): …“ mit dem HKLM-Wert; WIN7RTM ist ausgegraut, dazu „Ein Windows-Kompatibilitätsmodus ist schon gesetzt …“. In HKLM ändert der Launcher nie etwas. |
| WP5-14 | Nur in der Windows-7-VM (Abschnitt 6) mit einer Installation von Setup 1.7.2: Seite *Einstellungen* nach „Bestätigen“. Falls `HKCU\…\Layers` für ein Programm genau `~ RUNASADMIN` enthält: „„Als Administrator ausführen“ entfernen“. Danach den Snapshot zurücksetzen, das aktuelle Setup mit der freiwilligen Aufgabe `compatibility_legacy` installieren (wie Setup-Fall TP-20) und die Seite erneut öffnen. | Keine Schalter, stattdessen „Unter Windows 7 und unter Wine bietet der Launcher keine Kompatibilitätsoptionen an …“; alte Werte (z. B. `~ RUNASADMIN WINXPSP3`) stehen schreibgeschützt da mit dem Rat, das aktuelle Community-Setup auszuführen. Nach dem Knopf ist nur der Wert `~ RUNASADMIN` gelöscht, Sicherung unter `Backups\…_remove-runasadmin\`. Mit `compatibility_legacy`: kein Hinweis auf alte Werte, obwohl `~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation` gesetzt ist (Vertrag 3.7, Revision 2). |
| WP5-15 | Bildschirm unter 768 Pixel Höhe (Forenbericht Abschnitt 8, Testfall 6), z. B. Auflösung 1280×720 oder 1024×600 einstellen; Launcher starten. | Hinweis „Der Bildschirm ist nur 720 Pixel hoch; die Menüs des Spiels brauchen mindestens 768 …“ auf *Einstellungen* und in der Infoleiste. Nach „Zurücksetzen“: Fenster 1280×768 (Breite des Bildschirms, Höhe mindestens 768). Auflösung zurückstellen. |
| WP5-16 | Netzwerkpfad: den Ordner `Empire Earth` auf eine Freigabe kopieren, auf der Seite *Launcher* mit „...“ als `\\Rechner\Freigabe\Empire Earth` wählen (nicht als Laufwerk). | Hinweis „… liegt nicht auf einem Laufwerksbuchstaben, daher können die Werte „Installed From“ nicht auf ihn zeigen …“; „Installed From“ wird nicht geschrieben (`log.txt`: Warnung `is not on a drive letter`). Danach wieder „Automatisch“. |
| WP5-17 | Seiten *Einstellungen* (vor und nach „Bestätigen“) und *Spielen* (mit Infoleiste) auf Deutsch, Englisch und Französisch, je einmal bei 100 % und 150 %; Screenshots. Bis zum Ende der Seite scrollen. | Keine abgeschnittenen oder überlappenden Texte; lange Texte brechen um und schieben die folgenden Elemente nach unten; die Seite scrollt. Deutsche Texte gegenlesen wie in WP3-03. |
| WP5-18 | Windows 10/11 mit Setup-Aufgabe „Kompatibilitätsmodus für ältere Windows-Versionen“: Einstellungen → System → Bildschirm → Grafik. | `Empire Earth.exe` und `EE-AOC.exe` stehen mit „Hohe Leistung“ in der Liste (Vertrag 3.4). |
| WP5-19 | Spielordner mit Umlauten (Forenbericht Abschnitt 8, Testfall 20): die Installation nach `C:\Spiele\Ägypten\Empire Earth` kopieren, auf der Seite *Launcher* wählen; Marker löschen; Launcher neu starten; danach AoC über eine Verknüpfung in diesem Ordner starten. Dann die Installation zusätzlich nach `C:\Spiele\Ελλάδα\Empire Earth` kopieren (griechische Buchstaben, nicht in der Codepage 1252 eines deutschen oder französischen Windows) und auf der Seite *Launcher* wählen. | `Installed From Directory` = `\SPIELE\ÄGYPTEN\Empire Earth\`; ein vom Setup geschriebenes `\SPIELE\äGYPTEN\…` wird nicht umgeschrieben (gleicher Ordner); für `Ägypten` kein Hinweis zum Ordnernamen. Ob das Spiel startet, notieren (ANSI-Pfad). Für `Ελλάδα`: auf *Einstellungen* und in der Infoleiste „Empire Earth: Der Ordner C:\Spiele\Ελλάδα\Empire Earth enthält Zeichen, die Windows für Nicht-Unicode-Programme wie das Spiel nicht kennt …“; ob das Spiel dort startet, notieren. |
| WP5-20 | Nach allen Fällen `log.txt` durchsehen. | Keine `Unhandled exception`, keine `A background task failed`; jede Änderung hat eine Zeile `Game defaults: created/changed …` bzw. `Compatibility: changed …` mit altem und neuem Wert; jede Sicherung `Backup written: …`. |

### L-WP6 – Spielstart (Vertrag 3.7 und 4.2)

Die Seite *Spielen* startet jetzt Empire Earth oder The Art of Conquest ([ADR 0010](adr/0010-game-start-and-mutex-probing.md)):
Unter der Spielauswahl stehen die Dateiversionen beider Programme und eine Statuszeile. Vor jedem Start prüft der
Launcher in dieser Reihenfolge: läuft ein Setup, läuft dasselbe Spiel, läuft das andere Spiel (Rückfrage), gibt es das
Programm, dann gleicht er „Installed From“ ab, richtet beim ersten Start die Standardwerte ein und startet das Programm
über die Windows-Shell im echten Spielordner. Fehlt das Programm, erscheint das Fenster „Installation reparieren“ mit
den Schritten aus Vertrag 4.4 und der Downloadseite. Ab diesem Paket enthält das Laptop-Paket den Ordner `Tests\`;
WP1-11 ist damit prüfbar. Vorbereitung wie bei L-WP5 (Registry-Schlüssel sichern). Die Fälle nennen die deutschen
Texte.

| Fall | Schritte | Erwartet |
|---|---|---|
| WP6-01 | Installation mit AoC (z. B. aus WP4-03 oder WP4-05). Seite *Spielen*: „Empire Earth“ wählen, „Spielen“; Spiel beenden. Dann „The Art of Conquest“ wählen, „Spielen“; Spiel beenden. | Beide Spiele starten. `log.txt` je Start eine Zeile `Game started: NeoEE <Installationsordner>, game EE, program …\Empire Earth\Empire Earth.exe, pid <Zahl>` (bzw. `game AoC`, `…\EE-AOC.exe`); steht dort `pid unknown`, notieren (kein Fehler). Statuszeile „Empire Earth wurde gestartet.“ bzw. „The Art of Conquest wurde gestartet.“ Im Task-Manager (Details, Spalte „Befehlszeile“ einblenden) steht das Programm ohne Argumente. |
| WP6-02 | Forenbericht Abschnitt 8 Zeile 1: Seite *Spielen* ansehen; dann im Explorer Rechtsklick auf `Empire Earth.exe` → Eigenschaften → Details, ebenso für `EE-AOC.exe`. | Unter der Spielauswahl „Empire Earth.exe: Version …“ und „EE-AOC.exe: Version …“ mit denselben Zahlen wie „Dateiversion“ im Explorer. Werte notieren (Bezug Forum: EEC 2.00.2949, AoC 1.00.2473, t=11034 p=47982). Ein Programm ohne Versionsangabe zeigt „keine Versionsangabe“. |
| WP6-03 | „The Art of Conquest“ wählen, Launcher schließen und neu starten. Dann auf der Seite *Launcher* einen Ordner mit einer Installation ohne AoC wählen (z. B. eine Kopie nur des Ordners `Empire Earth`). | Nach dem Neustart ist „The Art of Conquest“ wieder gewählt; `settings.json` enthält `"LastGame": "AoC"`. Bei der Installation ohne AoC ist „The Art of Conquest“ ausgegraut und „Empire Earth“ gewählt. Danach wieder „Automatisch“. |
| WP6-04 | Forenbericht Abschnitt 8 Testfall 7, AoC ohne vorherigen EE-Start: zweites Windows-Konto oder eigenes Konto nach dem Löschen von `Installed From Volume`/`Installed From Directory` unter dem AoC-Schlüssel (`HKCU\Software\Neo\Art of Conquest` bzw. `HKCU\Software\Mad Doc Software\EE-AOC`) und des Markers. Launcher starten, „The Art of Conquest“ wählen, „Spielen“. | AoC startet und kommt ins Hauptmenü, ohne dass vorher Empire Earth gestartet wurde. Die beiden Werte stehen vor dem Start wieder da (Pfad des AoC-Ordners ohne Laufwerk, Elternordner in Großbuchstaben). `log.txt`: `Game defaults: created …` bzw. `changed …` für „Installed From“ vor `Game started:`. |
| WP6-05 | Setup läuft (Vertrag 4.2): Launcher geöffnet, Seite *Spielen*. Ein Community-Setup starten und auf der ersten Seite stehen lassen (nicht installieren). Seiten *Spielen* und *Einstellungen* ansehen. Dann das Setup abbrechen. | Spätestens nach 2 Sekunden: „Spielen“ ist ausgegraut, die Statuszeile sagt „Das Setup von NeoEE läuft. Bis es beendet ist, startet der Launcher kein Spiel und ändert keine Spieleinstellungen.“ (bzw. Empire Earth); auf *Einstellungen* steht derselbe Satz und alle Schaltflächen und Kompatibilitätsschalter sind ausgegraut. Nach dem Abbrechen sind sie nach spätestens 2 Sekunden wieder bedienbar. `log.txt`: `The NeoEE setup is running (mutex NeoEE_Setup) …` und `The NeoEE setup has ended (seen for … s); the installations are searched again.`, danach die Suche. |
| WP6-06 | Launcher schließen, Community-Setup starten und auf der ersten Seite lassen, dann den Launcher starten. Nach dem Ansehen das Setup abbrechen. | Solange das Setup läuft: Seite *Launcher* und *Spielen* zeigen „Ein Setup läuft. Die Installationen werden gesucht, sobald es beendet ist.“; `log.txt`: `The installations are searched when the … setup has ended (install.ini is not read while a setup runs, contract 4.2).` Nach dem Abbrechen erscheinen die Installationen ohne weiteres Zutun. |
| WP6-07 | Forenbericht Abschnitt 8 Testfall 18: Empire Earth über den Launcher starten und im Hauptmenü lassen (Alt+Tab zurück zum Launcher), noch einmal „Empire Earth“ → „Spielen“. | Meldung „Empire Earth.exe läuft bereits. Der Launcher startet es kein zweites Mal.“ mit dem Hinweis auf den Task-Manager (Registerkarte „Details“, „Task beenden“), weil ein Prozess `Empire Earth.exe` existiert. Kein zweites Spiel; der Launcher beendet nichts. `log.txt`: `Game start refused: Empire Earth.exe is already running (mutex StainlessSteelStudiosPresentsEmpireEarth); a process Empire Earth.exe exists, it may hang.` |
| WP6-08 | Forenbericht Abschnitt 8 Testfall 18 (hängendes `Empire Earth.exe`, t=5859): Empire Earth läuft wie in WP6-07; „The Art of Conquest“ wählen, „Spielen“, zuerst „Nein“, dann noch einmal „Spielen“ und „Ja“. | Rückfrage „Empire Earth.exe läuft. The Art of Conquest trotzdem starten? Beide Spiele gleichzeitig können instabil laufen.“; „Nein“ startet nichts, „Ja“ startet AoC. `log.txt`: `Game started: … game AoC, program …\EE-AOC.exe, pid … (Empire Earth.exe is running, the player started anyway).` und davor die Warnung `Game start of EE-AOC.exe without synchronized "Installed From" values: Blocked …` (solange ein Spiel läuft, ändert der Launcher keine Spieleinstellungen, ADR 0016). Ergebnis im Spiel notieren. |
| WP6-09 | Kompatibilitätsebene `RUNASADMIN` (Vertrag 3.7): Rechtsklick auf `Empire Earth.exe` → Eigenschaften → Kompatibilität → „Programm als Administrator ausführen“ ankreuzen (nur für das eigene Konto), OK. Im Launcher „Spielen“, die UAC-Abfrage mit „Nein“ beantworten; dann noch einmal „Spielen“ und „Ja“. Danach das Häkchen wieder entfernen. | Windows zeigt die UAC-Abfrage (kein Fehler 740, kein „Der angeforderte Vorgang erfordert erhöhte Rechte“). Bei „Nein“: Meldung „Das Spiel wurde nicht gestartet, weil die Abfrage nach Administratorrechten abgebrochen wurde …“; `log.txt`: `… cancelled: the elevation prompt was not confirmed (error 1223).` Bei „Ja“ startet das Spiel (Task-Manager, Spalte „Mit erhöhten Rechten“: Ja). Der Launcher selbst bleibt ohne erhöhte Rechte. |
| WP6-10 | Programm fehlt: die Installation in einen eigenen Ordner kopieren (z. B. `C:\Spiele\EE-Test`), dort `Empire Earth.exe` in `Empire Earth.exe.bak` umbenennen, den Ordner auf der Seite *Launcher* wählen; Seite *Spielen* → „Spielen“. Im Fenster „Downloadseite öffnen“, dann „Schließen“. Danach umbenennen rückgängig machen und „Automatisch“. | Fenster „Installation reparieren“: „…\Empire Earth.exe fehlt. Die Installation ist beschädigt …“, darunter nummeriert zuerst „Fügen Sie zuerst in Ihrem Antivirenprogramm eine Ausnahme für den Ordner … hinzu …“, dann die Schritte zum Setup (bei einer Kopie ohne `install.ini` stattdessen „… stammt nicht vom Community-Setup …“), die Adresse `https://empireearth.eu/download`. „Downloadseite öffnen“ öffnet sie im Standardbrowser, ohne UAC-Abfrage (Task-Manager: Browser nicht „Mit erhöhten Rechten“); das Fenster bleibt offen. Auf *Spielen*: „Empire Earth.exe: fehlt“. `log.txt`: `Game start refused: the program … is missing …; repair advice: …` und `Repair advice for …: opening the download page https://empireearth.eu/download (the fixed page of contract 4.3 …)`. |
| WP6-11 | Forenbericht Abschnitt 8 Testfall 22: NeoEE-Installation „für alle Benutzer“ (Setup v2), als Administrator `EE-AOC.exe` umbenennen; im Launcher „The Art of Conquest“ → „Spielen“. Danach zurück umbenennen. Zusatz (Setup-Testplan TP-73): das Setup nach den Schritten erneut ausführen. | Die Schritte nennen den Installationsordner, „wählen Sie wieder „Installation für alle Benutzer““ und „Lassen Sie die Aufgabe „NeoEE-CD-Keys registrieren“ ausgewählt …“. Das Setup bietet die Reparatur an und läuft durch; danach startet AoC wieder über den Launcher. |
| WP6-12 | Forenbericht Abschnitt 8 Testfall 20, Spielerliste ohne Netz: Launcher starten, Spielerliste abwarten; Netzwerk trennen (Flugmodus oder Kabel ziehen), 2 Minuten warten; Netzwerk wieder verbinden, 1 Minute warten; Launcher schließen. | Ohne Netz: Überschrift „Online-Spieler (nicht verfügbar)“, das Fenster bleibt bedienbar. Mit Netz wieder die Liste. `log.txt` enthält für die Unterbrechung genau eine Zeile `Error : The online player list of … is unavailable, retrying every … ms.` und danach genau eine `The online player list is available again.` Nach dem Schließen ist der Launcher-Prozess sofort weg. |
| WP6-13 | Launcher starten und offen lassen; noch einmal `Empire Earth Launcher.exe` starten (auch aus einer Kopie des Ordners). | Meldung „Der Empire Earth Launcher läuft bereits. Bitte verwenden Sie das geöffnete Fenster …“, danach endet der zweite Start; es bleibt ein Launcher-Fenster. `log.txt`: `Another Empire Earth Launcher is already running (mutex EmpireEarthCommunityLauncher); this one ends.` |
| WP6-14 | Seite *Spielen* (mit Versionen, Statuszeile, Rückfrage aus WP6-08) und das Fenster aus WP6-10 auf Deutsch, Englisch und Französisch, je bei 100 % und 150 %; Screenshots. | Texte vollständig lesbar, nichts abgeschnitten; das Reparaturfenster wächst mit dem Text. Deutsche Texte gegenlesen wie in WP3-03. |
| WP6-15 | Nach allen Fällen `log.txt` durchsehen. | Keine `Unhandled exception`, keine `A background task failed`, keine `A handler of the online player list failed`; jeder Start hat eine Zeile `Game started:` oder `Game start refused:`. |
| WP6-16 | Fehlerbericht vom 03.10.2026 (rote X auf der Seite *Spielen*), ohne Spiel nachgestellt: Launcher auf der Seite *Spielen* offen lassen; in Windows eine Einstellung ändern und wieder zurückstellen, z. B. Einstellungen → Personalisierung → Taskleiste → „Taskleiste automatisch ausblenden“ an und aus (oder die Akzentfarbe, oder Hell/Dunkel). Danach die Seiten *Spielen*, *Einstellungen*, *Werkzeuge* und *Launcher* ansehen; auf *Einstellungen* „Bestätigen“ klicken. | Kein Fehlerdialog „Ein unerwarteter Fehler ist aufgetreten …“, nirgends ein rotes Kreuz (rotes X auf weißem Grund); die Zeilen der Gruppe „Spiel“ (Versionen, Versionsprüfung, Integrität, Statuszeile) und alle Texte der anderen Seiten sind lesbar und so groß wie vorher. `log.txt`: keine `Unhandled exception` und keine Warnung `The text … could not be drawn with its font …` (erscheint sie doch, die Zeile mit der Ausnahme notieren: der Text wurde dann mit der Standardschrift gezeichnet, kein Absturz, aber ein anderer Grund als der bekannte). |
| WP6-17 | Fehlerbericht vom 03.10.2026 wie beschrieben: Empire Earth über den Launcher im Vollbild starten (am besten mit einer Spielauflösung, die von der des Bildschirms abweicht, z. B. 1920×1080 auf 2560×1440), im Hauptmenü mit Alt+Tab zum Launcher zurück, eine Minute warten, zurück ins Spiel, das Spiel beenden. | Die Seite *Spielen* zeigt ihre vier Statuszeilen, kein Fehlerdialog, kein rotes Kreuz – während das Spiel läuft und nach dem Beenden. `log.txt` wie in WP6-16. |

### L-WP7 – Integrität und Reparatur (Vertrag 2 und 4)

Der Launcher prüft nach jeder Suche der Installationen (beim Start und nach einem Setup) im Hintergrund die Dateien der
gewählten Installation gegen die Liste, die das Community-Setup seit v2 schreibt (`_setupdata_<Produkt>\files.sha256`):
Alle Dateien müssen da sein, die Programmdateien werden gehasht (schnelle Prüfung). Auf der Seite *Spielen* steht das
Ergebnis unter den Versionen („Dateien: OK“ usw.), daneben „Details“ (Seite *Werkzeuge*) oder „Reparieren ...“. Die neue
Seite *Werkzeuge* zeigt die Erklärung und alle betroffenen Dateien, startet die vollständige Prüfung (auch die Spieldaten)
mit Fortschritt und Abbruch, öffnet die Reparatur-Hinweise und fragt nach Updates. Das Fenster „Installation reparieren“
fragt jetzt zuerst `api.empireearth.eu` nach dem Download des aktuellen Setups. Der Launcher liest die Spieldateien nur;
er ändert, löscht, verschiebt oder lädt keine. Voraussetzung: eine Installation mit dem Community-Setup v2 (Setup-Testplan
TP-40, TP-50); für WP7-07 eine mit Setup 1.7.2, für WP7-08 eine CD- oder GOG-Installation oder eine Kopie. Für Fälle, die
Dateien umbenennen, Adminrechte im Explorer; danach immer zurückbenennen.

| Fall | Schritte | Erwartet |
|---|---|---|
| WP7-01 | Installation mit dem Community-Setup v2, Launcher starten, Seite *Spielen*; dann Seite *Werkzeuge*. | Das Fenster ist sofort bedienbar; unter den Versionen kurz „Dateien: werden geprüft ...“, dann „Dateien: OK“ mit „Details“. *Werkzeuge*: „Alle … Dateien aus der Liste des Setups sind vorhanden, und die … Programmdateien sind unverändert (schnelle Prüfung).“ `log.txt`: `Integrity: quick check of <Installationsordner>: Ok, … files listed, … hashed, 0 findings.` Zeitabstand zur Zeile `Discovery: … installation(s) found` notieren. |
| WP7-02 | Forenbericht Abschnitt 8 Zeile 11 und Testfall 14 (Antivirus löscht Dateien): als Administrator im Explorer eine Programmdatei der Liste umbenennen, z. B. `Empire Earth\neoee.dll` (NeoEE) oder eine andere `.dll` aus `_setupdata_<Produkt>\files.sha256`, in `<Name>.bak`; dann eine Datei aus `Empire Earth\Data\` (keine `.exe`/`.dll`) ebenso. Launcher starten, „Reparieren ...“, „Downloadseite öffnen“, Fenster schließen. Danach beide Dateien zurückbenennen und den Launcher neu starten. | „Dateien: beschädigt“ mit „Reparieren ...“. Fenster „Installation reparieren“: „Dateien der Installation fehlen oder sind beschädigt. Antivirenprogramme löschen Spieldateien oft oder verschieben sie in die Quarantäne.“, darunter beide Dateien mit „fehlt“ (die Programmdatei zuerst), dann nummeriert zuerst „Fügen Sie zuerst in Ihrem Antivirenprogramm eine Ausnahme für den Ordner … hinzu …“, dann die Setup-Schritte. *Werkzeuge* listet dieselben Dateien. Nur die Datendatei umbenannt: „Dateien: unvollständig“. `log.txt` je Datei genau eine Zeile `Integrity finding in …: Empire Earth/neoee.dll (code): missing; expected <Hash>, actual none (missing).` Nach dem Zurückbenennen wieder „Dateien: OK“. Das Spiel startet auch im beschädigten Zustand, wenn `Empire Earth.exe` da ist. |
| WP7-03 | *Werkzeuge* → „Alle Dateien prüfen“; nach einigen Sekunden „Prüfung abbrechen“; dann noch einmal „Alle Dateien prüfen“ und bis zum Ende laufen lassen. Dauer notieren (SSD/HDD). | Fortschrittsbalken und „Prüfung: … von … Dateien“; während der Prüfung ist „Alle Dateien prüfen“ ausgegraut, das Fenster bleibt bedienbar und „Spielen“ funktioniert. Nach dem Abbrechen „Die Prüfung wurde abgebrochen.“ und „Dateien: Prüfung abgebrochen“ auf *Spielen*. Am Ende „Alle … Dateien aus der Liste des Setups sind vorhanden, und die … mit ihrer Prüfsumme verglichenen Dateien sind unverändert (vollständige Prüfung).“ `log.txt`: `Integrity: the full check of … was started by the user.`, beim Abbruch `… was cancelled after … of … files (cancelled); its findings are dropped.` |
| WP7-04 | ADR 0016 (Planprüfung), Vertrag 4.2: *Werkzeuge* → „Alle Dateien prüfen“ und sofort danach das Community-Setup starten und als Reparatur durchlaufen lassen (gleicher Ordner, gleicher Modus). | Spätestens 2 Sekunden nach dem ersten Setup-Fenster: „Die Prüfung wurde abgebrochen, weil ein Setup gestartet wurde. Sie läuft erneut, sobald das Setup beendet ist.“ Das Setup läuft ohne Fehlerdialog durch (keine Meldung „DeleteFile failed; code 32“ oder „Der Prozess kann nicht auf die Datei zugreifen“). Nach dem Setup sucht der Launcher die Installationen neu und prüft schnell: „Dateien: OK“. `log.txt`: `Integrity: the NeoEE setup started; the running full check is cancelled (contract 4.2).` (bzw. EE), danach `The … setup has ended …` und eine neue Zeile `Integrity: quick check of …`. |
| WP7-05 | Vertrag O6: Empire Earth und The Art of Conquest je einmal über den Launcher spielen (Hauptmenü, ein kurzes Gefecht gegen den Computer, speichern; bei NeoEE auch in die Lobby einloggen), beenden; dann *Werkzeuge* → „Alle Dateien prüfen“. | Ergebnis notieren: erwartet „Dateien: OK“ oder „Dateien: OK, Spieldaten geändert“. Jede Datei, die *Werkzeuge* dann listet, mit Pfad notieren (Antwort auf O6: welche installierten Dateien außer `cfg ini conf config log` ändert das Spiel?). Geänderte `.cfg`/`.ini` erscheinen nie. |
| WP7-06 | Vertrag 2.6 und O2 (nur NeoEE, optional): nach dem Spielen prüfen, ob der NeoEE-Updater (`NeoEE Updater\NeoEEUp.exe`) etwas aktualisiert hat; falls ja, Launcher neu starten. | Falls nur NeoEE-Programmdateien abweichen: „Dateien: seit der Installation geändert“ und auf *Werkzeuge* „Programmdateien haben sich seit der Installation geändert. Der NeoEE-Updater kann NeoEE-Dateien ersetzen …“ mit „…: seit der Installation geändert“. Betroffene Dateien notieren (Antwort auf O2). |
| WP7-07 | Installation mit dem Community-Setup 1.7.2 (ohne `_setupdata_<Produkt>\files.sha256`) wählen; Seite *Spielen*, „Details“, *Werkzeuge*. | Auf *Spielen* nur „Dateien: keine Prüfung (älteres Setup)“ mit „Details“; es öffnet sich kein Fenster von selbst. *Werkzeuge*: „Mit dem Community-Setup 1.7.2 oder älter installiert, das keine Liste der Dateien schreibt. Führen Sie das aktuelle Setup aus, um die Prüfung zu ermöglichen.“ „Reparatur-Hinweise“ zeigt die Setup-Schritte ohne Dateiliste. |
| WP7-08 | Eine CD- oder GOG-Installation oder eine Kopie ohne `_setupdata_…` auf der Seite *Launcher* wählen; Seiten *Spielen* und *Werkzeuge*. Danach wieder „Automatisch“. | Auf *Spielen* keine Zeile „Dateien: …“ und kein Knopf daneben (Vertrag 2.5: keine Prüfung, keine Meldung). *Werkzeuge*: „Nicht geprüft: Diese Installation stammt nicht vom Community-Setup …“, „Alle Dateien prüfen“ ausgegraut. |
| WP7-09 | Vertrag 2.5, älteres Setup danach (nur Installation „für alle Benutzer“): Uninstall-Schlüssel sichern: `reg export "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{<AppId>}_is1" "%USERPROFILE%\Desktop\uninstall.reg"` (AppId aus `install.ini`); dann als Administrator im Registrierungs-Editor den Wert `Empire Earth Community: ContractVersion` dieses Schlüssels löschen; Launcher starten. Danach `uninstall.reg` per Doppelklick wieder einspielen und den Launcher neu starten. | „Dateien: nicht prüfbar“ mit „Reparieren ...“; *Werkzeuge*: „Nach dem aktuellen Setup lief ein älteres Setup, oder das letzte Setup konnte seine Einträge nicht ersetzen. Führen Sie das aktuelle Setup aus.“ `log.txt`: `… is unknown (OlderSetupRanAfter): the uninstall key … lacks Empire Earth Community: ContractVersion …`. Nach dem Einspielen wieder „Dateien: OK“. |
| WP7-10 | Forenbericht Abschnitt 8 Zeile 1 und Testfall 2 (Versionsanzeige): Seite *Spielen* → „Version prüfen“. Danach dasselbe ohne Netz (Flugmodus). | Kurz „Der Update-Server wird gefragt ...“, dann „Spielversion …: aktuell.“ oder „Spielversion …: Version … ist verfügbar.“ – bei einer neueren Version öffnet sich „Installation reparieren“ mit dieser Zeile und den Setup-Schritten. Ohne Netz: „Der Update-Server konnte nicht gefragt werden (keine Verbindung); Einzelheiten im Protokoll.“ `log.txt`: `Update API: GET https://api.empireearth.eu/setup/?product=<AppId>&type=game&version=<Version>: HTTP 200 in … ms.` und `Version check: Game version … of …: UpToDate` (bzw. `UpdateAvailable`, `Failed (NetworkError)`). Antwort notieren. |
| WP7-11 | *Werkzeuge* → „Nach Updates suchen“ für eine Installation des Setups v2, eine des Setups 1.7.2 und eine fremde. | v2 und 1.7.2: je eine Zeile für die Spielversion und die Setup-Version (beim Setup 1.7.2 mit AppId und Versionen aus dem Uninstall-Schlüssel). Fremd: „Keine Versionsprüfung: Prüfen lassen sich nur Installationen des Community-Setups mit eingetragener Version.“ und keine Zeile `Update API:` im Protokoll. |
| WP7-12 | Vertrag 4.3: *Werkzeuge* → „Reparatur-Hinweise“, „Downloadseite öffnen“; danach dasselbe ohne Netz. | Zuerst „Der Update-Server wird nach dem aktuellen Setup gefragt ...“ und „Downloadseite öffnen“ ausgegraut, nach höchstens 10 Sekunden „Downloadseite des Community-Setups:“ mit der Adresse aus der Antwort (`https://` auf `empireearth.eu`, `neoee.net` oder `github.com/EE-modders/…`) oder `https://empireearth.eu/download` mit dem Hinweis „Keine Adresse vom Update-Server (…); dies ist die allgemeine Downloadseite.“ Ohne Netz: „(keine Verbindung)“. „Downloadseite öffnen“ öffnet sie ohne UAC-Abfrage. `log.txt`: `Repair: the update API names the setup download …` bzw. `Repair: the fixed download page https://empireearth.eu/download is used (<Grund>: …).` |
| WP7-13 | Vertrag O11 (wenn aus WP4-12 vorhanden: EE und NeoEE im selben Ordner): Seiten *Spielen* und *Werkzeuge*. | Der Zustand endet mit „(unzuverlässig)“; *Werkzeuge* zusätzlich „Empire Earth und NeoEE sind im selben Ordner installiert: Das Setup von … kann Dateien von … ersetzt haben, daher ist diese Prüfung unzuverlässig.“ |
| WP7-14 | Seiten *Spielen* (Zustand, Versionsprüfung, Infoleiste mit der Anzeigefrage aus WP5-02) und *Werkzeuge* (mit Dateiliste und während der vollständigen Prüfung) sowie das Fenster aus WP7-02 auf Deutsch, Englisch und Französisch, je bei 100 % und 150 %; Screenshots. Die Navigation hat jetzt vier Knöpfe: *Spielen*, *Einstellungen*, *Werkzeuge*, *Launcher*. | Nichts abgeschnitten oder überlappend; die Infoleiste auf *Spielen* ist 20 Pixel niedriger als in L-WP6, ihr Text muss trotzdem vollständig lesbar sein; *Werkzeuge* scrollt bei langen Texten. Deutsche Texte gegenlesen wie in WP3-03. |
| WP7-15 | Nach allen Fällen `log.txt` durchsehen. | Keine `Unhandled exception`, keine `A background task failed`, keine `The integrity check of … failed`; jede Prüfung hat genau eine Zeile `Integrity: quick check of …` bzw. `full check of …`; jede Anfrage eine Zeile `Update API: GET …` mit Status oder Fehler; unter Windows 10/11 beim Start `TLS: the versions Windows chooses (SystemDefault).` |

### L-WP8 – Wartungswerkzeuge

Die Seite *Werkzeuge* hat unter „Dateien der Installation“ und „Updates“ sechs neue Abschnitte: „Alte Registry-Einträge“
(R5), „WON-Login“ (R6), „VirtualStore“ (R8), „Spielstände und Szenarien“ (R10), „Spielernamen“ und „Sicherungen“ mit
„Sicherungsordner öffnen“. Was sie anzeigen, prüft der Launcher nach jeder Suche der Installationen und nach jeder
Aktion (nur lesend); zum erneuten Prüfen nach einer Änderung von außen den Launcher neu starten. Schreibend sind nur
„Auswahl löschen ...“ (nur Einträge unter `HKEY_CURRENT_USER`, vorher als `.reg` gesichert), „WON-Login zurücksetzen“
(verschiebt die Dateien in den Sicherungsordner) und der Import (ersetzte Dateien werden vorher kopiert); der Export
schreibt nur in den gewählten Ordner. Einträge unter `HKEY_LOCAL_MACHINE` zeigt der Launcher nur an, mit einem Rat;
`Software\Sierra` und alles darunter löscht er nie ([ARCHITECTURE.md](ARCHITECTURE.md), Abschnitt 4.6).
Vorbereitung wie bei L-WP5 (Registry-Schlüssel sichern, Abschnitt 1), zusätzlich die Ordner `Data\Saved Games` und
`Data\Scenarios` beider Spiele kopieren. Die Fälle nennen die deutschen Texte. Wer eine CD- oder GOG-Installation hat
oder hatte, macht WP8-02 zuerst, vor allen Fällen, die Schlüssel anlegen.

| Fall | Schritte | Erwartet |
|---|---|---|
| WP8-01 | Pflichtfall, CD-Keys unverändert: vor WP8-03 bis WP8-05 in einer Eingabeaufforderung `reg export "HKLM\SOFTWARE\WOW6432Node\Sierra\CDKeys" "%TEMP%\cdkeys-vorher.reg"` (32-Bit-Windows: `HKLM\SOFTWARE\Sierra\CDKeys`; gibt es `HKCU\Software\Sierra\CDKeys` oder `HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Sierra\CDKeys`, diese ebenso in eigene Dateien). Nach WP8-05 dieselben Schlüssel nach `…-nachher.reg` exportieren und je `fc /b "%TEMP%\cdkeys-vorher.reg" "%TEMP%\cdkeys-nachher.reg"` vergleichen. Danach alle diese Dateien löschen (sie enthalten die CD-Keys, nie weitergeben). Zum Schluss NeoEE starten und in die Lobby einloggen. | `fc` meldet „Keine Unterschiede gefunden“. `log.txt` nach jedem Löschen je vorhandenem Schlüssel `Registry cleanup: … \Sierra\CDKeys exists, unchanged by the cleanup.` und nie `… existed before the cleanup and is missing now.` Weder die Seite noch `log.txt` zeigt einen CD-Key. Der Login klappt (der CD-Key wird angenommen). |
| WP8-02 | Echte Reste notieren (Beleg für die Liste, Forenbericht Abschnitt 8 Testfall 8): auf einem Rechner mit einer CD- oder GOG-Installation von Empire Earth oder AoC, und noch einmal nach deren Deinstallation, die Schlüsselnamen auflisten, ohne `/s`: `reg query "HKCU\Software\SSSI"`, `reg query "HKCU\Software\Mad Doc Software"`, `reg query "HKCU\Software\Sierra"`, `reg query "HKCU\Software\Stainless Steel Studios"`, dieselben unter `HKLM\SOFTWARE\WOW6432Node\` und unter `HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\`, dazu `reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" /f "Empire Earth"` (InstallShield). Dann Seite *Werkzeuge*, „Alte Registry-Einträge“. | Notieren: welche Schlüssel es gibt (nur Namen, nie die Werte unter `CDKeys`), mit Installationsart und ob vor oder nach der Deinstallation; was die Seite dazu sagt. Die Seite nennt nur Schlüssel der Liste aus ARCHITECTURE 4.6. Schlüssel außerhalb der Liste – weitere Unterschlüssel von `Sierra` außer `CDKeys`, `Stainless Steel Studios`, die InstallShield-Einträge der CD-Setups – erscheinen nicht: Sie fehlen in der Liste, weil es für sie noch keinen Beleg gibt; diese Notizen sind der Beleg für spätere Einträge. |
| WP8-03 | R5, veralteter Eintrag des eigenen Kontos: nur wenn `reg query "HKCU\Software\SSSI\Empire Earth"` nichts findet und keine Installation von Empire Earth (Community-Setup EE, CD, GOG) erkannt wird. Anlegen: `reg add "HKCU\Software\SSSI\Empire Earth" /v "Installed From Volume" /d "C:" /f` und `reg add "HKCU\Software\SSSI\Empire Earth" /v "Installed From Directory" /d "\GIBTESNICHT\Empire Earth\\" /f`. Launcher starten, *Werkzeuge*. Den Eintrag ankreuzen, „Auswahl löschen ...“, zuerst „Nein“, dann noch einmal mit „Ja“. Danach die `.reg`-Datei aus der Meldung doppelklicken, `reg query "HKCU\Software\SSSI\Empire Earth"`, Launcher neu starten; zum Schluss `reg delete "HKCU\Software\SSSI\Empire Earth" /f`. | „1 Einträge Ihres Kontos sind Reste einer entfernten Installation. …“, in der Liste `HKEY_CURRENT_USER\Software\SSSI\Empire Earth (Ordner C:\GIBTESNICHT\Empire Earth existiert nicht mehr)`. „Auswahl löschen ...“ ist erst nach dem Ankreuzen bedienbar. Die Rückfrage nennt den Schlüssel und den Sicherungsordner; „Nein“ ändert nichts. Nach „Ja“: „1 Einträge gelöscht. Die Sicherung liegt in: …\Backups\<Datum>_registry-cleanup\<Datum>_registry-cleanup.reg“, dann „Nichts zu bereinigen: Kein Eintrag Ihres Kontos ist ein Rest.“ und kein bedienbarer Knopf „Auswahl löschen ...“. `log.txt`: `Registry cleanup: deleted HKEY_CURRENT_USER\Software\SSSI\Empire Earth (hkcu-ee-ee, stale: …` und `Registry cleanup: 1 key(s) deleted, backup ….` Nach dem Doppelklick stehen beide Werte wieder da, und der Launcher bietet den Eintrag wieder an. |
| WP8-04 | R5, Einträge, die bleiben (ADR 0007, Ergänzung nach der Planprüfung): mit dem Schlüssel aus WP8-03 nacheinander (Launcher jeweils neu starten): (a) Ordner `C:\GIBTESNICHT\Empire Earth` anlegen; (b) Ordner wieder löschen, `Installed From Volume` auf einen USB-Stick (z. B. `E:`) setzen, dann auf einen nicht vorhandenen Buchstaben (z. B. `Q:`) und auf ein Netzlaufwerk; (c) `Installed From Volume` löschen; (d) wieder `C:`, Eintrag ankreuzen, vor „Ja“ in der Rückfrage den Ordner `C:\GIBTESNICHT\Empire Earth` anlegen. Ist eine Installation von Empire Earth vorhanden, nur diese notieren. Danach `reg delete "HKCU\Software\SSSI\Empire Earth" /f` und den Ordner löschen. | Nichts davon wird angeboten; in der Liste darunter: (a) „…\SSSI\Empire Earth: bleibt erhalten, der Ordner … existiert.“ (b) „… liegt auf einem Laufwerk, das nicht angeschlossen oder keine lokale Festplatte ist.“ (c) „… der Eintrag nennt keinen Spielordner.“ (d) „Nichts wurde gelöscht: Ein Eintrag wird wieder genutzt …“, der Schlüssel ist unverändert, im Sicherungsordner kein neuer Ordner. Mit Installation: „… bleibt erhalten, eine Installation von Empire Earth wurde gefunden.“ |
| WP8-05 | R5, Einträge für alle Benutzer: nur wenn `reg query "HKLM\SOFTWARE\WOW6432Node\SSSI\Empire Earth"` nichts findet. In einer Eingabeaufforderung als Administrator dieselben zwei `reg add`-Befehle wie in WP8-03 mit `HKLM\SOFTWARE\WOW6432Node\SSSI\Empire Earth` (32-Bit-Windows: `HKLM\SOFTWARE\SSSI\Empire Earth`). Launcher als normaler Benutzer starten, *Werkzeuge*. Danach den Rat befolgen: Registrierungs-Editor als Administrator, Schlüssel exportieren, löschen. | Kein Kästchen dafür; im Textfeld „HKEY_LOCAL_MACHINE\Software\WOW6432Node\SSSI\Empire Earth: Rest einer entfernten Installation (Ordner … existiert nicht mehr). Der Eintrag gilt für alle Benutzer, daher ändert der Launcher ihn nicht. So entfernen Sie ihn: …“. Gibt es `Software\Sierra`, steht dort „…\Sierra: nicht löschen, dort liegen die CD-Keys von NeoEE (CD-Keys vorhanden).“; keine Zeile rät, `Software`, `Sierra` oder `CDKeys` zu löschen. Ohne angebotenen Eintrag „Nichts zu bereinigen …“. Der Launcher fragt nie nach Administratorrechten. |
| WP8-06 | R6, WON-Login zurücksetzen (Forum: Login-Fehler `WS_GetCert_InvalidPubKeyBlock`): NeoEE spielen und in die Lobby einloggen, beenden. Im Explorer `_wonkver.pub` und `_wonlogin.ks` im Ordner `Empire Earth` und im AoC-Ordner suchen, auch im VirtualStore (`%LOCALAPPDATA%\VirtualStore\…`). *Werkzeuge* → „WON-Login zurücksetzen“; „Sicherungsordner öffnen“; danach wieder in die Lobby einloggen. | Vorher listet „WON-Login-Dateien: …“ genau die gefundenen Dateien. Danach „… Dateien nach …\Backups\<Datum>_won-login-reset verschoben. Dieser Ordner enthält Login-Daten: Geben Sie ihn nie weiter. …“; im Spielordner sind sie weg, im Sicherungsordner liegen sie unter `EE\`, `AoC\` (bzw. `EE-VirtualStore\`, `AoC-VirtualStore\`) mit `moved-files.txt` (woher jede Datei kam). Dateien aus der Liste des Setups (`_setupdata_<Produkt>\files.sha256`) bleiben liegen, `log.txt`: `… is listed in the manifest of the setup and is kept.` (notieren, falls das vorkommt). `log.txt`: `WON login reset of …: Done, … file(s) moved into … (it contains login data).` Der nächste Login klappt; das Spiel legt neue Dateien an. |
| WP8-07 | R6, Zugriff verweigert und laufendes Spiel: (a) Installation „für alle Benutzer“ unter `C:\Program Files (x86)`, normales Benutzerkonto; als Administrator im Explorer eine leere Datei `_wonlogin.ks` in den Ordner `Empire Earth` legen; Launcher neu starten, „WON-Login zurücksetzen“. (b) Empire Earth über den Launcher starten, Alt+Tab, „WON-Login zurücksetzen“. Danach die Datei aus (a) als Administrator löschen. | (a) „Nach … kopiert, aber diese Dateien konnten nicht entfernt werden (Zugriff verweigert). Beenden Sie das Spiel und versuchen Sie es erneut, oder löschen Sie sie im Explorer:“ mit dem Pfad; kein Absturz, keine UAC-Abfrage. (b) „Nicht möglich, solange Empire Earth.exe läuft.“, nichts wird verschoben; `log.txt`: `Not allowed to reset the WON login now: Empire Earth.exe is running …`. |
| WP8-08 | R8, VirtualStore mit normalem Benutzerkonto (Forenbericht Abschnitt 8 Testfall 1): Installation „für alle Benutzer“ unter `C:\Program Files (x86)`, normales Konto; Empire Earth über den Launcher spielen (Lobby oder ein Spiel speichern), beenden, Launcher neu starten, *Werkzeuge* → „VirtualStore“. Dann als dieses Konto eine Programmdatei aus der Liste des Setups, z. B. `Empire Earth\neoee.dll`, an dieselbe Stelle unter `%LOCALAPPDATA%\VirtualStore\Program Files (x86)\<Ordner>\` kopieren, Launcher neu starten; danach die Kopie löschen. Zum Vergleich eine Installation außerhalb von `Program Files` wählen. | Nach dem Spielen: „Keine Kopien im VirtualStore.“ oder „… weitere Dateien (Lobby-Profile, Protokolle, Spielstände) liegen im VirtualStore; das ist normal …“ (Ergebnis und Dateien notieren). Mit der Kopie: „1 Dateien der Installation oder Programmdateien werden aus dem VirtualStore statt aus dem Spielordner verwendet; …“ und darunter `<VirtualStore-Pfad> (verwendet statt <Spielordner-Pfad>)`; `log.txt`: `VirtualStore: the game uses …`. Außerhalb: „Nicht betroffen: Die Spielordner liegen nicht unter Program Files, ProgramData oder dem Windows-Ordner.“ |
| WP8-09 | R10, Export (Forum t=9004 p=44629): in Empire Earth und AoC je ein Spiel speichern und ein Szenario im Editor speichern (falls aus WP7-05 vorhanden, diese). *Werkzeuge* → „Exportieren ...“, den Ordner `Dokumente` wählen. Dann noch einmal „Exportieren ...“ mit dem Spielordner als Ziel. Mit WP8-08: liegt ein gleichnamiger Spielstand im Spielordner und im VirtualStore, den Export ansehen. | „… Spielstände und … Szenarien gefunden.“ Danach „… Dateien exportiert nach: …\Dokumente\Empire Earth saves <Datum>“, darin `EE\Saved Games`, `EE\Scenarios`, `AoC\…` mit allen `.ees`/`.scn`, auch denen aus dem VirtualStore; die Spielordner sind unverändert. Spielordner als Ziel: „Wählen Sie einen Ordner außerhalb der Spielordner.“ Bei gleichem Namen exportiert der Launcher die VirtualStore-Kopie (die das Spiel verwendet) und nennt die andere: „Von einer gleichnamigen VirtualStore-Kopie verdeckt …“. Ein ZIP-Export ist nicht vorgesehen. |
| WP8-10 | R10, Import mit Prüfungen, Mehrspieler-Spielstand mit Umlauten (Forenbericht Abschnitt 8 Testfall 17): einen Spielstand aus WP8-09 in `Kampf um Köln.ees` umbenennen und in `Downloads` legen, dazu eine Kopie eines vorhandenen Spielstands unter gleichem Namen und eine Textdatei `test.exe`. *Werkzeuge* → „In Empire Earth importieren ...“, im Dialog `*.*` eintippen und alle drei wählen; die Rückfrage zum Ersetzen zuerst mit „Nein“, dann den Import des gleichnamigen mit „Ja“ wiederholen. Danach „Kampf um Köln“ im Spiel laden; wenn ein zweiter Rechner da ist, als Mehrspieler-Spielstand (beide Spieler mit derselben Datei, Host mit weitergeleiteten Ports: 33334 und 33336 TCP und UDP, 33335 TCP). Optional ein Name mit Zeichen außerhalb der Windows-Codepage (z. B. `テスト.ees` auf deutschem Windows). | Rückfrage nennt die Datei, die ersetzt würde. Nach „Nein“: „1 von 3 Dateien importiert.“ mit „test.exe: nur .ees- und .scn-Dateien lassen sich importieren“, „<Name>.ees: nicht ersetzt“ und „Kampf um Köln.ees: Der Name enthält Zeichen außerhalb von einfachem ASCII; im Mehrspieler braucht jeder Spieler genau diesen Namen.“ Nach „Ja“: ersetzt, „Die ersetzten Dateien liegen in: …\Backups\<Datum>_import-saved-games“ mit der alten Datei. Das Spiel listet und lädt „Kampf um Köln“; Mehrspieler-Ergebnis notieren. Optional: „… der Name enthält Zeichen, die das Spiel nicht lesen kann“. |
| WP8-11 | R10 und ADR 0016, Import mit normalem Konto in eine Installation unter `C:\Program Files (x86)` („für alle Benutzer“): einen neuen Spielstand importieren; im Spiel laden. | „1 von 1 Dateien importiert.“ ohne UAC-Abfrage. Die Datei liegt im VirtualStore (`%LOCALAPPDATA%\VirtualStore\Program Files (x86)\…\Data\Saved Games\`), nicht im Spielordner; `log.txt`: `Saved games: writing into … was denied; the file goes to its VirtualStore folder, where the game reads it (ADR 0016).` Das Spiel zeigt den Spielstand. |
| WP8-12 | Namensprüfung (Forenbericht Abschnitt 8 Testfall 17): in Empire Earth einen Spieler `Jürgen` anlegen (Einzelspieler), bei NeoEE ein Lobby-Profil mit Umlaut, falls möglich; Spiel beenden, Launcher neu starten, *Werkzeuge* → „Spielernamen“ und „Spielstände und Szenarien“ lesen. Danach den Spieler wieder löschen oder umbenennen. | „1 Namen enthalten Zeichen außerhalb von einfachem ASCII:“ und „Empire Earth, Spieler: Jürgen“ (bzw. „…, Lobby-Profil: …“); der Text darüber nennt den Grund aus dem Forum; „Spielstände und Szenarien“ nennt für den Host dieselben Ports wie die Netzwerkprüfung (WP9-08): standardmäßig 33334 und 33336 TCP und UDP, 33335 TCP. Ohne solche Namen: „Alle … Lobby-Profile und Spielernamen verwenden einfache Zeichen.“ `log.txt` nennt keinen Namen, nur `Name check: player folder 1 of EE has characters outside printable ASCII.` |
| WP8-13 | Vertrag 4.2 und ADR 0016: (a) Community-Setup starten und auf der ersten Seite lassen, Seite *Werkzeuge*; Setup abbrechen. (b) Empire Earth über den Launcher starten, Alt+Tab, mit dem Eintrag aus WP8-03 „Auswahl löschen ...“ → „Ja“ und „In Empire Earth importieren ...“. | (a) Spätestens nach 2 Sekunden sind „Auswahl löschen ...“, „WON-Login zurücksetzen“ und beide Import-Knöpfe ausgegraut, unter den Abschnitten „Das Setup von … läuft. …“; „Exportieren ...“ und „Sicherungsordner öffnen“ bleiben bedienbar. Nach dem Abbrechen wieder bedienbar. (b) „Nicht möglich, solange Empire Earth.exe läuft.“; nichts gelöscht oder importiert, kein neuer Ordner im Sicherungsordner; `log.txt`: `Not allowed to delete stale registry keys now: …` bzw. `Not allowed to import saved games now: …`. |
| WP8-14 | „Sicherungsordner öffnen“, einmal vor allen anderen Fällen dieses Pakets (Ordner fehlt noch, z. B. nach Umbenennen von `Backups`) und einmal danach. | Der Explorer öffnet `%LOCALAPPDATA%\Empire Earth Launcher\Backups` ohne UAC-Abfrage; fehlt er, legt der Launcher ihn vorher an. Danach liegen dort die Ordner `…_registry-cleanup`, `…_won-login-reset`, `…_import-saved-games` (und `…_reset-game-settings` aus L-WP5). Der Text unter „Sicherungen“ nennt den Ordner und dass er Login-Daten enthält. |
| WP8-15 | Seite *Werkzeuge* mit allen Abschnitten (mit angebotenem Eintrag, Ratschlägen, VirtualStore-Liste, Ergebniszeilen) und die Rückfragen aus WP8-03 und WP8-10 auf Deutsch, Englisch und Französisch, je bei 100 % und 150 %; Screenshots. | Nichts abgeschnitten oder überlappend; die Seite scrollt; lange Pfade umbrechen oder lassen sich im Textfeld lesen. Deutsche Texte gegenlesen wie in WP3-03; französische Texte notieren, die unklar wirken. |
| WP8-16 | Nach allen Fällen `log.txt` durchsehen. | Keine `Unhandled exception`, keine `A background task failed`, keine `The scan of the maintenance tools failed.`; nach jeder Suche eine Zeile `Registry cleanup: … key(s) offered, … shown read-only …`; kein CD-Key, kein Spieler- oder Profilname; nie `… existed before the cleanup and is missing now.` |
| WP8-17 | Optional, für Fortgeschrittene (Sicherheitsprüfung): symbolische Registry-Verknüpfung unter einem veralteten Eintrag. Mit dem Schlüssel aus WP8-03 (angelegt, Launcher bietet ihn an) in einer PowerShell (normaler Benutzer) das Skript „Verknüpfung anlegen“ unter dieser Tabelle ausführen: Es legt `HKCU\Software\EELinkTest` mit dem Wert `Probe` an und unter `HKCU\Software\SSSI\Empire Earth` die Verknüpfung `Link` darauf. Launcher neu starten, *Werkzeuge*, den Eintrag ankreuzen, „Auswahl löschen ...“ → „Ja“. Danach `reg query "HKCU\Software\EELinkTest"`, zum Schluss das Skript „Aufräumen“. | „Nichts wurde geändert: Die Sicherung konnte nicht geschrieben werden (Einzelheiten im Protokoll).“; `HKCU\Software\SSSI\Empire Earth` und `HKCU\Software\EELinkTest` mit `Probe` sind unverändert da; im Sicherungsordner keine `.reg`-Datei, die `EELinkTest` oder `Probe` enthält. `log.txt`: `Registry cleanup: nothing was deleted because the backup failed: … is a symbolic registry link; it is neither followed nor backed up …`. |
| WP8-18 | R5, Ordner, den der Launcher nicht ansehen darf (Sicherheitsprüfung): Schlüssel wie in WP8-03 anlegen, dazu den Ordner `C:\GIBTESNICHT\Empire Earth`. In einer Eingabeaufforderung als Administrator `icacls "C:\GIBTESNICHT" /deny "%USERNAME%:(OI)(CI)F"` (`%USERNAME%` ist das Konto, mit dem der Launcher läuft; die Eingabeaufforderung als Administrator mit diesem Konto öffnen oder den Namen einsetzen). Launcher als dieses Konto starten, *Werkzeuge*. Danach als Administrator `icacls "C:\GIBTESNICHT" /remove:d "%USERNAME%"`, `rmdir /s /q "C:\GIBTESNICHT"` und `reg delete "HKCU\Software\SSSI\Empire Earth" /f`. | Der Eintrag wird nicht angeboten; in der Liste darunter „HKEY_CURRENT_USER\Software\SSSI\Empire Earth: bleibt erhalten, der Launcher darf den Ordner C:\GIBTESNICHT\Empire Earth oder seinen übergeordneten Ordner nicht ansehen und kann daher nicht feststellen, ob der Ordner noch existiert.“ `log.txt`: `Registry cleanup: … is kept, whether C:\GIBTESNICHT\Empire Earth exists cannot be told.` |

Skripte zu WP8-17 (PowerShell 5.1 oder 7, normaler Benutzer, nicht als Administrator). „Verknüpfung anlegen“:

```powershell
Add-Type -Namespace EE -Name Reg -MemberDefinition @'
[DllImport("advapi32.dll", CharSet = CharSet.Unicode)] public static extern int RegCreateKeyEx(IntPtr key, string subKey, int reserved, string cls, int options, int sam, IntPtr security, out IntPtr result, out int disposition);
[DllImport("advapi32.dll", CharSet = CharSet.Unicode)] public static extern int RegOpenKeyEx(IntPtr key, string subKey, int options, int sam, out IntPtr result);
[DllImport("advapi32.dll", CharSet = CharSet.Unicode)] public static extern int RegSetValueEx(IntPtr key, string name, int reserved, int type, byte[] data, int size);
[DllImport("advapi32.dll")] public static extern int RegCloseKey(IntPtr key);
[DllImport("ntdll.dll")] public static extern int NtDeleteKey(IntPtr key);
'@
$hkcu = [IntPtr](-2147483647)   # HKEY_CURRENT_USER
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
New-Item 'HKCU:\Software\EELinkTest' -Force | Out-Null
New-ItemProperty 'HKCU:\Software\EELinkTest' -Name Probe -Value 1 -PropertyType DWord -Force | Out-Null
$link = [IntPtr]::Zero; $disposition = 0
# REG_OPTION_CREATE_LINK = 2, KEY_ALL_ACCESS = 0xF003F, REG_LINK = 6
[EE.Reg]::RegCreateKeyEx($hkcu, 'Software\SSSI\Empire Earth\Link', 0, $null, 2, 0xF003F, [IntPtr]::Zero, [ref]$link, [ref]$disposition)
$target = [Text.Encoding]::Unicode.GetBytes("\REGISTRY\USER\$sid\Software\EELinkTest")
[EE.Reg]::RegSetValueEx($link, 'SymbolicLinkValue', 0, 6, $target, $target.Length)
[EE.Reg]::RegCloseKey($link) | Out-Null
```

Beide Aufrufe geben `0` aus. „Aufräumen“ (in derselben PowerShell; die Verknüpfung selbst lässt sich nur so löschen, `reg
delete` würde ihr folgen):

```powershell
$link = [IntPtr]::Zero
# REG_OPTION_OPEN_LINK = 8, DELETE = 0x10000
[EE.Reg]::RegOpenKeyEx($hkcu, 'Software\SSSI\Empire Earth\Link', 8, 0x10000, [ref]$link)
[EE.Reg]::NtDeleteKey($link); [EE.Reg]::RegCloseKey($link) | Out-Null
reg delete "HKCU\Software\SSSI\Empire Earth" /f
reg delete "HKCU\Software\EELinkTest" /f
```

### L-WP9 – Netzwerkdiagnose, Bericht, Laptop-Paket

Die Seite *Werkzeuge* endet mit zwei neuen Abschnitten. „Netzwerk“ prüft nur auf Knopfdruck (oder über den Link der Seite
*Spielen*): die Netzwerkadapter dieses Rechners (Typ, Treibername, IPv4 mit Gateway, IPv6 nur als „keine“/„nur
Link-local“/„vorhanden“, virtuelle und VPN-Adapter markiert), die Namensauflösung (DNS) des NeoEE-Statusservers und des
Servers aus `NeoEE.cfg`, den Update-Server (`api.empireearth.eu`, gesendet wird nur die AppId wie in WP7-12), den
NeoEE-Statusserver (dieselbe Anfrage wie die Spielerliste), `NeoEE.cfg` und `WONLobby.cfg` beider Spielordner (nur lesend)
mit der Tabelle der Portweiterleitung und `upnp_info.txt`. Daraus entstehen das Ergebnis (zum Beispiel „Vermutlich ein
Serverausfall, nicht Ihr PC“) und Hinweise. Der Launcher fragt keinen Dienst nach der öffentlichen Adresse und verbindet sich
nie mit den Ports 10002/10003 von NeoEE. „Diagnosebericht“ kopiert einen englischen Text mit allem, was der Launcher gefunden
hat, in die Zwischenablage oder speichert ihn; er enthält keine CD-Keys, keine Login-Daten, keine Spielernamen, keine MAC-
oder öffentliche IP-Adresse und keinen Benutzer- oder Rechnernamen, und der Launcher sendet ihn nie. Auf der Seite *Spielen*
steht unter einer Spielerliste „Spieler online (nicht verfügbar)“ der Link „Warum? Netzwerk prüfen“.

Vorbereitung: `ipconfig /all` und `getmac` in einer Eingabeaufforderung ausführen und die Ausgabe **nur lokal** aufheben
(sie enthält MAC-Adressen und Namen; nicht ins Protokoll kopieren), dazu den eigenen Benutzernamen (`echo %USERNAME%`) und
Rechnernamen (`hostname`). Für WP9-05 und WP9-06 die Datei `Empire Earth Launcher.exe.config` im entpackten Ordner vorher
kopieren. Die Fälle nennen die deutschen Texte.

| Fall | Schritte | Erwartet |
|---|---|---|
| WP9-01 | R7, Netzwerkadapter: Launcher starten, *Werkzeuge* → „Netzwerk prüfen“. Ergebnis mit `ipconfig /all` vergleichen. | Höchstens etwa 20 Sekunden „Das Netzwerk wird geprüft ...“, das Fenster bleibt bedienbar. Danach „Der NeoEE-Server antwortet (… Spieler online). …“ und im Textfeld je Adapter eine Zeile wie `Ethernet „Intel(R) …“: IPv4 192.168.178.20/24, Gateway 192.168.178.1, IPv6 nur Link-local`: Typ, Treibername (wie „Beschreibung“ in `ipconfig /all`), IPv4-Adresse, Präfix und Gateway stimmen; der Adaptername aus `ipconfig` (z. B. „Ethernet 2“, „WLAN“) und die MAC-Adresse stehen **nicht** da. Dazu „Namensauflösung titan.empireearth.eu: OK“, „Update-Server: antwortet (HTTP 200)“, „NeoEE-Statusserver titan.empireearth.eu:10005: antwortet, … Spieler online“. `log.txt`: Zeilen `Network diagnostics: …` und genau eine `Update API: GET https://api.empireearth.eu/setup/?product=<AppId>: …`. |
| WP9-02 | R7, Forum 4.10 und Forenbericht Abschnitt 8 Testfall 12 (Netzwerkadapter): mit einem verbundenen VPN, Hamachi oder einem virtuellen Adapter (VirtualBox, Hyper-V) „Netzwerk prüfen“. Dann im Spiel unter „Mehrspieler → Optionen“ und in den Lobby-Optionen den Netzwerkadapter ansehen und ein Spiel hosten. Sammelaufgabe (t=32479): vor und nach dem Ändern der Adapterwahl im Spiel `reg export "HKCU\Software\Neo" "%TEMP%\neo-adapter-vorher.reg"` bzw. `…-nachher.reg` und `fc` der beiden Dateien; ebenso `_wonlobbypersistent.dat` (Größe/Änderungszeit) vergleichen. | Der virtuelle Adapter trägt „, virtuell oder VPN“; Hinweis „… virtuelle oder VPN-Adapter sind verbunden …“ mit dem Rat, in Lobby und Spiel denselben echten Adapter zu wählen. Eine öffentliche oder Hamachi-Adresse (25.x.x.x) erscheint nur als „öffentliche Adresse“. Notieren: welche Adapter das Spiel anbietet, ob das gehostete Spiel in der Lobby erscheint, ob etwas abstürzt (t=1576, t=3128), und **wo die Adapterwahl gespeichert wird** (geänderter Registry-Wert oder Datei; offener Punkt in ARCHITECTURE 14). |
| WP9-03 | R7: Kabel und WLAN gleichzeitig verbunden, „Netzwerk prüfen“. | Hinweis „2 Netzwerkadapter sind mit einem Gateway verbunden …“; die Zeile der Portweiterleitung nennt „… an die IPv4-Adresse dieses Rechners“ statt einer Adresse. Mit nur einem Adapter steht dort dessen IPv4-Adresse (wie in `ipconfig`). |
| WP9-04 | R7, Forenbericht Abschnitt 8 Testfall 20 (ohne Netz): Flugmodus ein (oder Kabel ziehen und WLAN aus), „Netzwerk prüfen“; danach Netz wieder ein. | „Dieser Rechner scheint keine Internetverbindung zu haben: …“ und der Hinweis „Kein verbundener Netzwerkadapter hat ein IPv4-Gateway …“; „Namensauflösung …: fehlgeschlagen“ oder „keine Antwort“, „Update-Server: keine Antwort (keine Verbindung)“ oder „… (keine Antwort innerhalb von 10 Sekunden)“. Kein Fehlerdialog, das Fenster bleibt bedienbar. |
| WP9-05 | Ausfall-Hinweis (Forenbericht Abschnitt 8 Zeile 9, nicht streichbar): Launcher schließen, in `Empire Earth Launcher.exe.config` den Wert von `NeoServerPort` von `10005` auf `10099` ändern (ein Port, auf dem der Server nicht antwortet), Launcher starten, Seite *Spielen* etwa 30 Sekunden offen lassen; dann auf „Warum? Netzwerk prüfen“ klicken. Danach die Kopie der `.exe.config` zurücklegen. | *Spielen*: „Spieler online (nicht verfügbar)“ und darunter der Link „Warum? Netzwerk prüfen“. Der Klick öffnet *Werkzeuge* am Abschnitt „Netzwerk“ und startet die Prüfung; Ergebnis: „Vermutlich ein Serverausfall, nicht Ihr PC: Die Servernamen werden aufgelöst und der Update-Server antwortet, aber der NeoEE-Server nicht. …“. Mit der zurückgelegten Datei verschwindet der Link, sobald die Liste wieder kommt. `log.txt`: `Network diagnostics: probably a server outage, not this computer …`. |
| WP9-06 | R7: wie WP9-05, aber `NeoServerHost` auf `gibtesnicht.empireearth.eu` setzen (Port wieder 10005); „Netzwerk prüfen“. Optional (als Administrator): eine ausgehende Firewall-Regel nur für `Empire Earth Launcher.exe` (`netsh advfirewall firewall add rule name="EE-Launcher-Test" dir=out action=block program="<Pfad>\Empire Earth Launcher.exe"`), prüfen, danach `netsh advfirewall firewall delete rule name="EE-Launcher-Test"`. Am Ende die Kopie der `.exe.config` zurücklegen. | Falscher Name: „Der Update-Server antwortet, aber der Name gibtesnicht.empireearth.eu des NeoEE-Servers lässt sich nicht auflösen. …“. Mit der Firewall-Regel: „Kein Server antwortet, obwohl die Namen aufgelöst werden. Vielleicht blockiert eine Firewall …“. |
| WP9-07 | R7 ohne Community-Installation (nur wenn eine CD-/GOG-Installation oder Kopie gewählt ist und keine Installation des Community-Setups existiert, sonst „nicht geprüft“): „Netzwerk prüfen“. | „Update-Server: nicht gefragt (keine Installation des Community-Setups mit AppId)“; im Protokoll keine Zeile `Update API: GET`. Antwortet der Statusserver nicht, lautet das Ergebnis „… Ob er ausgefallen ist, lässt sich nicht sagen …“. |
| WP9-08 | R7, Forum 4.9 und Forenbericht Abschnitt 8 Testfall 11 (Hosting): NeoEE-Installation, „Netzwerk prüfen“; `NeoEE.cfg` und `WONLobby.cfg` beider Spielordner im Editor ansehen. Dann ein Standardspiel über NeoEE hosten (RIP-Hosting, ohne Portweiterleitung) und, wenn möglich, ein Szenario oder einen Spielstand mit Portweiterleitung nach der angezeigten Tabelle; optional zwei Rechner hinter einem Router (t=3320, t=3340). | Zeilen „Empire Earth, NeoEE.cfg: RIP-Hosting an, Server titan.neoee.net, Spielport 33334, Relay-Ports 33340, Portprüfung an, UPnP an“ und „…, WONLobby.cfg: CDKeyCheck true, Dateiübertragungsport 33335, Lobby-Port 33336“ mit denselben Werten wie die Dateien; „Namensauflösung“ auch für den Server aus `NeoEE.cfg`; „Portweiterleitung zum Hosten (Empire Earth, The Art of Conquest): 33334 TCP+UDP, 33335 TCP, 33336 TCP+UDP an <IPv4 dieses Rechners>“. Ergebnisse des Hostens notieren (Ping, „RIP“, Beitritt). Die Dateien sind danach unverändert (Änderungsdatum im Explorer). |
| WP9-09 | Forenbericht Abschnitt 8 Testfall 13 (`CDKeyCheck`) und RIP-Hosting aus: Als Administrator `NeoEE.cfg` und `WONLobby.cfg` des Ordners `Empire Earth` sichern (kopieren) und dann `Active: false` bzw. `CDKeyCheck: false` eintragen. „Netzwerk prüfen“. Danach beide Sicherungen zurückkopieren. | Hinweise „NeoEE.cfg von Empire Earth: Das RIP-Hosting ist ausgeschaltet (Active: false). …“ und „WONLobby.cfg von Empire Earth: CDKeyCheck ist nicht true. … Community-Setup als Reparatur …“. Der Launcher bietet keine Änderung der Dateien an; ihre Änderungszeit bleibt (nur gelesen). Bei einer EE-Installation ohne NeoEE gibt es zu `CDKeyCheck: false` keinen Hinweis. |
| WP9-10 | Sammelaufgabe `upnp_info.txt` (ARCHITECTURE 14, Format unbekannt): nach dem Hosten aus WP9-08 im Spielordner und im VirtualStore (`%LOCALAPPDATA%\VirtualStore\…\Empire Earth`) nach `upnp_info.txt` suchen; „Netzwerk prüfen“. Wenn die Datei existiert: ihren Inhalt ins Protokoll kopieren und **die externe IP-Adresse durch `x.x.x.x` ersetzen**. Router und Anschlussart notieren (DSL, Kabel, Glasfaser; laut Router „DS-Lite“ oder „IPv4“). | Zeile „Empire Earth, upnp_info.txt: …“: „nicht vorhanden“, „unbekanntes Format“ oder „externe Adresse <Klasse>, lokale Adresse …“ – nie die externe Adresse selbst. Bei einem DS-Lite- oder CGNAT-Anschluss erscheint ein Hinweis zu DS-Lite bzw. Carrier-Grade-NAT (t=11057 p=48100), bei einem zweiten Router davor einer zu doppeltem NAT. Ergebnis und Dateiinhalt (anonymisiert) sind die Belege für den Parser. |
| WP9-11 | Diagnosebericht prüfen (ADR 0013, Ergänzung nach der Planprüfung): nach WP9-01 *Werkzeuge* → „Bericht kopieren“, in Notepad einfügen und lesen. Mit Strg+F nach dem eigenen Benutzernamen, dem Rechnernamen (`hostname`), den MAC-Adressen und Adapternamen aus der Vorbereitung, der öffentlichen IP-Adresse (Router-Oberfläche) und nach eigenen Spieler- und Lobby-Profilnamen suchen. | „Der Bericht wurde in die Zwischenablage kopiert; er steht unten.“; der Text steht auch im Textfeld. Er enthält: Launcher- und Windows-Version, Bildschirm, Grafikkarte („Display:“, wie im Geräte-Manager unter „Grafikkarten“), die Installationen mit Ordnern, Dateiversionen, ob ein DirectX-Wrapper installiert ist („DirectX wrapper: EE installed/none (…)“, passend zur Setup-Auswahl), Integrität mit Befunden, Standardwerte, Hinweise der Einstellungen, VirtualStore, „CD keys: … exists/missing“ ohne Werte, die Netzwerkprüfung. **Keiner** der gesuchten Werte kommt vor; Pfade unter dem Benutzerordner beginnen mit `%USERPROFILE%` oder `%LOCALAPPDATA%`, andere Vorkommen des Benutzernamens als Ordner stehen als `<user>`, Spielernamen nur als „EE lobby profile 1: characters outside printable ASCII“. |
| WP9-12 | „Bericht speichern ...“ in `Dokumente`; dann noch einmal mit dem Spielordner als Ziel. Datei in Notepad öffnen. | Vorgeschlagener Name „Empire Earth Launcher Bericht <Datum>.txt“; „Der Bericht wurde gespeichert: …“; Notepad zeigt denselben Text mit Umlauten korrekt (UTF-8). Im Spielordner: „Der Bericht wird nicht in einem Spielordner oder im Installationsordner gespeichert …“, keine Datei dort. `log.txt`: `Diagnostics report: saved to %USERPROFILE%\Documents\…` bzw. `not saved into the installation`. |
| WP9-13 | Datenschutz im Protokoll: nach WP9-01 bis WP9-12 `log.txt` nach den Werten aus WP9-11 durchsuchen (Benutzername nur in den Zeilen, die nicht mit `Network diagnostics:` oder `Diagnostics report:` beginnen, ist erlaubt). | In den Zeilen `Network diagnostics: …` und `Diagnostics report: …` keine MAC, kein Adaptername, keine öffentliche oder externe IP-Adresse, keine IPv6-Adresse, kein Benutzer- oder Rechnername; `upnp_info.txt` nur mit „external address <Klasse>“. |
| WP9-14 | Seiten *Werkzeuge* (Abschnitte „Netzwerk“ mit Ergebnis, Hinweisen und Textfeld, „Diagnosebericht“ mit Textfeld) und *Spielen* (mit dem Link aus WP9-05) auf Deutsch, Englisch und Französisch, je bei 100 % und 150 %; Screenshots. | Nichts abgeschnitten oder überlappend; der Link „Warum? Netzwerk prüfen“ ist vollständig lesbar und liegt über der leeren Spielerliste; die Seite *Werkzeuge* scrollt bis zum Bericht. Deutsche Texte gegenlesen wie in WP3-03; französische Texte notieren, die unklar wirken. |
| WP9-15 | Laptop-Paket (Abschnitt 2.1): Zip öffnen und den Inhalt ansehen; WP1-11 mit diesem Paket ausführen. | Ordner `Empire-Earth-Launcher\` (mit `de\`, `fr\`, `Empire_Earth_Launcher_Core.dll`), `Empire-Earth-Mod-Creator\`, `Tests\`, dazu `LICENSE`, `THIRD-PARTY-NOTICES.md`, `THIRD-PARTY-LICENSES.txt`; die Prüfsumme stimmt (WP1-02); WP1-11: `Failed: 0`, Exit-Code 0. |
| WP9-16 | Nach allen Fällen `log.txt` durchsehen. | Keine `Unhandled exception`, keine `A background task failed`; jede Prüfung beginnt mit `Network diagnostics: started on request.` und endet mit einer Zeile mit dem Ergebnis; keine Netzwerkprüfung ohne Klick (keine solche Zeile beim Start). |

## 6. Optional: Windows 7 SP1 in einer VM

Windows 7 SP1 ist unterstützt, aber ungetestet ([ADR 0001](adr/0001-target-dotnet-framework-4-8.md),
Amendment). Wer eine Windows-7-VM hat (VirtualBox, Hyper-V, …), prüft damit Start, Darstellung und TLS.

Vorbereitung: Windows 7 SP1 mit allen verfügbaren Updates. Für den Offline-Installer von .NET Framework 4.8
verlangt Microsoft, dass das Stammzertifikat „Microsoft Root Certificate Authority 2011“ installiert ist
([Anleitung von Microsoft](https://learn.microsoft.com/en-us/previous-versions/dotnet/framework/install/on-windows-7)).

| Fall | Schritte | Erwartet |
|---|---|---|
| W7-01 | Vor der Installation von .NET 4.8 den Launcher starten. | Windows meldet, dass .NET Framework 4.8 fehlt, und bietet den Download an; der Launcher stürzt nicht ab. |
| W7-02 | .NET Framework 4.8 installieren, neu starten, Launcher starten. | Hauptfenster erscheint, Krypton-Darstellung wie unter Windows 10/11 (Screenshot). |
| W7-03 | `log.txt` öffnen. | Startzeilen wie in WP1-04, keine weiteren `Error`-Zeilen als dort genannt. |
| W7-04 | Mod-Creator starten. | „You are using: Windows 7“. |
| W7-05 | Ab L-WP7: Reparatur-Hinweis öffnen bzw. Update-Prüfung starten. | Entweder Antwort von `api.empireearth.eu` (TLS 1.2) oder die feste Seite `https://empireearth.eu/download`; `log.txt` nennt bei der Ersatzseite den Grund (z. B. TLS-Handshake). |
| W7-06 | Ab L-WP9: *Werkzeuge* → „Netzwerk prüfen“, „Bericht kopieren“. | Adapterzeilen wie in WP9-01 (auch unter Windows 7 ohne Adapternamen und MAC); „Update-Server: antwortet“ oder „keine Antwort (die sichere Verbindung ist fehlgeschlagen)“ bei fehlendem TLS 1.2 – dann lautet das Ergebnis nicht „Serverausfall“; der Bericht nennt „Windows: NT 6.1.7601, …“. |

## 7. Zuordnung der Anforderungen und Forum-Testfälle

Jede Anforderung R1 bis R10 und R17 der v2-Planung und jeder Testfall für echtes Windows aus dem Forenbericht
(Abschnitt 8, Nummern 1 bis 22) ist hier Fällen dieses Plans zugeordnet. „Setup:“ heißt: Der Fall gehört (auch) in den
Testplan des Setup-Repositorys, mit dessen Fall-IDs; „entfällt:“ nennt den Grund. Mehrere Angaben sind durch `;`
getrennt. Bis L-WP8 hieß „offen (L-WPn)“, dass die Fälle mit diesem Arbeitspaket kommen; seit L-WP9 ist keine Zeile mehr
offen. Ein automatischer Test (`TestPlanTests`) prüft die Tabelle: jede Zeile genau einmal, jede genannte Fall-ID
existiert, kein „offen“ mehr, und jede Zeile „Setup:“ oder „entfällt:“ hat einen Grund.

| Bezug | Thema | Zuordnung |
|---|---|---|
| R1 | Standardwerte pro Benutzer, auch für andere Konten | WP5-01, WP5-02, WP5-03, WP5-04 |
| R2 | Integritätsmanifest | WP7-01, WP7-02, WP7-03, WP7-04, WP7-05, WP7-06, WP7-07, WP7-08, WP7-09, WP7-13; Setup: TP-50 (das Setup schreibt das Manifest) |
| R3 | Spielen, laufende Instanzen, Kompatibilitätsoptionen | WP5-12, WP5-13, WP5-14, WP6-01, WP6-03, WP6-07, WP6-08, WP6-09, WP6-15, WP6-16, WP6-17 |
| R4 | Spieleinstellungen zurücksetzen mit `.reg`-Sicherung | WP5-08, WP5-09, WP5-10, WP5-11 |
| R5 | Registry-Bereinigung | WP8-01, WP8-02, WP8-03, WP8-04, WP8-05, WP8-13, WP8-17, WP8-18 |
| R6 | WON-Login zurücksetzen | WP8-06, WP8-07, WP8-14 |
| R7 | Netzwerkdiagnose | WP9-01, WP9-02, WP9-03, WP9-04, WP9-05, WP9-06, WP9-07, WP9-08, WP9-09, WP9-10, WP9-13 |
| R8 | VirtualStore | WP4-16, WP8-08, WP8-09, WP8-11 |
| R9 | Reparatur über das Setup | WP6-05, WP6-06, WP6-10, WP6-11, WP7-02, WP7-04, WP7-12 |
| R10 | Spielstände und Szenarien | WP8-09, WP8-10, WP8-11, WP8-12 |
| R17 | Texte auf Englisch, Deutsch, Französisch | WP3-01, WP3-03, WP3-11, WP3-12, WP4-17, WP5-17, WP6-14, WP7-14, WP8-15, WP9-14 |
| Forum 1 | Frische Installation, Standardnutzer, zweites Konto | WP4-06, WP4-16, WP5-02, WP8-08, WP8-11; Setup: TP-41, TP-71 (Installation und Rechte) |
| Forum 2 | Versionsanzeige, Mehrspieler ohne Versionskonflikt | WP6-02, WP7-10, WP7-11; Setup: TP-70, TP-72 (Version im Hauptmenü) |
| Forum 3 | Grafikmatrix mit und ohne Wrapper | WP5-05; Setup: TP-23 (Wrapper und Renderer installiert das Setup) |
| Forum 4 | Farbtiefe 16 Bit, Rücksetzen auf 32 Bit | WP5-06, WP5-08 |
| Forum 5 | Kompatibilitätsflags, Windows 7 | WP5-12, WP5-13, WP5-14; Setup: TP-20, TP-21, TP-22 (Werte des Setups) |
| Forum 6 | Auflösungsgrenzen, Bildschirm unter 768 Pixel | WP5-07, WP5-15 |
| Forum 7 | AoC ohne vorherigen EE-Start | WP5-02, WP5-19, WP6-04 |
| Forum 8 | Alt-Installation (CD, GOG) vorhanden | WP4-08, WP4-15, WP8-02, WP8-04 |
| Forum 9 | EE und NeoEE parallel, eines deinstallieren | WP4-12; Setup: TP-62, TP-75 (Deinstallation) |
| Forum 10 | Firewall beim Hosten | Setup: TP-76 (Firewall-Regeln legt nur das Setup an, der Launcher ändert die Firewall nicht) |
| Forum 11 | Hosting-Varianten, Portweiterleitung | WP9-08, WP9-10; Setup: TP-76 (Firewall-Regeln für das Hosten legt das Setup an) |
| Forum 12 | Netzwerkadapter (VPN, Hamachi) | WP9-02, WP9-03 |
| Forum 13 | CD-Keys: Server gesperrt, VM, `CDKeyCheck` | WP8-01, WP9-09, WP9-11; Setup: TP-77 (nur das Setup registriert CD-Keys) |
| Forum 14 | Antivirus löscht Dateien | WP4-13, WP6-10, WP7-02; Setup: TP-50 (Hinweis am Ende der Installation) |
| Forum 15 | Offline, nur Spiegel, manipulierter Download | Setup: TP-00, TP-10, TP-11, TP-16 (Downloads macht nur das Setup) |
| Forum 16 | Sprachen: Deutsch für EE und AoC | Setup: TP-78 (Sprachdateien des Spiels installiert das Setup) |
| Forum 17 | Spielstände im Mehrspieler, Namen mit Sonderzeichen | WP8-09, WP8-10, WP8-12 |
| Forum 18 | Laufende Instanz | WP5-10, WP6-07, WP6-08, WP8-07, WP8-13; Setup: TP-79 |
| Forum 19 | Kampagnen-Tribut | entfällt: Spiellogik der Spieldateien, die weder Launcher noch Setup ändern |
| Forum 20 | Launcher: Spielerliste ohne Netz, beschädigte Einstellungen, Pfad mit Umlauten | WP1-04, WP2-05, WP5-19, WP6-12, WP9-04, WP9-05 |
| Forum 21 | GOG als Basis | WP4-08; Setup: TP-63 |
| Forum 22 | NeoEE-Wartungsmodus über kaputter Installation | WP6-11; Setup: TP-73 (die Reparatur macht das Setup) |

Die Launcher-Punkte der Implementierungs-Checkliste des Vertrags ([CONTRACT.md](CONTRACT.md), Abschnitt 7) sind ebenso
Fällen zugeordnet; `TestPlanTests` prüft, dass jeder der fünf Punkte genau eine Zeile mit existierenden Fall-IDs hat. Die
automatischen Tests dazu nennt die abgehakte Checkliste in [ARCHITECTURE.md](ARCHITECTURE.md), Abschnitt 15.

| Vertrag 7 | Punkt | Zuordnung |
|---|---|---|
| Launcher 1 | Erkennung mit allen fünf Quellen, Setups bis 1.7.2, fremde und beschädigte Installationen, Zusammenführen (1.4) | WP4-02, WP4-03, WP4-04, WP4-05, WP4-06, WP4-07, WP4-08, WP4-10, WP4-13, WP4-15 |
| Launcher 2 | Manifest lesen und prüfen: BOM, CRLF, ungültige Zeilen, Pfade außerhalb, Klassen, Zustände, Uninstall-Regel (2) | WP7-01, WP7-02, WP7-03, WP7-05, WP7-06, WP7-07, WP7-08, WP7-09, WP7-13 |
| Launcher 3 | Standardwerte, Marker, Konsistenzprüfungen, Zurücksetzen mit Sicherung (3) | WP5-01, WP5-02, WP5-03, WP5-04, WP5-05, WP5-06, WP5-08, WP5-09, WP5-15 |
| Launcher 4 | Reparatur-Übergabe und Versionsprüfung mit den URL-Fällen des Setups (4) | WP6-10, WP6-11, WP7-02, WP7-10, WP7-11, WP7-12, W7-05 |
| Launcher 5 | Setup- und Spiel-Mutexe: kein Start, kein Lesen von `install.ini`/Manifest und keine Prüfung während eines Setups, laufende Prüfung abgebrochen, Freigabemodi; Start per Shell (4.2) | WP6-01, WP6-05, WP6-06, WP6-07, WP6-09, WP7-04 |
