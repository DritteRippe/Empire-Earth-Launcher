using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Empire_Earth_Launcher.Core.Contract
{
    /// <summary>
    /// A setup that holds a setup mutex while it runs (contract 4.2): the setup of a product, EE or NeoEE, or, since
    /// revision 4, the suite "Empire Earth Community" that runs the product setups. For the launcher all of them mean the
    /// same: no game start, no reading of <c>install.ini</c> and <c>files.sha256</c>, no change, until the mutex is gone.
    /// There are exactly three instances.
    /// </summary>
    public sealed class SetupKind
    {
        /// <summary>The setup of NeoEE (<c>NeoEE_Setup</c>).</summary>
        public static readonly SetupKind NeoEE = new SetupKind(Product.NeoEE.Id, Product.NeoEE.AppName,
            Product.NeoEE.SetupMutexName, Product.NeoEE);

        /// <summary>The setup of EE (<c>EE_Setup</c>).</summary>
        public static readonly SetupKind EE = new SetupKind(Product.EE.Id, Product.EE.AppName, Product.EE.SetupMutexName,
            Product.EE);

        /// <summary>
        /// The suite (<c>EmpireEarthCommunity_Suite</c>, contract revision 4): held for its whole run, also between the two
        /// product setups, where neither product mutex exists.
        /// </summary>
        public static readonly SetupKind Suite = new SetupKind("Suite", ContractNames.SuiteAppName,
            ContractNames.SuiteSetupMutexName, null);

        /// <summary>
        /// Every setup in the order the mutexes are looked at: NeoEE, EE, then the suite (the suite holds its mutex around a
        /// product setup, so a running product setup is named first).
        /// </summary>
        public static readonly IReadOnlyList<SetupKind> All = new ReadOnlyCollection<SetupKind>(new[] { NeoEE, EE, Suite });

        private SetupKind(string id, string appName, string mutexName, Product product)
        {
            Id = id;
            AppName = appName;
            MutexName = mutexName;
            Product = product;
        }

        /// <summary><c>NeoEE</c>, <c>EE</c> or <c>Suite</c>, for the log.</summary>
        public string Id { get; }

        /// <summary><c>AppName</c> of the setup, for the texts.</summary>
        public string AppName { get; }

        /// <summary>The mutex the running setup holds (session namespace, contract 4.2).</summary>
        public string MutexName { get; }

        /// <summary>The product of a product setup; null for the suite.</summary>
        public Product Product { get; }

        /// <summary>The setup of <paramref name="product"/>.</summary>
        public static SetupKind For(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            return All.First(kind => kind.Product == product);
        }

        public override string ToString()
        {
            return Id;
        }
    }
}
