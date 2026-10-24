namespace Library.Api.Features.Loans;

public static class LoansExtensions
{
    public static IServiceCollection AddLoans(this IServiceCollection services)
    {
        services.AddScoped<CreateLoanHandler>();
        services.AddScoped<GetLoanHandler>();
        services.AddScoped<LoanCloser>();
        services.AddScoped<ReturnLoanHandler>();
        services.AddScoped<CancelLoanHandler>();
        return services;
    }
}
