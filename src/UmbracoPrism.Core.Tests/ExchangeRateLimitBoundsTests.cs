using AwesomeAssertions;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class ExchangeRateLimitBoundsTests
{
    private static (ExchangeRateLimitService Service, MutableClock Clock) Build(int maxFailed = 3, int windowMinutes = 10)
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));
        var service = new ExchangeRateLimitService(
            Options.Create(new PrismBiometricOptions { MaxFailedAttempts = maxFailed, FailureWindowMinutes = windowMinutes, PerIpRequestsPerMinute = 20 }),
            clock);
        return (service, clock);
    }

    [Fact]
    public void ALockedToken_IsReleased_WhenTheFailureWindowHasPassed()
    {
        var (service, clock) = Build();
        for (var i = 0; i < 3; i++) service.RecordTokenFailure("hash");
        service.CheckTokenLimit("hash").IsLimited.Should().BeTrue();

        clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        service.CheckTokenLimit("hash").IsLimited.Should().BeFalse("the lock lasts the window the Retry-After header reports, not until restart");
    }

    [Fact]
    public void RetryAfter_CountsDownAsTheLockRuns()
    {
        var (service, clock) = Build();
        for (var i = 0; i < 3; i++) service.RecordTokenFailure("hash");

        clock.Advance(TimeSpan.FromMinutes(4));

        service.CheckTokenLimit("hash").RetryAfterSeconds.Should().Be(6 * 60);
    }

    [Fact]
    public void CheckingAnUnknownToken_DoesNotCreateAnEntry()
    {
        var (service, _) = Build();

        for (var i = 0; i < 1000; i++) service.CheckTokenLimit($"garbage-{i}");

        service.TrackedTokenCount.Should().Be(0);
    }

    [Fact]
    public void IdleEntries_AreSwept()
    {
        var (service, clock) = Build();
        service.RecordTokenFailure("hash");
        service.CheckIpLimit("203.0.113.9");
        service.TrackedTokenCount.Should().Be(1);
        service.TrackedIpCount.Should().Be(1);

        clock.Advance(TimeSpan.FromMinutes(11));
        service.CheckTokenLimit("other");

        service.TrackedTokenCount.Should().Be(0, "a failure outside the window leaves nothing worth keeping");
        service.TrackedIpCount.Should().Be(0);
    }

    [Fact]
    public void ALockedToken_IsNotSweptWhileItsLockRuns()
    {
        var (service, clock) = Build(windowMinutes: 30);
        for (var i = 0; i < 3; i++) service.RecordTokenFailure("hash");

        clock.Advance(TimeSpan.FromMinutes(5));
        service.CheckTokenLimit("other");

        service.CheckTokenLimit("hash").IsLimited.Should().BeTrue();
    }

    [Fact]
    public void TheNumberOfTrackedKeys_IsCapped()
    {
        var (service, _) = Build();

        for (var i = 0; i < ExchangeRateLimitService.MaxTrackedKeys + 500; i++)
        {
            service.RecordTokenFailure($"garbage-{i}");
        }

        service.TrackedTokenCount.Should().BeLessThanOrEqualTo(ExchangeRateLimitService.MaxTrackedKeys);
    }

    private sealed class MutableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
