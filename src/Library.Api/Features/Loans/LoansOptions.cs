using System.ComponentModel.DataAnnotations;

namespace Library.Api.Features.Loans;

public sealed class LoansOptions
{
    public const string SectionName = "Loans";

    [Range(1, 365)]
    public int DefaultPeriodDays { get; init; } = 14;
}
