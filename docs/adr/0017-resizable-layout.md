# 0017 Resizable layout

Status: **Accepted** (2026-10-06)

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
- The Play page and the Launcher page keep their fixed geometry in this decision; they fill the page area at the top left until
  they are reworked (work package L3).

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
  only show on Windows. Rejected for 1.1.0; the Play and Launcher pages may use it (L3).
- **Anchor the controls left and right and keep the fixed heights.** A label that grows with the width keeps its height, so a
  German text is cut off when the window is narrow. Rejected: the heights must come from the text.
- **Fix the window size** (no maximize box). Hides the report instead of fixing it and takes a feature from the user.
  Rejected (user decision for 1.1.0).
- **Cap the content width** (a centered column). Rejected for 1.1.0; the user asked for content that grows.
