# Testplan Launcher v2 (Test auf einem echten Windows-Rechner)

Dieser Testplan beschreibt den manuellen Test von Empire Earth Launcher v2 und dem Mod-Creator auf einem echten
Windows-Rechner (dem Laptop-Test). Er prüft, was die automatischen Tests unter Mono nicht prüfen können:
echte Registry-Ansichten, Mutexe, Programmstart, Bildschirm und Skalierung, TLS unter Windows, die Darstellung
von Krypton und die Texte der Oberfläche ([ADR 0012](adr/0012-test-strategy.md)).

| | |
|---|---|
| Stand | Fälle von L-WP1 und L-WP2; jedes weitere Arbeitspaket ergänzt seinen Abschnitt in 5 im selben Commit |
| Sprache | Deutsch (die Programmtexte gibt es auf Englisch, Deutsch und Französisch) |
| Gehört zu | [ARCHITECTURE.md](ARCHITECTURE.md), Abschnitt 11 und 15 |

Inhalt: [1. Voraussetzungen](#1-voraussetzungen) · [2. Paket holen und prüfen](#2-paket-holen-und-prüfen) ·
[3. Wo der Launcher Dateien ablegt](#3-wo-der-launcher-dateien-ablegt) ·
[4. Ergebnisse festhalten](#4-ergebnisse-festhalten) · [5. Testfälle je Arbeitspaket](#5-testfälle-je-arbeitspaket) ·
[6. Optional: Windows 7 SP1 in einer VM](#6-optional-windows-7-sp1-in-einer-vm)

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
  `HKCU\Software\SSSI` und `HKCU\Software\Mad Doc Software`. Den Schlüssel `Software\Sierra\CDKeys` nie ändern
  oder löschen.
- **Netzwerk**: Der Launcher fragt im Hintergrund die Online-Spielerliste beim NeoEE-Statusserver ab
  (`titan.empireearth.eu`, Port 10005, eingestellt in `Empire Earth Launcher.exe.config`). Weitere Verbindungen
  kommen erst mit späteren Arbeitspaketen dazu und stehen dann bei deren Testfällen.

## 2. Paket holen und prüfen

### 2.1 Laptop-Paket

Das Laptop-Paket entsteht aus dem lokalen Release-Build, der den CI-Build nachbildet (gleiche
Referenz-Assemblys `Microsoft.NETFramework.ReferenceAssemblies.net48` 1.0.3, Release, alle Tests grün). Es
kommt nicht ins Repository. Es besteht aus zwei Dateien:

- `Empire-Earth-Launcher-v2-<Stand>.zip` mit den Ordnern `Empire-Earth-Launcher\` (Programm, `.exe.config`,
  Bibliotheken – ab L-WP2 auch `Empire_Earth_Launcher_Core.dll` –, Sprachordner `de\` und `fr\`) und `Empire-Earth-Mod-Creator\`, dazu `LICENSE`,
  `THIRD-PARTY-NOTICES.md` und `THIRD-PARTY-LICENSES.txt`;
- `Empire-Earth-Launcher-v2-<Stand>.zip.sha256` mit der SHA-256-Prüfsumme.

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

## 3. Wo der Launcher Dateien ablegt

Alle Dateien des Launchers liegen pro Benutzer unter `%LOCALAPPDATA%\Empire Earth Launcher\` (öffnen mit
`Win+R` → `%LOCALAPPDATA%\Empire Earth Launcher`). Neben das Programm schreibt der Launcher nie.

| Was | Ort | seit |
|---|---|---|
| Protokoll | `log.txt`, ältere Einträge in `log.txt.old` (ab 1 MiB gekürzt) | heute |
| Einstellungen (Spielordner, Theme) | `settings.json`; eine beschädigte Datei wird zu `settings.json.damaged`, beim Speichern entsteht kurz `settings.json.tmp` | L-WP2 |
| Sicherungen (`.reg`-Dateien, verschobene WON-Dateien) | `Backups\<yyyy-MM-dd_HHmmss>_<was>\` | ab L-WP5 |
| Arbeitsordner des Mod-Creators | `Mod Creator\` | heute |

Testpakete vor L-WP2 speicherten die Einstellungen in einer `user.config` in einem von .NET angelegten
Unterordner von `%LOCALAPPDATA%` (Ordnername mit Hash). Der Launcher liest sie nicht mehr; finden und danach
löschen:

```powershell
Get-ChildItem $env:LOCALAPPDATA -Recurse -Filter user.config -ErrorAction SilentlyContinue |
  Where-Object FullName -like '*Empire*' | Select-Object FullName
```

Datenschutz: `log.txt` enthält Pfade mit dem Benutzernamen; vor dem Weitergeben ansehen. Der Ordner `Backups`
enthält ab L-WP8 Login-Daten des Spiels (WON-Dateien) und wird nie weitergegeben.

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

### L-WP2 – Core-Grundlage (settings.json, Protokoll, Mutation Guard)

Die Einstellungen liegen jetzt in `settings.json` (Abschnitt 3). Die Fälle brauchen kein installiertes Spiel.
Vor WP2-01 eine vorhandene `settings.json` (und `settings.json.damaged`) löschen. Zum Bearbeiten der Datei den
Launcher immer erst schließen; Editor: Notepad. Die Oberfläche ist bis L-WP3 englisch, die Schaltflächen heißen
hier so, wie sie heute angezeigt werden.

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

Wird mit L-WP3 ergänzt.

### L-WP4 – Installationserkennung (Vertrag 1)

Wird mit L-WP4 ergänzt.

### L-WP5 – Spieleinstellungen (Vertrag 3)

Wird mit L-WP5 ergänzt.

### L-WP6 – Spielstart (Vertrag 3.7 und 4.2)

Wird mit L-WP6 ergänzt.

### L-WP7 – Integrität und Reparatur (Vertrag 2 und 4)

Wird mit L-WP7 ergänzt.

### L-WP8 – Wartungswerkzeuge

Wird mit L-WP8 ergänzt.

### L-WP9 – Netzwerkdiagnose, Bericht, Laptop-Paket

Wird mit L-WP9 ergänzt.

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
