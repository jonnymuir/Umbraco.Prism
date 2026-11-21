using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using UmbracoPrism.Core.Services;
using UmbracoPrism.Core.Sessions;

namespace UmbracoPrism.Core.Tests;

public class PrismTicketStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static PrismTicketStore StoreOver(IPrismSessionStorage storage, IDataProtectionProvider? protection = null) =>
        new(storage, protection ?? new EphemeralDataProtectionProvider(), Options.Create(new PrismSessionOptions()), new FixedClock(Now));

    private static AuthenticationTicket TicketWithTokens(DateTimeOffset? expires = null)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("tid", "tenant-1"), new Claim("oid", "member-1"), new Claim("name", "Alex Ju")], "PrismMemberCookie"));
        var properties = new AuthenticationProperties { ExpiresUtc = expires };
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = "access-secret" },
            new AuthenticationToken { Name = "refresh_token", Value = "refresh-secret" },
        ]);
        return new AuthenticationTicket(principal, properties, "PrismMemberCookie");
    }

    [Fact]
    public async Task A_stored_ticket_comes_back_with_its_claims_and_tokens()
    {
        var store = StoreOver(new InMemoryPrismSessionStorage());

        var key = await store.StoreAsync(TicketWithTokens());
        var ticket = await store.RetrieveAsync(key);

        ticket.Should().NotBeNull();
        ticket!.Principal.FindFirst("oid")!.Value.Should().Be("member-1");
        ticket.Properties.GetTokenValue("refresh_token").Should().Be("refresh-secret");
    }

    [Fact]
    public async Task The_key_is_random_and_the_stored_bytes_hold_no_token_in_the_clear()
    {
        var storage = new InMemoryPrismSessionStorage();
        var store = StoreOver(storage);

        var first = await store.StoreAsync(TicketWithTokens());
        var second = await store.StoreAsync(TicketWithTokens());

        first.Should().NotBe(second);
        first.Length.Should().BeGreaterThanOrEqualTo(43, "32 random bytes, base64url");
        var stored = System.Text.Encoding.UTF8.GetString(storage.Rows[first].Data);
        stored.Should().NotContain("refresh-secret").And.NotContain("access-secret").And.NotContain("member-1");
    }

    [Fact]
    public async Task Renewing_replaces_the_ticket_under_the_same_key()
    {
        var storage = new InMemoryPrismSessionStorage();
        var store = StoreOver(storage);
        var key = await store.StoreAsync(TicketWithTokens());

        var renewed = TicketWithTokens();
        renewed.Properties.UpdateTokenValue("refresh_token", "refresh-secret-2");
        await store.RenewAsync(key, renewed);

        storage.Rows.Should().HaveCount(1);
        (await store.RetrieveAsync(key))!.Properties.GetTokenValue("refresh_token").Should().Be("refresh-secret-2");
    }

    [Fact]
    public async Task Removing_a_session_ends_it()
    {
        var store = StoreOver(new InMemoryPrismSessionStorage());
        var key = await store.StoreAsync(TicketWithTokens());

        await store.RemoveAsync(key);

        (await store.RetrieveAsync(key)).Should().BeNull();
    }

    [Fact]
    public async Task An_unknown_key_is_no_session()
    {
        (await StoreOver(new InMemoryPrismSessionStorage()).RetrieveAsync("not-a-session")).Should().BeNull();
    }

    [Fact]
    public async Task A_session_protected_with_other_keys_is_no_session_rather_than_an_error()
    {
        var storage = new InMemoryPrismSessionStorage();
        var key = await StoreOver(storage, new EphemeralDataProtectionProvider()).StoreAsync(TicketWithTokens());

        (await StoreOver(storage, new EphemeralDataProtectionProvider()).RetrieveAsync(key)).Should().BeNull();
    }

    [Fact]
    public async Task The_session_expires_with_the_ticket_and_records_the_member()
    {
        var storage = new InMemoryPrismSessionStorage();
        var expires = Now.AddHours(3);

        var key = await StoreOver(storage).StoreAsync(TicketWithTokens(expires));

        storage.Rows[key].ExpiresAt.Should().Be(expires);
        storage.Rows[key].Subject.Should().Be("tenant-1:member-1");
    }

    [Fact]
    public async Task A_ticket_with_no_expiry_lasts_the_sliding_window()
    {
        var storage = new InMemoryPrismSessionStorage();

        var key = await StoreOver(storage).StoreAsync(TicketWithTokens());

        storage.Rows[key].ExpiresAt.Should().Be(Now.AddMinutes(new PrismSessionOptions().SlidingExpirationMinutes));
    }

    [Fact]
    public async Task The_distributed_cache_storage_round_trips_and_removes()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var storage = new PrismDistributedCacheSessionStorage(cache);

        await storage.SaveAsync("k", [1, 2, 3], DateTimeOffset.UtcNow.AddMinutes(5), null, CancellationToken.None);
        (await storage.LoadAsync("k", CancellationToken.None)).Should().Equal(1, 2, 3);

        await storage.RemoveAsync("k", CancellationToken.None);
        (await storage.LoadAsync("k", CancellationToken.None)).Should().BeNull();
        (await storage.RemoveExpiredAsync(CancellationToken.None)).Should().Be(0);
    }

    [Theory]
    [InlineData(RuntimeLevel.Run, 1)]
    [InlineData(RuntimeLevel.Install, 0)]
    [InlineData(RuntimeLevel.Upgrade, 0)]
    public async Task Cleanup_only_runs_against_a_running_site(RuntimeLevel level, int expectedCalls)
    {
        var storage = new Mock<IPrismSessionStorage>();
        var runtime = new Mock<IRuntimeState>();
        runtime.SetupGet(r => r.Level).Returns(level);
        var cleanup = new PrismSessionCleanup(storage.Object, runtime.Object, NullLogger<PrismSessionCleanup>.Instance);

        await cleanup.RunOnceAsync(CancellationToken.None);

        storage.Verify(s => s.RemoveExpiredAsync(It.IsAny<CancellationToken>()), Times.Exactly(expectedCalls));
    }

    [Fact]
    public async Task Cleanup_survives_a_failing_store()
    {
        var storage = new Mock<IPrismSessionStorage>();
        storage.Setup(s => s.RemoveExpiredAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db down"));
        var runtime = new Mock<IRuntimeState>();
        runtime.SetupGet(r => r.Level).Returns(RuntimeLevel.Run);
        var cleanup = new PrismSessionCleanup(storage.Object, runtime.Object, NullLogger<PrismSessionCleanup>.Instance);

        var act = () => cleanup.RunOnceAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
