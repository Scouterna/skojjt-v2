using System.Diagnostics;
using System.Reflection;

namespace Skojjt.Shared;

public static class AppVersionHelper
{
    public static string GetVersion(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetEntryAssembly();
        if (assembly is null)
            return "0.0.0";

        var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
        int major = fileVersionInfo.FileMajorPart;
        int minor = fileVersionInfo.FileMinorPart;
        int build = fileVersionInfo.FileBuildPart;
        return $"{major}.{minor}.{build}";
    }
}
