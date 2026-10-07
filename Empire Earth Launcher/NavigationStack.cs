using System;
using System.Collections.Generic;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// Where the buttons of the navigation bar sit when one of them comes and goes (the button of the Mods page exists only for
    /// an installation with dreXmod 3): the buttons that are shown one below the other from <c>firstTop</c>, each
    /// <c>step</c> pixels below the one before; a button that is hidden takes no room.
    /// </summary>
    internal static class NavigationStack
    {
        /// <summary>The top of each button; for a button that is hidden the top the next button that is shown gets (never used).</summary>
        public static IReadOnlyList<int> Tops(int firstTop, int step, IReadOnlyList<bool> shown)
        {
            if (shown == null)
                throw new ArgumentNullException(nameof(shown));
            var tops = new int[shown.Count];
            int top = firstTop;
            for (int i = 0; i < shown.Count; i++)
            {
                tops[i] = top;
                if (shown[i])
                    top += step;
            }
            return tops;
        }
    }
}
