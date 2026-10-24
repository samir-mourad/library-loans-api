namespace Library.Api.Features.Books;

public static class BooksExtensions
{
    public static IServiceCollection AddBooks(this IServiceCollection services)
    {
        services.AddScoped<CreateBookHandler>();
        services.AddScoped<GetBookHandler>();
        services.AddScoped<ListBooksHandler>();
        services.AddScoped<UpdateBookHandler>();
        services.AddScoped<DeactivateBookHandler>();
        return services;
    }
}
