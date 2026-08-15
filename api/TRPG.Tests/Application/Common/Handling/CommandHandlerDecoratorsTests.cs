using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Handling;
using TRPG.Handling;

namespace TRPG.Tests.Application.Common.Handling;

public sealed class CommandHandlerDecoratorsTests
{
    [Fact]
    public async Task Handle_DoesNotInvokeInnerHandler_WhenInputIsInvalid()
    {
        var inner = new RecordingHandler();
        var handler = new ValidatingCommandHandler<ExampleCommand, string>(
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
        var handler = new ValidatingCommandHandler<ExampleCommand, string>(
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
    public async Task Handle_LogsDuration_WhenValidationFails()
    {
        var logger = new RecordingLogger<TimedCommandHandler<ExampleCommand, string>>();
        var inner = new RecordingHandler();
        var validatingHandler = new ValidatingCommandHandler<ExampleCommand, string>(
            [new DataAnnotationsCommandValidator<ExampleCommand>()],
            inner
        );
        var handler = new TimedCommandHandler<ExampleCommand, string>(validatingHandler, logger);

        await Assert.ThrowsAsync<InputValidationException>(() =>
            handler.Handle(new ExampleCommand { Name = " " }, TestContext.Current.CancellationToken)
        );

        Assert.Single(logger.Messages);
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
            .Decorate(typeof(ICommandHandler<,>), typeof(ValidatingCommandHandler<,>))
            .Decorate(typeof(ICommandHandler<,>), typeof(TimedCommandHandler<,>));
        using var serviceProvider = services.BuildServiceProvider();
        var handler = serviceProvider.GetRequiredService<ICommandHandler<ExampleCommand, string>>();

        await Assert.ThrowsAsync<InputValidationException>(() =>
            handler.Handle(new ExampleCommand { Name = " " }, TestContext.Current.CancellationToken)
        );
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
