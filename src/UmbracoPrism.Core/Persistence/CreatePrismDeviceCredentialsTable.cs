using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Migration that creates the prismDeviceCredentials table for the biometric device registry.
/// </summary>
public class CreatePrismDeviceCredentialsTable(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!TableExists("prismDeviceCredentials"))
        {
            Create.Table<PrismDeviceCredentialSchema>().Do();

            // Unique composite index: one DeviceId per tenant
            Database.Execute(@"
                CREATE UNIQUE INDEX IX_prismDeviceCredentials_TenantId_DeviceId
                ON prismDeviceCredentials (TenantId, DeviceId);");

            // Lookup index: all credentials for a user within a tenant
            Database.Execute(@"
                CREATE INDEX IX_prismDeviceCredentials_TenantId_UserId
                ON prismDeviceCredentials (TenantId, UserId);");

            // Exchange validation: token hash lookup
            Database.Execute(@"
                CREATE INDEX IX_prismDeviceCredentials_TokenHash
                ON prismDeviceCredentials (TokenHash);");
        }

        return Task.CompletedTask;
    }
}
