using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class RefreshTokenEnvelopeTests
{
    private static readonly byte[] KeyA = RandomNumberGenerator.GetBytes(32);
    private static readonly byte[] KeyB = RandomNumberGenerator.GetBytes(32);

    private static RefreshTokenEncryptionService Create(byte[] key, params byte[][] previous) =>
        new(Options.Create(new PrismBiometricOptions
        {
            EncryptionKey = Convert.ToBase64String(key),
            PreviousEncryptionKeys = previous.Select(Convert.ToBase64String).ToArray()
        }));

    [Fact]
    public void ACiphertext_IsVersioned_AndNamesItsKey()
    {
        var encrypted = Create(KeyA).Encrypt("refresh-token", "ctx");

        encrypted.Should().StartWith("v2.");
        encrypted.Split('.').Should().HaveCount(3);
        encrypted.Split('.')[1].Should().HaveLength(8);
    }

    [Fact]
    public void RoundTrips_WithTheSameAssociatedData()
    {
        var service = Create(KeyA);

        service.Decrypt(service.Encrypt("refresh-token", "tenant|user|device"), "tenant|user|device")
            .Should().Be("refresh-token");
    }

    [Theory]
    [InlineData("tenant|user|OTHER-device")]
    [InlineData("OTHER-tenant|user|device")]
    [InlineData("")]
    [InlineData(null)]
    public void ACiphertextCopiedToAnotherRow_DoesNotOpen(string? otherContext)
    {
        var service = Create(KeyA);
        var encrypted = service.Encrypt("refresh-token", "tenant|user|device");

        var act = () => service.Decrypt(encrypted, otherContext);

        act.Should().Throw<CryptographicException>("the ciphertext is bound to the row it was written for");
    }

    [Fact]
    public void AnUnversionedCiphertext_FromBeforeTheEnvelope_StillDecrypts_WithAnyContext()
    {
        var legacy = LegacyEncrypt(KeyA, "old-refresh-token");

        Create(KeyA).Decrypt(legacy, "tenant|user|device").Should().Be("old-refresh-token");
    }

    [Fact]
    public void AfterRotation_TokensUnderThePreviousKeyStillOpen_AndNewOnesUseTheNewKey()
    {
        var before = Create(KeyA).Encrypt("refresh-token", "ctx");
        var rotated = Create(KeyB, KeyA);

        rotated.Decrypt(before, "ctx").Should().Be("refresh-token");

        var after = rotated.Encrypt("refresh-token", "ctx");
        after.Split('.')[1].Should().NotBe(before.Split('.')[1]);
        Create(KeyB).Decrypt(after, "ctx").Should().Be("refresh-token", "the new key alone is enough for new tokens");
    }

    [Fact]
    public void AfterRotation_AnUnversionedCiphertextUnderThePreviousKeyStillOpens()
    {
        var legacy = LegacyEncrypt(KeyA, "old-refresh-token");

        Create(KeyB, KeyA).Decrypt(legacy).Should().Be("old-refresh-token");
    }

    [Fact]
    public void ACiphertextForAKeyThatIsNoLongerConfigured_IsRefused()
    {
        var encrypted = Create(KeyA).Encrypt("refresh-token", "ctx");

        var act = () => Create(KeyB).Decrypt(encrypted, "ctx");

        act.Should().Throw<CryptographicException>().WithMessage("*not configured*");
    }

    [Fact]
    public void ATamperedCiphertext_IsRefused()
    {
        var service = Create(KeyA);
        var parts = service.Encrypt("refresh-token", "ctx").Split('.');
        var bytes = Convert.FromBase64String(parts[2]);
        bytes[^1] ^= 0xFF;
        var tampered = $"{parts[0]}.{parts[1]}.{Convert.ToBase64String(bytes)}";

        var act = () => service.Decrypt(tampered, "ctx");

        act.Should().Throw<CryptographicException>();
    }

    private static string LegacyEncrypt(byte[] key, string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        return Convert.ToBase64String(nonce.Concat(ciphertext).Concat(tag).ToArray());
    }
}
