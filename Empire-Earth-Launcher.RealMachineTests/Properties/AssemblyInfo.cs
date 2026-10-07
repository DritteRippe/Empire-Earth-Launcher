using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Empire-Earth-Launcher.RealMachineTests")]
[assembly: AssemblyDescription("Checks of the launcher core against a real installation on a GitHub-hosted Windows runner")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("Empire-Earth-Launcher.RealMachineTests")]
[assembly: AssemblyCopyright("Copyright ©  2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("06c3ce48-099d-4fb4-9ce0-ca677fccd7d7")]

// The real-machine fixtures share one session and run in the order of their [Order] attributes: one test at a time.
[assembly: NonParallelizable]

// AssemblyVersion, AssemblyFileVersion and AssemblyInformationalVersion are
// shared by all projects and maintained in one place: SharedAssemblyInfo.cs
// at the repository root (linked as Properties\SharedAssemblyInfo.cs).
