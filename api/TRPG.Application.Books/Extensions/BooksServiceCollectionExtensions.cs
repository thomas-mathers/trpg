using Microsoft.Extensions.DependencyInjection;

namespace TRPG.Application.Books.Extensions;

public static class BooksServiceCollectionExtensions
{
    public static IServiceCollection AddBooksServices(this IServiceCollection services) =>
        services.AddTransient<BookPageComposer>();
}
