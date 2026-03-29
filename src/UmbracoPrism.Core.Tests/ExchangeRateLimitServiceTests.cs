using FluentAssertions;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class ExchangeRateLimitServiceTests
{
    private static ExchangeRateLimitService BuildService(
        int maxFailed = 3,
        int windowMinutes = 10,
        int ipPerMinute = 20,
        TimeProvider? timeProvider = null)
    {
        var opts = Options.Create(new PrismBiometricOptions
        {
            MaxFailedAttempts = maxFailed,
            FailureWindowMinutes = windowMinutes,
            PerIpRequestsPerMinute = ipPerMinute,
        });
        return new ExchangeRateLimitService(opts, timeProvider ?? TimeProvider.System);
    }

    // ------------------------------------------------------------------ token rate limiting

    [Fact]
    public void CheckTokenLimit_NoFailures_ReturnsNotLimited()
    {
        var svc = BuildService();
        var (isLimited, _) = svc.CheckTokenLimit("hash-1");
        isLimited.Should().BeFalse();
    }

    [Fact]
    public void RecordTokenFailure_BelowThreshold_TokenNotLocked()
    {
        var svc = BuildService(maxFailed: 3);

        svc.RecordTokenFailure("hash-1");
        svc.RecordTokenFailure("hash-1");

        var (isLimited, _) = svc.CheckTokenLimit("hash-1");
        isLimited.Should().BeFalse();
    }

    [Fact]
    public void RecordTokenFailure_AtThreshold_LocksToken()
    {
        var svc = BuildService(maxFailed: 3);

        svc.RecordTokenFailure("hash-1");
        svc.RecordTokenFailure("hash-1");
        svc.RecordTokenFailure("hash-1");

        var (isLimited, retryAfter) = svc.CheckTokenLimit("hash-1");
        isLimited.Should().BeTrue();
        retryAfter.Should().Be(600); // 10 minutes
    }

    [Fact]
    public void LockedToken_StaysLocked_OnSubsequentChecks()
    {
        var svc = BuildService(maxFailed: 3);

        for (int i = 0; i < 3; i++)
            svc.RecordTokenFailure("hash-1");

        // Multiple checks — should stay locked
        for (int i = 0; i < 5; i++)
        {
            var (isLimited, _) = svc.CheckTokenLimit("hash-1");
            isLimited.Should().BeTrue();
        }
    }

    [Fact]
    public void ResetTokenFailures_ClearsCounter()
    {
        var svc = BuildService(maxFailed: 3);

        svc.RecordTokenFailure("hash-1");
        svc.RecordTokenFailure("hash-1");

        svc.ResetTokenFailures("hash-1");

        var (isLimited, _) = svc.CheckTokenLimit("hash-1");
        isLimited.Should().BeFalse();
    }

    [Fact]
    public void DifferentTokenHashes_TrackedIndependently()
    {
        var svc = BuildService(maxFailed: 3);

        for (int i = 0; i < 3; i++)
            svc.RecordTokenFailure("hash-A");

        var (limitedA, _) = svc.CheckTokenLimit("hash-A");
        limitedA.Should().BeTrue();

        var (limitedB, _) = svc.CheckTokenLimit("hash-B");
        limitedB.Should().BeFalse();
    }

    [Fact]
    public void FailuresOutsideWindow_AreNotCounted()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var svc = BuildService(maxFailed: 3, windowMinutes: 10, timeProvider: fakeTime);

        svc.RecordTokenFailure("hash-1");
        svc.RecordTokenFailure("hash-1");

        // Advance past the 10-minute window
        fakeTime.Advance(TimeSpan.FromMinutes(11));

        // Old failures pruned; this is only the 1st failure in the new window
        svc.RecordTokenFailure("hash-1");

        var (isLimited, _) = svc.CheckTokenLimit("hash-1");
        isLimited.Should().BeFalse();
    }

    // ------------------------------------------------------------------ IP rate limiting

    [Fact]
    public void CheckIpLimit_UnderLimit_ReturnsNotLimited()
    {
        var svc = BuildService(ipPerMinute: 5);

        var (isLimited, _) = svc.CheckIpLimit("192.168.1.1");
        isLimited.Should().BeFalse();
    }

    [Fact]
    public void CheckIpLimit_AtLimit_ReturnsLimited()
    {
        var svc = BuildService(ipPerMinute: 3);

        svc.CheckIpLimit("192.168.1.1");
        svc.CheckIpLimit("192.168.1.1");
        svc.CheckIpLimit("192.168.1.1");

        var (isLimited, retryAfter) = svc.CheckIpLimit("192.168.1.1");
        isLimited.Should().BeTrue();
        retryAfter.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CheckIpLimit_DifferentIps_TrackedIndependently()
    {
        var svc = BuildService(ipPerMinute: 2);

        svc.CheckIpLimit("10.0.0.1");
        svc.CheckIpLimit("10.0.0.1");

        var (limitedA, _) = svc.CheckIpLimit("10.0.0.1");
        limitedA.Should().BeTrue();

        var (limitedB, _) = svc.CheckIpLimit("10.0.0.2");
        limitedB.Should().BeFalse();
    }

    [Fact]
    public void CheckIpLimit_RequestsExpireAfterOneMinute()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var svc = BuildService(ipPerMinute: 2, timeProvider: fakeTime);

        svc.CheckIpLimit("10.0.0.1");
        svc.CheckIpLimit("10.0.0.1");

        var (isLimited, _) = svc.CheckIpLimit("10.0.0.1");
        isLimited.Should().BeTrue();

        // Advance past the 1-minute window
        fakeTime.Advance(TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(1)));

        var (isLimitedAfter, _) = svc.CheckIpLimit("10.0.0.1");
        isLimitedAfter.Should().BeFalse();
    }

    [Fact]
    public void CheckIpLimit_RetryAfter_IsPositive()
    {
        var svc = BuildService(ipPerMinute: 1);

        svc.CheckIpLimit("10.0.0.1");

        var (_, retryAfter) = svc.CheckIpLimit("10.0.0.1");
        retryAfter.Should().BeGreaterThanOrEqualTo(1);
        retryAfter.Should().BeLessThanOrEqualTo(60);
    }

    // ------------------------------------------------------------------ fake time provider

    private class FakeTimeProvider(DateTimeOffset startTime) : TimeProvider
    {
        private DateTimeOffset _utcNow = startTime;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
