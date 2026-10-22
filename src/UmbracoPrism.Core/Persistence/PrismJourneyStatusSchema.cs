using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// The status last published for one of a member's journeys, kept while its iOS Live Activity
/// runs: the activity's own token arrives from the device after the activity starts, and the
/// latest status is sent with it.
/// </summary>
[TableName("prismJourneyStatuses")]
[PrimaryKey("Id", AutoIncrement = true)]
[ExplicitColumns]
public class PrismJourneyStatusSchema
{
    [Column("Id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("UserId")]
    [Length(450)]
    public string UserId { get; set; } = string.Empty;

    [Column("TenantId")]
    [Length(450)]
    public string TenantId { get; set; } = string.Empty;

    [Column("JourneyKey")]
    [Length(255)]
    public string JourneyKey { get; set; } = string.Empty;

    [Column("ServiceName")]
    [Length(255)]
    public string ServiceName { get; set; } = string.Empty;

    [Column("Status")]
    [Length(255)]
    public string Status { get; set; } = string.Empty;

    [Column("Step")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public int? Step { get; set; }

    [Column("Steps")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public int? Steps { get; set; }

    [Column("Path")]
    [Length(1000)]
    public string Path { get; set; } = string.Empty;

    /// <summary>Gets or sets the status kind, <c>Waiting</c> or <c>Done</c>.</summary>
    [Column("Kind")]
    [Length(16)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the running Live Activity's own push token, once the device has sent it.</summary>
    [Column("ActivityToken")]
    [Length(500)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? ActivityToken { get; set; }

    [Column("UpdatedAt")]
    public DateTime UpdatedAt { get; set; }
}
