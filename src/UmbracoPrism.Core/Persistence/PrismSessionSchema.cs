using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// A member's signed-in session, held on the server so the browser's cookie carries only a
/// reference. <c>Data</c> is the authentication ticket, protected with Data Protection.
/// </summary>
[TableName("prismSessions")]
[PrimaryKey("SessionKey", AutoIncrement = false)]
[ExplicitColumns]
public class PrismSessionSchema
{
    [Column("SessionKey")]
    [PrimaryKeyColumn(AutoIncrement = false)]
    [Length(64)]
    public string SessionKey { get; set; } = string.Empty;

    [Column("Subject")]
    [Length(450)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Subject { get; set; }

    [Column("Data")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    public string Data { get; set; } = string.Empty;

    [Column("ExpiresAt")]
    public DateTime ExpiresAt { get; set; }
}
