using System.Text.Json;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.WorkflowRuntime.Cli;

internal static class WorkflowFileReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowOutOfOrderMetadataProperties = true
    };

    public static WorkflowDefinitionFile Read(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<WorkflowDefinitionFile>(json, Options)
            ?? throw new InvalidOperationException($"'{path}' did not deserialize to a WorkflowDefinitionFile.");
    }
}
