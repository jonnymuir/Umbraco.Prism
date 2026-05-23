using FluentAssertions;
using UmbracoPrism.TestSite.Controllers.Models;

namespace UmbracoPrism.Core.Tests;

public class PrismVinylNotificationSecurityTests
{
    [Fact]
    public void PrismVinylBackInStockRequest_DoesNotExposeTenantId()
    {
        typeof(PrismVinylBackInStockRequest)
            .GetProperty("TenantId")
            .Should()
            .BeNull();
    }
}
