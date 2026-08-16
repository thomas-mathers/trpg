using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Common.Validation;
using TRPG.Commands;
using TRPG.Queries;
using TRPG.Validation;

namespace TRPG.Tests.Application.Common.Commands;

public sealed class CommandHandlerDecoratorsTests
{
    [Fact]
    public async Task Handle_DoesNotInvokeInnerHandler_WhenInputIsInvalid()
    {
        var inner = new RecordingHandler();
        var handler = new ValidatingCommandHandlerDecorator<ExampleCommand, string>(
            [new DataAnnotationsCommandValidator<ExampleCommand>()],
            inner
        );

        await Assert.ThrowsAsync<InputValidationException>(() =>
            handler.Handle(new ExampleCommand { Name = " " }, TestContext.Current.CancellationToken)
        );

        Assert.False(inner.WasHandled);
    }

    [Fact]
    public async Task Handle_InvokesInnerHandler_WhenInputIsValid()
    {
        var inner = new RecordingHandler();
        var handler = new ValidatingCommandHandlerDecorator<ExampleCommand, string>(
            [new DataAnnotationsCommandValidator<ExampleCommand>()],
            inner
        );

        var result = await handler.Handle(
            new ExampleCommand { Name = "Move" },
            TestContext.Current.CancellationToken
        );

        Assert.True(inner.WasHandled);
        Assert.Equal("Move", result);
    }

    [Fact]
    public async Task Handle_LogsStartAndFailure_WhenValidationFails()
    {
        var logger = new RecordingLogger<LoggedCommandHandlerDecorator<ExampleCommand, string>>();
        var inner = new RecordingHandler();
        var validatingHandler = new ValidatingCommandHandlerDecorator<ExampleCommand, string>(
            [new DataAnnotationsCommandValidator<ExampleCommand>()],
            inner
        );
        var handler = new LoggedCommandHandlerDecorator<ExampleCommand, string>(
            validatingHandler,
            logger
        );

        await Assert.ThrowsAsync<InputValidationException>(() =>
            handler.Handle(new ExampleCommand { Name = " " }, TestContext.Current.CancellationToken)
        );

        Assert.Equal("Handling ExampleCommand", logger.Messages[0]);
        Assert.StartsWith(
            "Failed ExampleCommand after ",
            logger.Messages[1],
            StringComparison.Ordinal
        );
        Assert.False(inner.WasHandled);
    }

    [Fact]
    public async Task Resolve_UsesScrutorDecorators_WhenHandlerIsRegisteredByInterface()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddTransient<
                ICommandValidator<ExampleCommand>,
                DataAnnotationsCommandValidator<ExampleCommand>
            >()
            .AddTransient<ICommandHandler<ExampleCommand, string>, RecordingHandler>()
            .Decorate(typeof(ICommandHandler<,>), typeof(ValidatingCommandHandlerDecorator<,>))
            .Decorate(typeof(ICommandHandler<,>), typeof(LoggedCommandHandlerDecorator<,>));
        using var serviceProvider = services.BuildServiceProvider();
        var handler = serviceProvider.GetRequiredService<ICommandHandler<ExampleCommand, string>>();

        await Assert.ThrowsAsync<InputValidationException>(() =>
            handler.Handle(new ExampleCommand { Name = " " }, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_LogsStartAndCompletion_ForQuery()
    {
        var logger = new RecordingLogger<LoggedQueryHandlerDecorator<ExampleQuery, string>>();
        var handler = new LoggedQueryHandlerDecorator<ExampleQuery, string>(
            new RecordingQueryHandler(),
            logger
        );

        var result = await handler.Handle(
            new ExampleQuery { Name = "Move" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal("Move", result);
        Assert.Equal(2, logger.Messages.Count);
        Assert.Equal("Handling ExampleQuery", logger.Messages[0]);
        Assert.StartsWith("Handled ExampleQuery in ", logger.Messages[1], StringComparison.Ordinal);
    }

    private sealed class ExampleCommand
    {
        [NotBlank]
        public required string Name { get; init; }
    }

    private sealed class RecordingHandler : ICommandHandler<ExampleCommand, string>
    {
        public bool WasHandled { get; private set; }

        public Task<string> Handle(
            ExampleCommand command,
            CancellationToken cancellationToken = default
        )
        {
            WasHandled = true;
            return Task.FromResult(command.Name);
        }
    }

    private sealed class ExampleQuery
    {
        public required string Name { get; init; }
    }

    private sealed class RecordingQueryHandler : IQueryHandler<ExampleQuery, string>
    {
        public Task<string> Handle(
            ExampleQuery query,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(query.Name);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public Collection<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Messages.Add(formatter(state, exception));
    }
}
