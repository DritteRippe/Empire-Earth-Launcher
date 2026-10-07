using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Logging;

namespace Empire_Earth_Launcher.Core.Play
{
    /// <summary>
    /// The command line of the launcher (contract 1.4, "Default selection", revision 4): the only argument is
    /// <c>--product=EE</c> or <c>--product=NeoEE</c>, as the shortcuts of suite 1.0.0 and the players' own shortcuts pass it (the
    /// one shortcut of suite 1.1.0 passes none), and the second launcher of the hand-over. It selects a product for this
    /// session only, nothing is saved. Anything else is ignored and logged; the launcher never fails on its command line.
    /// </summary>
    public sealed class LauncherArguments
    {
        private LauncherArguments(Product sessionProduct, IEnumerable<string> ignored)
        {
            SessionProduct = sessionProduct;
            Ignored = new ReadOnlyCollection<string>(new List<string>(ignored));
        }

        /// <summary>No argument.</summary>
        public static LauncherArguments None { get; } = new LauncherArguments(null, new string[0]);

        /// <summary>The product of <c>--product=</c>; null if there was none, or no valid one.</summary>
        public Product SessionProduct { get; }

        /// <summary>The arguments that were ignored (invalid value of <c>--product</c>, unknown argument), as written.</summary>
        public IReadOnlyList<string> Ignored { get; }

        /// <summary>
        /// Reads <paramref name="args"/>: the name <c>--product</c> without regard to case, the value exactly <c>EE</c> or
        /// <c>NeoEE</c> (contract 1.4: another value is ignored). If it is given more than once, the last valid value counts.
        /// Every ignored argument is logged once as a warning; the selection is logged.
        /// </summary>
        /// <param name="args">The arguments of <c>Main</c> (null counts as none).</param>
        /// <param name="logger">Log of the launcher.</param>
        public static LauncherArguments Parse(IEnumerable<string> args, ILogger logger)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (args == null)
                return None;

            Product product = null;
            var ignored = new List<string>();
            string prefix = ContractNames.ProductArgumentName + "=";
            foreach (string arg in args)
            {
                if (arg != null && arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    Product parsed = ParseProduct(arg.Substring(prefix.Length));
                    if (parsed != null)
                    {
                        product = parsed;
                        continue;
                    }
                    logger.Warning("The command-line argument \"" + arg + "\" is ignored: the value must be EE or NeoEE.");
                }
                else
                {
                    logger.Warning("The command-line argument \"" + arg + "\" is not known and is ignored.");
                }
                ignored.Add(arg);
            }

            if (product != null)
                logger.Info("Command line: " + prefix + product.Id + ", this session starts with that product.");
            return new LauncherArguments(product, ignored);
        }

        /// <summary>The product of a value of <c>--product=</c>: exactly <c>EE</c> or <c>NeoEE</c>; null for anything else.</summary>
        public static Product ParseProduct(string value)
        {
            foreach (Product product in Product.All)
            {
                if (string.Equals(product.Id, value, StringComparison.Ordinal))
                    return product;
            }
            return null;
        }
    }
}
