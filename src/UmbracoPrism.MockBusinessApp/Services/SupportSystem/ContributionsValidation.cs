using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace UmbracoPrism.MockBusinessApp.Services.SupportSystem;

/// <summary>
/// Mock Business App's own synthetic validation rules for the NJF Contributions Team's monthly
/// contributions file — believable, deterministic rules for a demo, not real business logic.
/// Mirrors the core Wayfinder repo's own <c>SafetyNetUnderwriting/ContributionsValidation.cs</c>
/// (same fictional National Juggling Federation scenario, same rule shapes) — kept as this app's
/// own copy rather than a shared reference, since MockBusinessApp has no dependency on Wayfinder
/// at all any more (see this app's own README). Deliberately stateless (no submission history): a
/// matched member id is derived from the member reference itself rather than looked up, so this
/// app doesn't need its own persistent member database for the demo to feel real.
/// </summary>
public static class ContributionsValidation
{
    // "C" alone formats using CultureInfo.CurrentCulture — under invariant globalization
    // (the default on a Linux container/CI runner with no ICU data), that's the invariant
    // culture, whose currency symbol is "¤", not "£". This demo is GBP-only, so the culture
    // is pinned explicitly rather than left to whatever happens to be current.
    private static readonly CultureInfo Gbp = CultureInfo.GetCultureInfo("en-GB");


    public static readonly string[] Columns =
        ["memberRef", "memberName", "tier", "fireEndorsement", "under18", "dob", "monthlyContribution",
         "businessAppMemberId", "errorText", "warningText"];

    private static readonly Dictionary<string, (decimal Standard, decimal FireFloor)> TierRates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Recreational"] = (15m, 20m),
        ["Performer"] = (30m, 35m),
        ["Instructor"] = (45m, 50m),
    };

    /// <summary>
    /// Parses <paramref name="csvBytes"/> against the NJF contributions schema and returns the
    /// same columns plus this app's own three appended ones, one row per input row — exactly the
    /// shape <c>bulk-dataset-ingest</c>'s column schema in bulk-contributions.json expects back.
    /// </summary>
    public static byte[] Validate(byte[] csvBytes)
    {
        using var reader = new StreamReader(new MemoryStream(csvBytes));
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null, BadDataFound = null };
        using var csv = new CsvReader(reader, config);

        var rows = new List<Dictionary<string, string>>();
        if (csv.Read())
        {
            csv.ReadHeader();
            while (csv.Read())
            {
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var column in new[] { "memberRef", "memberName", "tier", "fireEndorsement", "under18", "dob", "monthlyContribution" })
                {
                    csv.TryGetField<string>(column, out var value);
                    row[column] = value ?? "";
                }

                rows.Add(row);
            }
        }

        var seenRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var (errors, warnings) = ValidateRow(row, seenRefs);
            row["businessAppMemberId"] = string.IsNullOrWhiteSpace(row["memberRef"]) ? "" : DeriveMemberId(row["memberRef"]);
            row["errorText"] = string.Join(" ", errors);
            row["warningText"] = string.Join(" ", warnings);
        }

        return WriteCsv(rows);
    }

    private static (List<string> Errors, List<string> Warnings) ValidateRow(
        Dictionary<string, string> row, HashSet<string> seenRefs)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var memberRef = row["memberRef"];
        if (string.IsNullOrWhiteSpace(memberRef))
        {
            errors.Add("Missing member reference.");
        }
        else if (!seenRefs.Add(memberRef))
        {
            errors.Add($"Duplicate member reference '{memberRef}' in this file.");
        }

        var tier = row["tier"];
        var hasKnownTier = TierRates.TryGetValue(tier, out var rates);
        if (!hasKnownTier)
        {
            errors.Add($"Unrecognised tier '{tier}' — expected Recreational, Performer, or Instructor.");
        }

        var hasContribution = decimal.TryParse(row["monthlyContribution"], NumberStyles.Number, CultureInfo.InvariantCulture, out var contribution);
        if (!hasContribution || contribution <= 0)
        {
            errors.Add("Monthly contribution must be a positive amount.");
        }

        var fireEndorsement = string.Equals(row["fireEndorsement"], "Y", StringComparison.OrdinalIgnoreCase);
        if (fireEndorsement && hasKnownTier && hasContribution && contribution < rates.FireFloor)
        {
            errors.Add($"Fire endorsement requires a minimum contribution of {rates.FireFloor.ToString("C", Gbp)} for {tier}.");
        }

        var under18 = string.Equals(row["under18"], "Y", StringComparison.OrdinalIgnoreCase);
        if (under18)
        {
            var hasDob = DateOnly.TryParse(row["dob"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var dob);
            if (!hasDob)
            {
                errors.Add("Date of birth is required and must be a valid date when under18 is Y.");
            }
            else
            {
                var age = DateOnly.FromDateTime(DateTime.UtcNow).Year - dob.Year;
                if (dob > DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-age))
                {
                    age--;
                }

                if (age >= 18)
                {
                    errors.Add($"Date of birth implies an age of {age}, which is not under 18.");
                }
                else if (age == 17 && DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1) >= dob.AddYears(18))
                {
                    warnings.Add("This member turns 18 within the next 12 months — their under-18 status will need updating soon.");
                }
            }
        }

        if (hasKnownTier && hasContribution && errors.Count == 0)
        {
            var lowerBand = rates.Standard * 0.5m;
            var upperBand = rates.Standard * 1.5m;
            if (contribution < lowerBand || contribution > upperBand)
            {
                warnings.Add($"Contribution {contribution.ToString("C", Gbp)} is outside the expected {lowerBand.ToString("C", Gbp)}–{upperBand.ToString("C", Gbp)} band for {tier} — check this isn't a data entry error.");
            }
        }

        return (errors, warnings);
    }

    /// <summary>Deterministic, stateless "matching" — a real business app would look this up against its own member database; this demo derives it instead so no database is needed.</summary>
    private static string DeriveMemberId(string memberRef)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(memberRef.ToUpperInvariant()));
        return "MBA-" + Convert.ToHexString(hash)[..8];
    }

    private static byte[] WriteCsv(List<Dictionary<string, string>> rows)
    {
        using var buffer = new MemoryStream();
        using (var writer = new StreamWriter(buffer, leaveOpen: true))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            foreach (var column in Columns)
            {
                csv.WriteField(column);
            }

            csv.NextRecord();

            foreach (var row in rows)
            {
                foreach (var column in Columns)
                {
                    csv.WriteField(row.GetValueOrDefault(column, ""));
                }

                csv.NextRecord();
            }
        }

        return buffer.ToArray();
    }
}
