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

        /// <summary>
        /// "Open backup folder" (ADR 0007): the folder through the shell, without a verb, ending with a separator, so that the
        /// shell takes it as a folder and never as a program of that name.
        /// </summary>
        [TestCase(@"C:\Users\Player\AppData\Local\Empire Earth Launcher\Backups", @"C:\Users\Player\AppData\Local\Empire Earth Launcher\Backups\")]
        [TestCase(@"C:\Users\Player\AppData\Local\Empire Earth Launcher\Backups\", @"C:\Users\Player\AppData\Local\Empire Earth Launcher\Backups\")]
        [TestCase(@"D:\Backups\Empire Earth.exe", @"D:\Backups\Empire Earth.exe\")]
        [TestCase(@"\\server\share\Backups", @"\\server\share\Backups\")]
        public void Adr0007_TheBackupFolderOpensInTheExplorerThroughTheShell(string folder, string expected)
        {
            ProcessStartInfo info = ShellProcessStarter.CreateFolderStartInfo(folder);

            Assert.That(info.UseShellExecute, Is.True);
            Assert.That(info.FileName, Is.EqualTo(expected));
            Assert.That(info.Verb, Is.Empty);
            Assert.That(info.Arguments, Is.Empty);
            Assert.That(info.ErrorDialog, Is.False);
        }

        [TestCase("Backups", TestName = "OpenFolder_RelativePath_IsRefused")]
        [TestCase("https://empireearth.eu/download", TestName = "OpenFolder_Url_IsRefused")]
        [TestCase("", TestName = "OpenFolder_Empty_IsRefused")]
        public void OnlyFullFolderPaths_AreOpened(string folder)
        {
            Assert.That(() => ShellProcessStarter.CreateFolderStartInfo(folder), Throws.ArgumentException);
        }

        /// <summary>
        /// "Open dreXmod.config" of the Mods page (launcher 1.1.0): the document through the shell, without a verb and without
        /// arguments, so that the program Windows has registered for it shows it; the launcher keeps no handle.
        /// </summary>
        [TestCase(@"C:\Program Files (x86)\Empire Earth\Empire Earth\dreXmod.config")]
        [TestCase(@"D:\Games\EE\dreXmod.config")]
        [TestCase(@"\\server\share\EE\dreXmod.config")]
        public void TheModsPage_OpensAConfigFileThroughTheShell(string file)
        {
            ProcessStartInfo info = ShellProcessStarter.CreateFileStartInfo(file);

            Assert.That(info.UseShellExecute, Is.True);
            Assert.That(info.FileName, Is.EqualTo(file));
            Assert.That(info.Verb, Is.Empty);
            Assert.That(info.Arguments, Is.Empty);
            Assert.That(info.ErrorDialog, Is.False);
        }

        [TestCase("dreXmod.config", TestName = "OpenFile_RelativePath_IsRefused")]
        [TestCase("https://empireearth.eu/dreXmod.config", TestName = "OpenFile_Url_IsRefused")]
        [TestCase("", TestName = "OpenFile_Empty_IsRefused")]
        [TestCase(@"C:\Games\EE\Empire Earth.exe", TestName = "OpenFile_Program_IsRefused")]
        [TestCase(@"C:\Games\EE\EMPIRE EARTH.EXE", TestName = "OpenFile_ProgramInCapitals_IsRefused")]
        [TestCase(@"C:\Games\EE\setup.bat", TestName = "OpenFile_BatchFile_IsRefused")]
        [TestCase(@"C:\Games\EE\run.ps1", TestName = "OpenFile_Script_IsRefused")]
        [TestCase(@"C:\Games\EE\game.lnk", TestName = "OpenFile_Shortcut_IsRefused")]
        [TestCase(@"C:\Games\EE\import.reg", TestName = "OpenFile_RegistryFile_IsRefused")]
        public void OnlyFullPathsOfDocuments_AreOpened(string file)
        {
            Assert.That(() => ShellProcessStarter.CreateFileStartInfo(file), Throws.ArgumentException);
        }
    }
}
