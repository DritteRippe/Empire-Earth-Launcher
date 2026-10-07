# 0017 Resizable layout

Status: **Accepted** (2026-10-06), amended 2026-10-06 (Play and Launcher pages) and 2026-10-07 (the Graphics and Mods pages)

## Context

Until 1.0.0 the main window was sizable, but nothing in it reacted: `MainForm` placed the four pages at x = 126 with the
fixed size 554 x 380 and the navigation buttons at fixed places, and the pages placed their controls at fixed widths (505 px)
and, on the Game settings page, with fixed heights for some texts. A maximized window showed the same content in the upper
left corner and a wide empty area (bug report of 2026-10-06, screenshots of the normal and the maximized window). The Game
settings page and the Tools page already stacked their controls from the heights of their texts
(`LauncherWrapLabel.TextHeight`), but never changed a width. The user decided for 1.1.0: the window is freely resizable, its
minimum size stays as it is today, and the content grows with the window; the launcher stays DPI-unaware
([ADR 0011](0011-screen-size-in-physical-pixels.md)).

## Decision

- **Main window.** The four navigation buttons sit in a `Panel` docked at the left edge (`navigationPanel`, 126 px, the
  buttons keep their places inside it); the four pages are `Dock = Fill`, so each page gets the rest of the client area. The
  panel is added after the pages, because WinForms docks the last control first. The minimum size is the size the window opens
  with (`MinimumSize = Size` in `OnLoad`, when the window has its real frame): "as today" without a pixel guess for the frame
  of the Krypton form. There is no maximum.
- **Scrolling pages: one layout class.** `ScrollPageLayout` stacks the controls of the Game settings page and of the Tools
  page, which both show a long column in an `AutoScroll` panel. It takes the content width from the width of the panel minus
  the scroll bar (the bar's width is reserved whether it is shown or not, so that its appearing never changes the width and a
  layout never triggers another) and the margins, and gives it to every text, list and box; a wrapping label gets its height
  from `LauncherWrapLabel.TextHeight` at that width; Krypton labels, check boxes and buttons are as high as their preferred
  height. Buttons keep the width of the designer or the width their text needs, and a row of buttons starts a new line where it
  does not fit. The page runs the layout again when the width of its panel changes (`SizeChanged`, only for a changed width, so
  the scroll bar cannot start a loop) and keeps the scroll position.
- **A text that cannot wrap.** A Krypton check box or label shows one line. If its text is wider than the content (a long
  translation, a large Windows font, the smallest window), the content gets that width in a second pass and the page scrolls
  sideways, so that no text is cut off.
- **The warning of the compatibility options** (the book picture, the text and the button) is a block of the content width
  whose parts are placed in code from the width of the block: the picture (66 x 66, its drawn size) centered, the text below it
  as high as it needs, the button centered under it, and the block as high as that sums up to. The four controls and their
  names stay ([ADR 0014](0014-only-working-features-in-the-ui.md)).
- **Sentences are wrapping labels.** The installation line ("NeoEE in C:\...", a path of any length) of the Game settings page
  and of the Tools page and the line "no hints" were Krypton labels of one line; they are `LauncherWrapLabel` now
  (`installationKryptonWrapLabel`, `integrityInstallationKryptonWrapLabel`, `hintsNoneKryptonWrapLabel`) and wrap. The
  headings stay Krypton labels with `AutoSize = false`, so that Krypton does not resize them against the layout.
- The Play page and the Launcher page are reworked in the amendment below (work package L3).

## Evidence

- `PageLayoutTests` (ADR 0012 amendment of 2026-10-06) measures the Game settings page in every state at the window sizes
  minimum, 800 x 500, 1024 x 640 and 1920 x 1080, in English, German and French, with the system font and 50 % larger: no
  overlap, nothing outside its page, no cut-off text, the content grows. All rules hold; the list of known defects it carried
  until now is gone. On Windows the same test measures the Tools page.
- `ScrollPageLayoutTests` tests the layout class on a small panel, also what the Tools page needs and Mono cannot create:
  widths, heights, rows that wrap, hidden controls, the second pass for a text that cannot wrap, the same result when it runs
  twice.
- `MainWindowLayoutTests` (source tree) checks the docking of the navigation panel and the pages and the minimum size.
- The bug report's screenshots: content at the left of a maximized window; the overlap of the header, description and
  installation line on the Game settings page had another cause (ADR 0012 amendment).

## Consequences

- Texts get the width of the window. On a very wide window a line of text is long; the launcher does not limit it, as the
  decision is "the content grows with the window".
- The window cannot be made smaller than it opens. On a screen smaller than that the window is as large as the screen allows.
- A control added to the Game settings page or the Tools page is stacked by the layout (a line in `PlaceControls`), not placed
  at fixed coordinates; the designer coordinates are the starting values only.
- Mono shows the logic only; Windows (CI, laptop) shows what the fonts of Windows do. The pictures of the build
  (`page-pictures`) show the pages at the minimum size and at 1024 x 640; the manual cases WP1-07 and WP5-17 of the test plan
  cover the maximized window and a large font.
- The launcher remains DPI-unaware: everything is in the logical pixels Windows gives it; the layout depends only on the
  size of the window and the preferred sizes of the controls.

## Alternatives considered

- **Rebuild all pages on `TableLayoutPanel` / `FlowLayoutPanel`.** Designer-friendly, but a wrapped label in such a panel needs
  its maximum width kept in sync with the column, and the change touches every page and its resx at once, with regressions that
  only show on Windows. Rejected for 1.1.0, for the Play and Launcher pages as well (see the amendment).
- **Anchor the controls left and right and keep the fixed heights.** A label that grows with the width keeps its height, so a
  German text is cut off when the window is narrow. Rejected: the heights must come from the text.
- **Fix the window size** (no maximize box). Hides the report instead of fixing it and takes a feature from the user.
  Rejected (user decision for 1.1.0).
- **Cap the content width** (a centered column). Rejected for 1.1.0; the user asked for content that grows.

## Amendment 2026-10-06 (Play and Launcher pages, work package L3)

The Play page and the Launcher page used fixed slots for their texts (a label of 30 pixels for the program versions, 28 for the
result of the version check, 18 for the integrity state, 40 for the state line; a group box of 540 x 364 pixels) and stayed
at the upper left of a larger window. They use the same layout class now, which got three small additions for them
(`ScrollPageLayout` constructor: the margin and whether the width of the scroll bar is reserved; `ContentHeight`;
`Place(control, indent)`; `PlaceField`; radio buttons get their height from their text like the other Krypton controls).

- **A block that does not scroll.** The content of a group box (`gameSettingsKryptonGroupBox.Panel`,
  `launcherSettingsKryptonGroupBox.Panel`) and the info bar are laid out by a `ScrollPageLayout` with its own margin and
  without room for a scroll bar; `ContentHeight` is the height the block needs, and the group is made that high plus what it
  takes besides its content (heading, borders: `Height - Panel.Height`, which does not depend on the content). Krypton sizes
  the panel of a group itself when the group is laid out, so the pages also lay themselves out when the size of such a panel
  changes (a theme with another heading font).
- **Play page, two columns.** The online players (the group with the player list, the state line and the profile line) and
  the Play button keep the width of the designer at the right edge and have the height of the page: the group runs from the
  top down to the Play button, the player list takes the room between the heading and the profile line, and the state line
  above the profile line takes the height of its text (`MaxLobbyStatusHeight` for a small list, more where the column is
  taller; the list keeps 60 pixels at least). The rest of the width is the game column: a plain panel that scrolls
  (`gameColumnPanel`, white like the page, as the scroll panels of the other pages) with the group of the game choice and, below
  it, the info bar. The four texts of the group are wrapping labels as high as their text needs; one without text is hidden and
  takes no room, so the group has no empty lines. The info bar is a block as wide as the group and as high as its text and its
  two buttons need (the buttons wrap into a second line in a narrow window). The info bar is in the column, not docked at the
  bottom of the page: where the texts of a large font or a small window do not fit it scrolls with the group, and nothing
  sticks out of the page.
- **Launcher page, one group.** A plain panel that scrolls (`launcherScrollPanel`, `Dock = Fill`) holds the group of the
  settings, which has the width of the panel and the height of its content, or the height the window leaves if that is more:
  the list of the installations takes the difference (at least the height of the designer), so a maximized window shows a
  large list and the hints below it. The rows of the form are placed by `PlaceField`: the three labels share one column (as wide
  as the widest label needs), so that the fields start at the same place; the lists of theme and language keep their width, the
  text box of the game folder takes what the two buttons leave and, where that is less than 120 pixels, the buttons go below it;
  the line with the origin of the choice is indented to the fields.
- **Sentences are wrapping labels** (as in the amendment above for the installation line): the line with the origin of the game
  folder and the note that a new language is used from the next start on were Krypton labels of one line; they are
  `LauncherWrapLabel` now and wrap (`gameDirectorySourceKryptonWrapLabel`, `uiLanguageHintKryptonWrapLabel`).
  The headings and the labels of the rows stay Krypton labels with `AutoSize = false`.
- **A page is laid out when it is laid out.** The two pages run their layout in `OnLayout` (and when a text that changes a
  height changes), not when their size changes: a page that is created at the size it will have gets no `SizeChanged`, and its
  first layout would never happen. `PageLayoutTests` found this on the Tools page too, which is laid out by its state in the
  launcher and was not laid out at its first size in the test (under Mono with stubs); the test now goes through another size first
  (`LauncherPages.Resize`), as a window that is resized does.
- **Hidden controls.** The Play page and the Launcher page keep the visibility they want in a dictionary (as the Game settings
  page does: `Control.Visible` reads false while a page is hidden), seeded from the designer (info bar, integrity button and
  restart hint are hidden there). The designer sizes of the buttons next to each other are 120 pixels (they were 144 and 150),
  so that two of them fit next to each other in the smallest window.
- **The Play button** keeps the size of the designer. Krypton adds the golden picture (251 x 84 pixels, also its face) to the
  preferred width of the button, which is then wider than the whole column; the rule "wide enough for the text" of the
  geometry tests excuses the button by name (`LayoutChecker.NarrowByDesign`), with that reason. The link of the player list
  is a line of one text in a column of 210 pixels: in a font 50 % larger Krypton shortens it with an ellipsis, which the rule
  excuses by name as well. Both are to be looked at on the page pictures of the build.

### Evidence

- `PageLayoutTests` measures the Play page in five states (designer, long texts of the game group, info bar with the display
  question, both together, state line of the player list) and the Launcher page in two (designer, long texts and hints), at the
  four window sizes, in English, German and French, with the system font and one 50 % larger: no overlap, nothing outside, no
  cut-off text, the content grows. The cases create the Krypton combo box and the lists, so they run on Windows (CI, laptop) and
  are ignored under Mono; the author ran them under Mono with the Windows libraries that Krypton asks for replaced by stubs
  (all measures are Mono's) and with a picture of the bounds of every control; the first real run is the one of the build.
- `ScrollPageLayoutTests` tests what the pages need from the layout class on a small panel (own margin, no reserved scroll bar,
  `ContentHeight`, indent, radio buttons, rows of a form with the field taking the rest, the buttons wrapping below a field that
  would be too narrow, a hidden row); `LayoutCheckerTests` the rules for radio buttons and the named excuses.
- `LobbyStatusTests` covers the state line taking more than the default where its caller allows it.

### Consequences

- The Play page shows the info bar below the group and scrolls with it. In the smallest window with a long German question
  the bar may be below the fold; the Play button and the player list stay where they are.
- The state line of the player list may take more than four lines in a tall column (a large window, a large font), and the list
  keeps 60 pixels.
- A control added to one of these pages is placed by `PlaceGameGroup`, `PlaceColumn`, `PlaceHint` or `PlaceGroupContent`; the
  designer coordinates are the starting values only.

## Amendment 2026-10-07 (the Graphics page)

- The main window has a fifth page and a fifth navigation button (*Graphics*, between *Settings* and *Tools*; the buttons keep
  their places inside the panel, the last one ends at 338 px of the 381 px of the smallest window). The page is `Dock = Fill` like
  the others and is added before the panel (`MainWindowLayoutTests` lists it).
- `GraphicsUserControl` is stacked by `ScrollPageLayout` from the start: wrapping labels over the content width, one row of a
  form for the list of sizes (label in its natural width, the combo box at its designer width of 240 px, which holds the longest
  entry in every language and stays below half of the smallest page, so the rule "grows with the page" does not apply), the
  button of the list in a line of its own so that a large font cannot push it out of the page, and the page scrolls. Hidden
  controls take no room and visibility comes from `IsShown`, not `Control.Visible`.

## Amendment 2026-10-07 (the Mods page)

- The main window has a sixth page and a sixth navigation button (*Mods*, between *Graphics* and *Tools*), which exists only
  while the selected installation has dreXmod 3. Six buttons of 52 px must fit the 381 px of the smallest window, so the first
  button moved up from 78 to 66 px (the last one ends at 378 px); the page is `Dock = Fill` like the others and is added before
  the panel (`MainWindowLayoutTests`). When the button is hidden the buttons below it move up by one step
  (`NavigationStack`, the step is the distance of the first two buttons of the designer, so a larger font that scales the
  buttons scales it too), and if the page was open the Play page is shown.
- `ModsUserControl` is stacked by `ScrollPageLayout` like the Graphics page: wrapping labels over the content width, per game a
  heading, the choice of the config, the list of the presets (one wrapping label, one block of one or two lines per preset) and
  a row of the two buttons that wraps into a second line where the width does not suffice; the page scrolls, hidden controls
  take no room and visibility comes from `IsShown`.
