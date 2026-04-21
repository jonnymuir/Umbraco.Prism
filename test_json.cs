using System.Text.Json;
using UmbracoPrism.Shared.Models.Workflow;

var json = """
{
  "definitionKey": "test",
  "displayName": "Test",
  "version": 1,
  "initialState": "waiting",
  "instancePolicy": "single",
  "states": [],
  "transitions": []
}
""";

var result = JsonSerializer.Deserialize<WorkflowDefinitionFile>(json);
Console.WriteLine($"States count: {result?.States.Count}");
