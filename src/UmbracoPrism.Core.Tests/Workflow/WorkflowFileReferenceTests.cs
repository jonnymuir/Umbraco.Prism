using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.Core.Tests.Workflow;

/// <summary>
/// <see cref="WorkflowFileReference.FromFieldValue"/> has to handle a value in either of two
/// real shapes: still its original CLR type within the same request, or a boxed
/// <see cref="JsonElement"/> after a round-trip through <c>WorkflowInstanceState.FieldValues</c>'
/// JSON persistence (no custom converter there) — this is the exact same round-trip
/// <see cref="UmbracoPrism.Core.Services.Workflow.UmbracoCmsWorkflowInstanceStore"/> performs.
/// </summary>
public class WorkflowFileReferenceTests
{
    private static readonly JsonSerializerOptions PersistenceOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void FromFieldValue_WithTheOriginalClrType_ReturnsItUnchanged()
    {
        var original = new WorkflowFileReference
        {
            StorageKey = "instance-1/abc.pdf",
            OriginalFileName = "Current Licence.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1234
        };

        var resolved = WorkflowFileReference.FromFieldValue(original);

        resolved.Should().Be(original);
    }

    [Fact]
    public void FromFieldValue_AfterAJsonPersistenceRoundTrip_ParsesTheJsonElementBack()
    {
        var original = new WorkflowFileReference
        {
            StorageKey = "instance-1/abc.pdf",
            OriginalFileName = "Current Licence.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1234
        };

        // Mirrors how FieldValues (Dictionary<string, object?>) actually round-trips: the whole
        // instance is JSON-serialized then deserialized generically, so a stored reference comes
        // back as a boxed JsonElement, not its original record type.
        var fieldValues = new Dictionary<string, object?> { ["current-licence"] = original };
        var json = JsonSerializer.Serialize(fieldValues, PersistenceOptions);
        var reloaded = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, PersistenceOptions)!;

        reloaded["current-licence"].Should().BeOfType<JsonElement>();

        var resolved = WorkflowFileReference.FromFieldValue(reloaded["current-licence"]);

        resolved.Should().BeEquivalentTo(original);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("just a plain string")]
    [InlineData(42)]
    public void FromFieldValue_WithAnythingElse_ReturnsNull(object? raw)
    {
        WorkflowFileReference.FromFieldValue(raw).Should().BeNull();
    }
}
