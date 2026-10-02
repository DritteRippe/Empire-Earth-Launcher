using System;
using System.Diagnostics;
using Empire_Earth_Launcher.Core.Platform;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>
    /// The start information of <see cref="ShellProcessStarter"/> (ADR 0010, contract 3.7 and 4.3): shell execute, the game
    /// folder as working folder, no arguments, no verb. Nothing is started; the real start is a test plan case.
    /// </summary>
    [TestFixture]
    public class ShellProcessStarterTests
    {
        private const string EeFolder = @"C:\Program Files (x86)\Neo Empire Earth\Empire Earth";
        private const string Program = EeFolder + @"\Empire Earth.exe";

        [Test]
        public void Contract_3_7_TheGameStartsThroughTheShellInItsFolderWithoutArguments()
        {
            ProcessStartInfo info = ShellProcessStarter.CreateProgramStartInfo(Program, EeFolder);

            Assert.That(info.UseShellExecute, Is.True, "a plain CreateProcess ignores the layers (error 740)");
            Assert.That(info.FileName, Is.EqualTo(Program));
            Assert.That(info.WorkingDirectory, Is.EqualTo(EeFolder));
            Assert.That(info.Arguments, Is.Empty);
            Assert.That(info.ErrorDialog, Is.False, "the launcher shows its own message");
        }

        [Test]
        public void TheGameStart_AsksForNoElevation()
        {
            ProcessStartInfo info = ShellProcessStarter.CreateProgramStartInfo(Program, EeFolder);

            Assert.That(info.Verb, Is.Empty, "no \"runas\": only a layer the player chose may elevate the game");
        }

        [TestCase(@"Empire Earth.exe", EeFolder)]
        [TestCase(Program, @"Empire Earth")]
        [TestCase(@"\Empire Earth\Empire Earth.exe", EeFolder)]
        public void RelativePaths_AreProgrammingErrors(string program, string folder)
        {
            Assert.That(() => ShellProcessStarter.CreateProgramStartInfo(program, folder), Throws.ArgumentException);
        }

        [Test]
        public void Contract_4_3_TheDownloadPageOpensThroughTheShellWithoutElevation()
        {
            ProcessStartInfo info = ShellProcessStarter.CreateUrlStartInfo("https://empireearth.eu/download");

            Assert.That(info.UseShellExecute, Is.True);
            Assert.That(info.FileName, Is.EqualTo("https://empireearth.eu/download"));
            Assert.That(info.Verb, Is.Empty);
            Assert.That(info.Arguments, Is.Empty);
            Assert.That(info.ErrorDialog, Is.False);
        }

        [TestCase("http://empireearth.eu/download", TestName = "OpenUrl_Http_IsRefused")]
        [TestCase("file:///C:/Windows/System32/cmd.exe", TestName = "OpenUrl_File_IsRefused")]
        [TestCase(@"C:\Windows\System32\cmd.exe", TestName = "OpenUrl_Path_IsRefused")]
        [TestCase("empireearth.eu/download", TestName = "OpenUrl_RelativeUrl_IsRefused")]
        [TestCase("", TestName = "OpenUrl_Empty_IsRefused")]
        public void OnlyHttpsUrls_AreOpened(string url)
        {
            Assert.That(() => ShellProcessStarter.CreateUrlStartInfo(url), Throws.ArgumentException);
        }
    }
}
