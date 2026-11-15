using System.Text;
using AwesomeAssertions;
using UmbracoPrism.MockBusinessApp.Services.SupportSystem;

namespace UmbracoPrism.Core.Tests.ServiceDesign.SupportSystem;

/// <summary>
/// The CSV dialect <see cref="ContributionsValidation.Validate"/> reads and writes: RFC 4180,
/// comma separated, header matched exactly, values echoed as written, Windows line endings. The
/// expected outputs are exact bytes, so a change of CSV library can't quietly change the file the
/// contributions team gets back.
/// </summary>
public class ContributionsValidationCsvFormatTests
{
    private const string InputHeader = "memberRef,memberName,tier,fireEndorsement,under18,dob,monthlyContribution";
    private const string OutputHeader = InputHeader + ",businessAppMemberId,errorText,warningText\r\n";

    private static string Validate(string csv) => Validate(Encoding.UTF8.GetBytes(csv));

    private static string Validate(byte[] csv) => Encoding.UTF8.GetString(ContributionsValidation.Validate(csv));

    [Fact]
    public void AnExcelExport_WithAByteOrderMarkAndWindowsLineEndings_IsReadNormally()
    {
        var input = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(InputHeader + "\r\nNJF-001,Ana,Performer,N,N,,30.00\r\nNJF-002,Ben,Recreational,N,N,,15.00\r\n"))
            .ToArray();

        Validate(input).Should().Be(
            OutputHeader +
            "NJF-001,Ana,Performer,N,N,,30.00,MBA-B6DB5278,,\r\n" +
            "NJF-002,Ben,Recreational,N,N,,15.00,MBA-59C73EA9,,\r\n");
    }

    [Theory]
    [InlineData("")]
    [InlineData(InputHeader + "\n")]
    public void AnEmptyFileOrAHeaderAlone_GivesBackJustTheHeader(string input)
    {
        Validate(input).Should().Be(OutputHeader);
    }

    [Fact]
    public void AwkwardValues_AreEchoedBackExactly_QuotedWhereTheyNeedIt()
    {
        var input = InputHeader + "\n" +
            "NJF-001,\"She said \"\"hi\"\"\",Performer,N,N,,30.00\n" +
            "NJF-002,\"two\nlines\",Performer,N,N,,30.00\n" +
            "NJF-003,  padded  ,Performer,N,N,,30.00\n" +
            "NJF-004,Al\"ice,Performer,N,N,,30.00\n" +
            "NJF-005,=SUM(A1),Performer,N,N,,30.00\n" +
            "NJF-006,café ✓,Performer,N,N,,30.00\n" +
            "NJF-007,\"Smith, Jo\",Performer,N,N,,30.00\n";

        Validate(input).Should().Be(
            OutputHeader +
            "NJF-001,\"She said \"\"hi\"\"\",Performer,N,N,,30.00,MBA-B6DB5278,,\r\n" +
            "NJF-002,\"two\nlines\",Performer,N,N,,30.00,MBA-59C73EA9,,\r\n" +
            "NJF-003,\"  padded  \",Performer,N,N,,30.00,MBA-4DD319E1,,\r\n" +
            "NJF-004,\"Al\"\"ice\",Performer,N,N,,30.00,MBA-19DDFEF2,,\r\n" +
            "NJF-005,=SUM(A1),Performer,N,N,,30.00,MBA-863C4D1F,,\r\n" +
            "NJF-006,café ✓,Performer,N,N,,30.00,MBA-447A25BD,,\r\n" +
            "NJF-007,\"Smith, Jo\",Performer,N,N,,30.00,MBA-6AC3361E,,\r\n");
    }

    [Fact]
    public void ColumnsAreFoundByName_InAnyOrder_AndExtraColumnsAreIgnored()
    {
        var input = "monthlyContribution,extra,memberRef,memberName,tier,fireEndorsement,under18,dob\n" +
                    "30.00,zzz,NJF-001,Ana,Performer,N,N,\n";

        Validate(input).Should().Be(OutputHeader + "NJF-001,Ana,Performer,N,N,,30.00,MBA-B6DB5278,,\r\n");
    }

    [Fact]
    public void ARepeatedHeaderName_ReadsTheFirstColumn()
    {
        var input = InputHeader + ",memberName\nNJF-001,first,Performer,N,N,,30.00,second\n";

        Validate(input).Should().Be(OutputHeader + "NJF-001,first,Performer,N,N,,30.00,MBA-B6DB5278,,\r\n");
    }

    [Theory]
    [InlineData("memberRef;memberName;tier;fireEndorsement;under18;dob;monthlyContribution\nNJF-001;Ana;Performer;N;N;;30,00\n")]
    [InlineData("MemberRef,MemberName,Tier,FireEndorsement,Under18,Dob,MonthlyContribution\nNJF-001,Ana,Performer,N,N,,30.00\n")]
    public void OnlyACommaSeparatedFileWithTheExactHeaderNames_IsRead(string input)
    {
        var lines = Validate(input).Split("\r\n");

        lines[1].Should().StartWith(",,,,,,,,", "no column matched, so every echoed value is blank");
        lines[1].Should().Contain("Missing member reference.");
    }

    [Fact]
    public void AShortRow_ReadsItsMissingFieldsAsBlank()
    {
        var lines = Validate(InputHeader + "\nNJF-001,Ana\n").Split("\r\n");

        lines[1].Should().StartWith("NJF-001,Ana,,,,,,MBA-B6DB5278,");
    }
}
