namespace Library.Api.Features.Loans;

public static class LoansExtensions
{
    public static IServiceCollection AddLoans(this IServiceCollection services)
    {
        services.AddScoped<CreateLoanHandler>();
        services.AddScoped<GetLoanHandler>();
        return services;
    }
}
