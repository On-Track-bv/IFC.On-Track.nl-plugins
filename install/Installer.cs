// Purpose: WixSharp installer entry point for IFC.On-Track.nl Revit plugin
using Installer;
using WixSharp;
using WixSharp.CommonTasks;
using WixSharp.Controls;

const string outputName = "IfcOnTrack.Revit";
const string projectName = "IFC.On-Track.nl";

var version = args.Length > 0 ? args[0] : "1.0.0";
var binDirectories = args.Length > 1 ? args[1..] : [];

var project = new Project
{
    OutDir = "output",
    Name = projectName,
    Platform = Platform.x64,
    UI = WUI.WixUI_FeatureTree,
    MajorUpgrade = MajorUpgrade.Default,
    GUID = new Guid("8A9C2F5D-4B3E-4C6A-9F1D-2E7B8C4A5D6F"),
    Version = new Version(version),
    ControlPanelInfo =
    {
        Manufacturer = "On Track B.V.",
        HelpLink = "https://github.com/On-Track-bv/IFC.On-Track.nl-plugins/issues",
        Comments = "bSDD classification and IFC export plugin for Autodesk Revit"
    }
};

var wixEntities = Generator.GenerateWixEntities(binDirectories);
project.RemoveDialogsBetween(NativeDialogs.WelcomeDlg, NativeDialogs.CustomizeDlg);

BuildSingleUserMsi();
BuildMultiUserMsi();

void BuildSingleUserMsi()
{
    project.Scope = InstallScope.perUser;
    project.OutFileName = $"{outputName}-v{version}-SingleUser";
    project.Dirs =
    [
        new InstallDir(@"%AppDataFolder%\Autodesk\Revit\Addins\", wixEntities)
    ];
    project.BuildMsi();
}

void BuildMultiUserMsi()
{
    project.Scope = InstallScope.perMachine;
    project.OutFileName = $"{outputName}-v{version}-MultiUser";
    project.Dirs =
    [
        new InstallDir(@"%CommonAppDataFolder%\Autodesk\Revit\Addins\", wixEntities)
    ];
    project.BuildMsi();
}
