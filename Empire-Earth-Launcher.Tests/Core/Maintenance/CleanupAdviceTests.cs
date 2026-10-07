using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Core.Platform;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Maintenance
{
    /// <summary>
    /// The advice test of the ADR 0007 plan review: the advice of every entry of the cleanup list in every state (codes and
    /// parameters) goes through the canonical form of the write policy, and no advice names <c>Software</c>,
    /// <c>Software\Sierra</c> or another ancestor of <c>Software\Sierra\CDKeys</c> (nor any other protected key) as something to
    /// delete. <c>Software\Sierra</c> is always "do not delete: contains the CD keys"; the advice to export and delete with
    /// the Registry Editor is given only for the SSSI and Mad Doc keys.
    /// </summary>
    [TestFixture]
    public class CleanupAdviceTests
    {
        private static readonly CleanupState[] States = (CleanupState[])Enum.GetValues(typeof(CleanupState));

        /// <summary>Every advice the list can give: each entry in each state, with and without existing CD keys.</summary>
        private static IEnumerable<Tuple<CleanupEntry, CleanupState, CleanupAdvice>> EveryAdvice()
        {
            foreach (CleanupEntry entry in CleanupCandidates.All)
            {
                foreach (CleanupState state in States)
                {
                    foreach (bool? cdKeys in new bool?[] { true, false, null })
                        yield return Tuple.Create(entry, state, CleanupAdvice.For(entry, state, @"D:\Old\Empire Earth", cdKeys));
                }
            }
        }

        [Test]
        public void NoAdvice_NamesAProtectedKeyOrAnAncestorOfTheCdKeys_ForDeletion()
        {
            var targets = new List<RegistryLocation>();
            foreach (var advice in EveryAdvice())
            {
                RegistryLocation target = advice.Item3.DeletionTarget;
                if (target == null)
                    continue;
                targets.Add(target);
                RegistryLocation canonical = RegistryPath.Canonicalize(target);
                Assert.That(RegistryWritePolicy.ProtectionOf(target), Is.EqualTo(RegistryWriteDenial.None), advice.Item3.ToString());
                Assert.That(canonical.Path, Is.Not.EqualTo("SOFTWARE").And.Not.EqualTo(@"SOFTWARE\SIERRA"), advice.Item3.ToString());
                foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                {
                    foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
                    {
                        var cdKeys = new RegistryLocation(hive, view, @"Software\Sierra\CDKeys");
                        Assert.That(RegistryPath.IsSameOrBelow(cdKeys, target), Is.False, "an ancestor of " + cdKeys + ": " + advice.Item3);
                    }
                }
            }
            Assert.That(targets, Is.Not.Empty, "the test is not vacuous");
        }

        [Test]
        public void SoftwareSierra_IsAlways_DoNotDelete()
        {
            foreach (var advice in EveryAdvice().Where(advice => advice.Item1.Scope == CleanupScope.Protected))
            {
                Assert.That(advice.Item3.Code, Is.EqualTo(CleanupAdviceCode.DoNotDeleteContainsCdKeys), advice.Item3.ToString());
                Assert.That(advice.Item3.DeletionTarget, Is.Null);
                Assert.That(RegistryPath.Canonicalize(advice.Item3.Key).Path, Is.EqualTo(@"SOFTWARE\SIERRA"));
            }
            Assert.That(EveryAdvice().Where(advice => advice.Item3.Code == CleanupAdviceCode.DoNotDeleteContainsCdKeys)
                                     .Select(advice => advice.Item1.Scope).Distinct(), Is.EqualTo(new[] { CleanupScope.Protected }));
        }

        [Test]
        public void TheAdviceToDeleteWithTheRegistryEditor_IsOnlyForTheSssiAndMadDocKeys()
        {
            List<CleanupAdvice> manual = EveryAdvice().Select(advice => advice.Item3)
                .Where(advice => advice.Code == CleanupAdviceCode.ExportThenDeleteAsAdministrator).ToList();

            Assert.That(manual, Is.Not.Empty);
            foreach (CleanupAdvice advice in manual)
            {
                Assert.That(advice.Key.Hive, Is.EqualTo(RegistryHive.LocalMachine), advice.ToString());
                Assert.That(RegistryPath.Canonicalize(advice.Key).Path,
                    Is.EqualTo(@"SOFTWARE\SSSI\EMPIRE EARTH").Or.EqualTo(@"SOFTWARE\MAD DOC SOFTWARE\EE-AOC"), advice.ToString());
            }
        }

        [Test]
        public void OnlyAStaleEntry_IsNamedForDeletion_AndTheLauncherDeletesOnlyHkcuKeys()
        {
            foreach (var advice in EveryAdvice())
            {
                bool named = advice.Item3.DeletionTarget != null;
                Assert.That(named, Is.EqualTo(advice.Item2 == CleanupState.Stale && advice.Item1.Scope != CleanupScope.Protected),
                    advice.Item3.ToString());
                if (advice.Item3.Code == CleanupAdviceCode.LauncherCanDelete)
                    Assert.That(advice.Item3.Key.Hive, Is.EqualTo(RegistryHive.CurrentUser), advice.Item3.ToString());
            }
        }

        [TestCase(CleanupState.InstallationFound, CleanupAdviceCode.KeepInstallationFound)]
        [TestCase(CleanupState.FolderExists, CleanupAdviceCode.KeepFolderExists)]
        [TestCase(CleanupState.DriveNotFixed, CleanupAdviceCode.KeepDriveNotFixed)]
        [TestCase(CleanupState.NoFolderNamed, CleanupAdviceCode.KeepNoFolderNamed)]
        [TestCase(CleanupState.FolderUnknown, CleanupAdviceCode.KeepFolderUnknown)]
        [TestCase(CleanupState.Unreadable, CleanupAdviceCode.KeepUnreadable)]
        [TestCase(CleanupState.Stale, CleanupAdviceCode.ExportThenDeleteAsAdministrator)]
        public void EveryReasonToKeep_HasItsCode(CleanupState state, CleanupAdviceCode expected)
        {
            CleanupEntry hklm = CleanupCandidates.All.Single(entry => entry.Id == "hklm32-sssi-ee");

            CleanupAdvice advice = CleanupAdvice.For(hklm, state, @"D:\Old\Empire Earth", true);

            Assert.That(advice.Code, Is.EqualTo(expected));
            Assert.That(advice.Folder, Is.EqualTo(@"D:\Old\Empire Earth"));
            Assert.That(advice.CdKeysExist, Is.Null, "only Software\\Sierra says whether CD keys exist");
        }
    }
}
