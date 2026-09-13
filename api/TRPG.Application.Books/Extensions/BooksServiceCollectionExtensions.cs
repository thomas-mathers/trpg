using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Books.EventHandlers;
using TRPG.Application.Common.Events;

namespace TRPG.Application.Books.Extensions;

public static class BooksServiceCollectionExtensions
{
    public static IServiceCollection AddBooksServices(this IServiceCollection services) =>
        services
            .AddTransient<BookPageComposer>()
            .AddTransient<ItemGivenToCreatureEventHandler>()
            .AddTransient<IDomainEventConsumer<ItemGivenToCreatureEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<ItemGivenToCreatureEventHandler>()
            );
}
