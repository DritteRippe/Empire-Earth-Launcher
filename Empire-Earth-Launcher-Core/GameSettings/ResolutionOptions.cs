using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.GameSettings
{
    /// <summary>The shape of a game window size, for the hint of the graphics page (the menus of the game are made for 4:3).</summary>
    public enum AspectKind
    {
        /// <summary>4:3, e.g. 1024x768: what the menus of the game are made for (forum t=5522 p=37176).</summary>
        FourByThree,

        /// <summary>5:4, e.g. 1280x1024.</summary>
        FiveByFour,

        /// <summary>16:10, e.g. 1440x900.</summary>
        SixteenByTen,

        /// <summary>16:9, e.g. 1600x900; widescreen cuts the picture off in the menus (forum t=5022 p=34284).</summary>
        SixteenByNine,

        /// <summary>Any other shape.</summary>
        Other
    }

    /// <summary>One game window size the graphics page offers.</summary>
    public sealed class ResolutionOption
    {
        internal ResolutionOption(ScreenSize size, AspectKind aspect, bool isRecommended)
        {
            Size = size;
            Aspect = aspect;
            IsRecommended = isRecommended;
        }

        public ScreenSize Size { get; }

        public AspectKind Aspect { get; }

        /// <summary>True for the size the setup and the launcher's defaults write on this computer (<see cref="ComputedValues.GameWindow"/>).</summary>
        public bool IsRecommended { get; }

        public override string ToString()
        {
            return Size + " (" + Aspect + (IsRecommended ? ", recommended" : string.Empty) + ")";
        }
    }

    /// <summary>
    /// The game window sizes the graphics page offers for <c>Game Window Width</c> and <c>Game Window Height</c>
    /// (contract 3.2, 3.3): the usual 4:3, 5:4, 16:10 and 16:9 sizes that fit the primary screen, never above 1920x1080 and
    /// never below 1024x768, and always the recommended size of this computer. Launcher 1.1.0 stops at the limits of contract
    /// 3.3 (forum reports of crashes above 1080p, t=5831 p=39111, t=4277 p=30460; the project owner decided to keep them).
    /// </summary>
    /// <remarks>
    /// The screen is the one <see cref="ComputedValues.GameWindow"/> uses: the primary screen in physical pixels, else the size a
    /// DPI-unaware program sees. A size the game cannot show on a scaled screen is not hidden: the consistency checks of the
    /// Game settings page name it (<see cref="FindingCode.WindowFitsOnlyWithHighDpiAware"/>).
    /// </remarks>
    public static class ResolutionOptions
    {
        /// <summary>Within a tolerance of this, a ratio counts as one of the usual ones (1366x768 is 16:9).</summary>
        private const double RatioTolerance = 0.02;

        /// <summary>The sizes of the list before it is limited by the screen and the limits of contract 3.3.</summary>
        private static readonly IReadOnlyList<ScreenSize> KnownSizes = new ReadOnlyCollection<ScreenSize>(new[]
        {
            new ScreenSize(1024, 768),
            new ScreenSize(1152, 864),
            new ScreenSize(1280, 800),
            new ScreenSize(1280, 960),
            new ScreenSize(1280, 1024),
            new ScreenSize(1366, 768),
            new ScreenSize(1400, 1050),
            new ScreenSize(1440, 900),
            new ScreenSize(1600, 900),
            new ScreenSize(1680, 1050),
            new ScreenSize(1920, 1080),
        });

        /// <summary>The offered sizes for this computer, smallest width first, then smallest height.</summary>
        public static IReadOnlyList<ResolutionOption> For(ISystemInfo systemInfo)
        {
            if (systemInfo == null)
                throw new ArgumentNullException(nameof(systemInfo));
            ScreenSize screen = !systemInfo.PrimaryScreen.IsEmpty ? systemInfo.PrimaryScreen : systemInfo.PrimaryScreenUnaware;
            return For(screen, ComputedValues.GameWindow(systemInfo));
        }

        /// <summary>
        /// The offered sizes for a screen of <paramref name="screen"/> (empty if unknown: then only the recommended size, the
        /// minimum) with the recommended size <paramref name="recommended"/>.
        /// </summary>
        internal static IReadOnlyList<ResolutionOption> For(ScreenSize screen, ScreenSize recommended)
        {
            IEnumerable<ScreenSize> sizes = screen.IsEmpty
                ? Enumerable.Empty<ScreenSize>()
                : KnownSizes.Where(size => size.Width <= screen.Width && size.Height <= screen.Height);
            return new ReadOnlyCollection<ResolutionOption>(sizes.Concat(new[] { recommended })
                .Where(IsWithinLimits)
                .Distinct()
                .OrderBy(size => size.Width)
                .ThenBy(size => size.Height)
                .Select(size => new ResolutionOption(size, AspectOf(size), size == recommended))
                .ToList());
        }

        /// <summary>
        /// True if <paramref name="size"/> is inside the limits of contract 3.3: 1024 to 1920 wide and 768 to 1080 high. The
        /// service that writes the values refuses every other size, whatever the page offered.
        /// </summary>
        public static bool IsWithinLimits(ScreenSize size)
        {
            return size.Width >= ComputedValues.MinGameWindowWidth && size.Width <= ComputedValues.MaxGameWindowWidth &&
                   size.Height >= ComputedValues.MinGameWindowHeight && size.Height <= ComputedValues.MaxGameWindowHeight;
        }

        /// <summary>The shape of <paramref name="size"/>.</summary>
        public static AspectKind AspectOf(ScreenSize size)
        {
            if (size.IsEmpty)
                return AspectKind.Other;
            double ratio = (double)size.Width / size.Height;
            if (Math.Abs(ratio - 4.0 / 3.0) <= RatioTolerance)
                return AspectKind.FourByThree;
            if (Math.Abs(ratio - 5.0 / 4.0) <= RatioTolerance)
                return AspectKind.FiveByFour;
            if (Math.Abs(ratio - 16.0 / 10.0) <= RatioTolerance)
                return AspectKind.SixteenByTen;
            if (Math.Abs(ratio - 16.0 / 9.0) <= RatioTolerance)
                return AspectKind.SixteenByNine;
            return AspectKind.Other;
        }
    }
}
