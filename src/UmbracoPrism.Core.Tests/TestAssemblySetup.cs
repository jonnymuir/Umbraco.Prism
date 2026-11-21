using System.Runtime.CompilerServices;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

internal static class TestAssemblySetup
{
    // The demo tenant behaviours are off outside Development. Most tests exercise them, so the
    // test process opts in; PrismDemoTenantsTests covers the gate through its pure function.
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void AllowDemoTenants() =>
        Environment.SetEnvironmentVariable(PrismDemoTenants.OptInVariable, "true");
#pragma warning restore CA2255
}
