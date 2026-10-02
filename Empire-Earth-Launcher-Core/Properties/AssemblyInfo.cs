using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Empire_Earth_Launcher_Core")]
[assembly: AssemblyDescription("UI-free core of the Empire Earth Launcher")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("Empire_Earth_Launcher_Core")]
[assembly: AssemblyCopyright("Copyright ©  2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("9dbbdaad-668e-423a-8185-46b7772ca18b")]

// The tests reach internal helpers (e.g. the log trimming). Must match the AssemblyName of the test project.
[assembly: InternalsVisibleTo("Empire-Earth-Launcher.Tests")]

// AssemblyVersion, AssemblyFileVersion and AssemblyInformationalVersion are
// shared by all projects and maintained in one place: SharedAssemblyInfo.cs
// at the repository root (linked as Properties\SharedAssemblyInfo.cs).
