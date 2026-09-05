using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using FluentAssertions;
using UmbracoPrism.MockBusinessApp.Services.SupportSystem;

namespace UmbracoPrism.Core.Tests.ServiceDesign.SupportSystem;

/// <summary>
/// Regression coverage for a real display bug found while regenerating walkthrough
/// screenshots: <see cref="ContributionsValidation.Validate"/>'s warning/error messages used
/// the bare "C" currency format specifier, which formats using <see cref="CultureInfo.CurrentCulture"/>.
/// Under invariant globalization (the default on a Linux container/CI runner with no ICU data),
/// that's the invariant culture — whose currency symbol is "¤", not "£". This demo is GBP-only
/// and must render as such regardless of the host's configured culture.
/// </summary>
public class ContributionsValidationCurrencyTests
{
    [Fact]
    public void OutOfBandContributionWarning_RendersPoundSign_NotTheInvariantCurrencySign()
    {
        const string csv = "memberRef,memberName,tier,fireEndorsement,under18,dob,monthlyContribution\n" +
                            "NJF-001,Dev Patel,Performer,N,N,,55.00\n";

        var result = ContributionsValidation.Validate(Encoding.UTF8.GetBytes(csv));
        var warningText = ReadWarningText(result);

        warningText.Should().Contain("£", "the demo is GBP-only regardless of the host's current culture");
        warningText.Should().NotContain("¤", "the invariant-culture currency sign must never leak into a user-facing message");
    }

    private static string ReadWarningText(byte[] csvBytes)
    {
        using var reader = new StreamReader(new MemoryStream(csvBytes));
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));
        csv.Read();
        csv.ReadHeader();
        csv.Read();
        return csv.GetField("warningText") ?? "";
    }
}
