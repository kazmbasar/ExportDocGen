using System.Runtime.CompilerServices;
using QuestPDF.Infrastructure;

namespace ExportDocGen.Tests;

internal static class TestSetup
{
    [ModuleInitializer]
    public static void Init()
    {
        // The app sets this in Program.cs; tests render documents without it.
        QuestPDF.Settings.License = LicenseType.Community;
    }
}
