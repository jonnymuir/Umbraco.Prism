using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Migration that adds the RefreshTokenEnc column to prismDeviceCredentials
/// for storing AES-256-GCM encrypted Entra refresh tokens.
/// </summary>
public class AddRefreshTokenEncColumn(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (ColumnExists("prismDeviceCredentials", "RefreshTokenEnc"))
            return Task.CompletedTask;

        Alter.Table("prismDeviceCredentials")
            .AddColumn("RefreshTokenEnc")
            .AsString()
            .WithDefaultValue(string.Empty)
            .Do();

        return Task.CompletedTask;
    }
}
