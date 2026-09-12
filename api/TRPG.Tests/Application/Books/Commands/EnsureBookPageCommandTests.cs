using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Books;
using TRPG.Application.Books.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Tests.Helpers;
using BookPage = TRPG.Domain.Models.BookPage;
using BookSubjectType = TRPG.Domain.Models.BookSubjectType;
using BookTier = TRPG.Domain.Models.BookTier;
using BookWork = TRPG.Domain.Models.BookWork;

namespace TRPG.Tests.Application.Books.Commands;

public sealed class EnsureBookPageCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private readonly BookWork _work = new()
    {
        WorldId = WorldId,
        Title = $"A History of Ravenhollow {Guid.NewGuid():N}",
        Tier = BookTier.Flavour,
        SubjectType = BookSubjectType.City,
        SubjectName = "Ravenhollow",
        PageCount = 4,
    };

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = BuildProvider(_context);

        _context.BookWorks.Add(_work);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_KeepsTheFirstCopy_WhenTurningAPageOvertakesItsWarmUp()
    {
        // Arrange — the composer lands the page from another connection midway through this one,
        // which is what a warm-up finishing while the reader turns forward looks like.
        var interloper = new PageWritingChatClient(
            db,
            _work,
            pageNumber: 2,
            text: "Written first."
        );
        await using var provider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton(new BookPageComposer(interloper))
            .BuildServiceProvider();

        // Act
        var text = await Handler(provider)
            .Handle(MakeCommand(2), TestContext.Current.CancellationToken);

        // Assert — the loser returns the winner's prose rather than failing or writing a second row.
        Assert.Equal("Written first.", text);

        await using var verifyContext = db.CreateContext();
        Assert.Single(
            await verifyContext
                .BookPages.Where(page => page.WorkId == _work.Id && page.PageNumber == 2)
                .ToArrayAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_ReturnsTheStoredPage_WithoutComposingItAgain()
    {
        // Arrange
        var first = await Handler(_serviceProvider)
            .Handle(MakeCommand(1), TestContext.Current.CancellationToken);

        // Act
        var second = await Handler(_serviceProvider)
            .Handle(MakeCommand(1), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(first, second);
    }

    private static ServiceProvider BuildProvider(TrpgDbContext context) =>
        new ServiceCollection()
            .AddTrpgTestServices(context)
            .AddSingleton(new BookPageComposer(new FakeChatClient()))
            .BuildServiceProvider();

    private static ICommandHandler<EnsureBookPageCommand, string> Handler(
        ServiceProvider serviceProvider
    ) => serviceProvider.GetRequiredService<ICommandHandler<EnsureBookPageCommand, string>>();

    private EnsureBookPageCommand MakeCommand(int pageNumber) =>
        new() { WorkId = _work.Id, PageNumber = pageNumber };

    private sealed class PageWritingChatClient(
        DatabaseFixture db,
        BookWork work,
        int pageNumber,
        string text
    ) : IChatClient
    {
        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            await using var context = db.CreateContext();
            context.BookPages.Add(
                new BookPage
                {
                    WorldId = work.WorldId,
                    WorkId = work.Id,
                    PageNumber = pageNumber,
                    Text = text,
                }
            );
            await context.SaveChangesAsync(cancellationToken);

            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "Written second."));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
