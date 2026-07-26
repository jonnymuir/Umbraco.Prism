namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

internal static class ServiceBlueprintAuthoringFixtureLocator
{
    public static string GetFixturesPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "ServiceDesign", "Authoring", "Fixtures");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the workflow authoring fixtures directory from '{AppContext.BaseDirectory}'.");
    }
}
