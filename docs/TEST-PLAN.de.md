# Testplan Launcher v2 (Test auf einem echten Windows-Rechner)

Dieser Testplan beschreibt den manuellen Test von Empire Earth Launcher v2 und dem Mod-Creator auf einem echten
Windows-Rechner (dem Laptop-Test). Er prüft, was die automatischen Tests unter Mono nicht prüfen können:
echte Registry-Ansichten, Mutexe, Programmstart, Bildschirm und Skalierung, TLS unter Windows, die Darstellung
von Krypton und die Texte der Oberfläche ([ADR 0012](adr/0012-test-strategy.md)).

| | |
|---|---|
| Stand | Fälle von L-WP1 bis L-WP5; jedes weitere Arbeitspaket ergänzt seinen Abschnitt in 5 und die Zuordnung in 7 im selben Commit |
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

Pakete ab L-WP6 enthalten zusätzlich den Ordner `Tests\` mit dem Testprogramm `Empire-Earth-Launcher.Tests.exe` und
seinen Bibliotheken (ohne Quelltexte), damit die automatischen Tests einmal unter dem echten .NET Framework 4.8
laufen (WP1-11; [ADR 0012](adr/0012-test-strategy.md), Ergänzung nach der Planprüfung).

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
| Einstellungen (Spielordner, Theme, ab L-WP3 Sprache) | `settings.json`; eine beschädigte Datei wird zu `settings.json.damaged`, beim Speichern entsteht kurz `settings.json.tmp` | L-WP2 |
| Sicherungen (`.reg`-Dateien, ab L-WP8 auch verschobene WON-Dateien) | `Backups\<yyyy-MM-dd_HHmmss>_<was>\`, z. B. `Backups\2026-10-02_153012_reset-game-settings\2026-10-02_153012_NeoEE_EE.reg` | ab L-WP5 |
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

Wird mit L-WP6 ergänzt.

### L-WP7 – Integrität und Reparatur (Vertrag 2 und 4)

Wird mit L-WP7 ergänzt. Vorgemerkt: vollständige Prüfung starten, dann das Setup starten – das Setup läuft ohne
Fehlerdialog durch, die Prüfung meldet „abgebrochen“ und läuft nach dem Setup neu.

### L-WP8 – Wartungswerkzeuge

Wird mit L-WP8 ergänzt. Vorgemerkt: nach der Registry-Bereinigung existiert `Software\Sierra\CDKeys` unverändert;
echte HKCU-Reste von CD- und GOG-Installationen werden notiert (Beleg für spätere Einträge der Liste).

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

## 7. Zuordnung der Anforderungen und Forum-Testfälle

Jede Anforderung R1 bis R10 und R17 der v2-Planung und jeder Testfall für echtes Windows aus dem Forenbericht
(Abschnitt 8, Nummern 1 bis 22) ist hier Fällen dieses Plans zugeordnet. „offen (L-WPn)“ heißt: Die Fälle kommen mit
diesem Arbeitspaket. „Setup:“ heißt: Der Fall gehört (auch) in den Testplan des Setup-Repositorys, mit dessen
Fall-IDs; „entfällt:“ nennt den Grund. Mehrere Angaben sind durch `;` getrennt. Ein automatischer Test
(`TestPlanTests`) prüft die Tabelle: jede Zeile genau einmal, jede genannte Fall-ID existiert, „offen“ nur für
Pakete nach dem Stand dieses Plans und ab L-WP9 gar nicht mehr.

| Bezug | Thema | Zuordnung |
|---|---|---|
| R1 | Standardwerte pro Benutzer, auch für andere Konten | WP5-01, WP5-02, WP5-03, WP5-04 |
| R2 | Integritätsmanifest | offen (L-WP7) |
| R3 | Spielen, laufende Instanzen, Kompatibilitätsoptionen | WP5-12, WP5-13, WP5-14; offen (L-WP6) |
| R4 | Spieleinstellungen zurücksetzen mit `.reg`-Sicherung | WP5-08, WP5-09, WP5-10, WP5-11 |
| R5 | Registry-Bereinigung | offen (L-WP8) |
| R6 | WON-Login zurücksetzen | offen (L-WP8) |
| R7 | Netzwerkdiagnose | offen (L-WP9) |
| R8 | VirtualStore | WP4-16; offen (L-WP8) |
| R9 | Reparatur über das Setup | offen (L-WP6) |
| R10 | Spielstände und Szenarien | offen (L-WP8) |
| R17 | Texte auf Englisch, Deutsch, Französisch | WP3-01, WP3-03, WP3-11, WP3-12, WP4-17, WP5-17 |
| Forum 1 | Frische Installation, Standardnutzer, zweites Konto | WP4-06, WP4-16, WP5-02; Setup: TP-41, TP-71 (Installation und Rechte) |
| Forum 2 | Versionsanzeige, Mehrspieler ohne Versionskonflikt | offen (L-WP7); Setup: TP-70, TP-72 (Version im Hauptmenü) |
| Forum 3 | Grafikmatrix mit und ohne Wrapper | WP5-05; Setup: TP-23 (Wrapper und Renderer installiert das Setup) |
| Forum 4 | Farbtiefe 16 Bit, Rücksetzen auf 32 Bit | WP5-06, WP5-08 |
| Forum 5 | Kompatibilitätsflags, Windows 7 | WP5-12, WP5-13, WP5-14; Setup: TP-20, TP-21, TP-22 (Werte des Setups) |
| Forum 6 | Auflösungsgrenzen, Bildschirm unter 768 Pixel | WP5-07, WP5-15 |
| Forum 7 | AoC ohne vorherigen EE-Start | WP5-02, WP5-19 |
| Forum 8 | Alt-Installation (CD, GOG) vorhanden | WP4-08, WP4-15 |
| Forum 9 | EE und NeoEE parallel, eines deinstallieren | WP4-12; Setup: TP-62, TP-75 (Deinstallation) |
| Forum 10 | Firewall beim Hosten | Setup: TP-76 (Firewall-Regeln legt nur das Setup an, der Launcher ändert die Firewall nicht) |
| Forum 11 | Hosting-Varianten, Portweiterleitung | offen (L-WP9) |
| Forum 12 | Netzwerkadapter (VPN, Hamachi) | offen (L-WP9) |
| Forum 13 | CD-Keys: Server gesperrt, VM, `CDKeyCheck` | offen (L-WP9); Setup: TP-77 (nur das Setup registriert CD-Keys) |
| Forum 14 | Antivirus löscht Dateien | WP4-13; offen (L-WP7) |
| Forum 15 | Offline, nur Spiegel, manipulierter Download | Setup: TP-00, TP-10, TP-11, TP-16 (Downloads macht nur das Setup) |
| Forum 16 | Sprachen: Deutsch für EE und AoC | Setup: TP-78 (Sprachdateien des Spiels installiert das Setup) |
| Forum 17 | Spielstände im Mehrspieler, Namen mit Sonderzeichen | offen (L-WP8) |
| Forum 18 | Laufende Instanz | WP5-10; offen (L-WP6); Setup: TP-79 |
| Forum 19 | Kampagnen-Tribut | entfällt: Spiellogik der Spieldateien, die weder Launcher noch Setup ändern |
| Forum 20 | Launcher: Spielerliste ohne Netz, beschädigte Einstellungen, Pfad mit Umlauten | WP1-04, WP2-05, WP5-19 |
| Forum 21 | GOG als Basis | WP4-08; Setup: TP-63 |
| Forum 22 | NeoEE-Wartungsmodus über kaputter Installation | Setup: TP-73 (die Reparatur macht das Setup); offen (L-WP6) |
