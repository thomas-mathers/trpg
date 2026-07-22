using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.WeaponProficiency.Commands;
using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameSessions;

[Collection("Database")]
public sealed class GameTurnRunnerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private GameTurnRunner _runner = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId, name: "Hero");
    private readonly FakeChatClient _chatClient = new();
    private readonly GameTurnContext _turnContext = new();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        var session = Builders.MakeGameSession(WorldId, _player.Id);
        _context.Creatures.Add(_player);
        _context.GameSessions.Add(session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _turnContext.SessionId = session.Id;

        var getPlaytime = new GetPlaytimeQueryHandler(
            _context,
            NullLogger<GetPlaytimeQueryHandler>.Instance
        );
        var updateGameSession = new UpdateGameSessionCommandHandler(
            _context,
            NullLogger<UpdateGameSessionCommandHandler>.Instance
        );
        var alwaysHit = new TestOptionsSnapshot<CombatOptions>(
            new CombatOptions { MinHitChance = 1.0f, MaxHitChance = 1.0f }
        );

        _runner = new GameTurnRunner(
            _chatClient,
            _context,
            _turnContext,
            new GetGameSessionQueryHandler(
                _context,
                NullLogger<GetGameSessionQueryHandler>.Instance
            ),
            new GetOpenConversationsQueryHandler(_context),
            new AdvanceTimeCommandHandler(getPlaytime, updateGameSession),
            updateGameSession,
            new GetChatMessagesQueryHandler(
                _context,
                NullLogger<GetChatMessagesQueryHandler>.Instance
            ),
            new AppendChatMessagesCommandHandler(
                _context,
                NullLogger<AppendChatMessagesCommandHandler>.Instance
            ),
            new ClearChatMessagesCommandHandler(
                _context,
                NullLogger<ClearChatMessagesCommandHandler>.Instance
            ),
            new GetCombatantsQueryHandler(
                new GetActiveFightQueryHandler(_context),
                new GetCreaturesByIdsQueryHandler(_context),
                new GetInventoryByCreatureIdQueryHandler(_context),
                new GetAllWeaponProficienciesQueryHandler(_context),
                new GetCreatureAbilitiesQueryHandler(_context),
                AbilityDefinitions.Create()
            ),
            new CombatEngine(
                alwaysHit,
                new HitCalculator(alwaysHit),
                new DamageCalculator(alwaysHit)
            ),
            new ResolveCombatRoundCommandHandler(
                new PersistCombatantsCommandHandler(_context),
                new AdjustWeaponProficienciesCommandHandler(_context),
                new AdjustCreatureSkillsCommandHandler(
                    _context,
                    new TestOptionsSnapshot<CreatureGeneratorOptions>(
                        new CreatureGeneratorOptions()
                    )
                ),
                new EndFightCommandHandler(
                    _context,
                    new ApplyCombatRewardsCommandHandler(_context),
                    new UpdateCreaturesCommandHandler(_context),
                    getPlaytime
                )
            ),
            new RemoveInventoryItemCommandHandler(_context),
            [],
            new TestOptionsMonitor<LlmRoleOptions>(new LlmRoleOptions()),
            new TestOptionsSnapshot<GameClockOptions>(new GameClockOptions()),
            NullLogger<GameTurnRunner>.Instance
        );
    }

    public async ValueTask DisposeAsync()
    {
        _chatClient.Dispose();
        await _context.DisposeAsync();
    }

    private static async Task<string> Drain(IAsyncEnumerable<string> tokens)
    {
        var builder = new System.Text.StringBuilder();
        await foreach (var token in tokens)
        {
            builder.Append(token);
        }
        return builder.ToString();
    }

    [Fact]
    public async Task StreamOpeningResponse_NarratesTheOpeningScene_AndPersistsTheReply()
    {
        // Act
        var narration = await Drain(
            _runner.StreamOpeningResponse(TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Equal(_chatClient.ChatResponseText, narration);
        var persisted = await _context.ChatMessages.SingleAsync(
            m => m.SessionId == _turnContext.SessionId && m.Role == "assistant",
            TestContext.Current.CancellationToken
        );
        Assert.Contains(
            _chatClient.ChatResponseText,
            persisted.MessageJson,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task StreamWaitResponse_AdvancesTimeAndNarrates()
    {
        // Act
        var narration = await Drain(
            _runner.StreamWaitResponse(3, TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Equal(_chatClient.ChatResponseText, narration);
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.GameSessions.SingleAsync(
            s => s.Id == _turnContext.SessionId,
            TestContext.Current.CancellationToken
        );
        Assert.True(updated.Playtime > TimeSpan.Zero);
    }

    [Fact]
    public async Task StreamResponse_PersistsTheUserMessage_AndNarratesTheReply()
    {
        // Act
        var narration = await Drain(
            _runner.StreamResponse("I look around.", TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Equal(_chatClient.ChatResponseText, narration);
        var userMessage = await _context.ChatMessages.SingleAsync(
            m => m.SessionId == _turnContext.SessionId && m.Role == "user",
            TestContext.Current.CancellationToken
        );
        Assert.Contains("I look around.", userMessage.MessageJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StreamCombatActionResponse_ResolvesTheAttack_AndNarratesTheOutcome()
    {
        // Arrange
        var enemy = Builders.MakeCreature(WorldId, name: "Wraith", currentHp: 50);
        var fight = Builders.MakeFight(WorldId, _player.Id, [_player.Id, enemy.Id]);
        _context.Creatures.Add(enemy);
        _context.Fights.Add(fight);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var narration = await Drain(
            _runner.StreamCombatActionResponse(
                "Strike",
                "Wraith",
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal(_chatClient.ChatResponseText, narration);
        var updatedEnemy = await _context.Creatures.SingleAsync(
            c => c.Id == enemy.Id,
            TestContext.Current.CancellationToken
        );
        Assert.True(updatedEnemy.CurrentHp < 50);
    }

    [Fact]
    public async Task StreamFleeResponse_EndsTheFight_AndNarratesTheEscape()
    {
        // Arrange
        var enemy = Builders.MakeCreature(WorldId, name: "Wraith");
        var fight = Builders.MakeFight(WorldId, _player.Id, [_player.Id, enemy.Id]);
        _context.Creatures.Add(enemy);
        _context.Fights.Add(fight);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var narration = await Drain(
            _runner.StreamFleeResponse(TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Equal(_chatClient.ChatResponseText, narration);
        await using var verifyContext = db.CreateContext();
        var updatedFight = await verifyContext.Fights.SingleAsync(
            f => f.Id == fight.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CombatOutcome.Fled, updatedFight.Outcome);
        Assert.NotNull(updatedFight.CompletedAt);
    }

    [Fact]
    public async Task StreamCombatActionResponse_ReturnsAMessage_WhenNoFightIsActive()
    {
        // Act
        var narration = await Drain(
            _runner.StreamCombatActionResponse(
                "Strike",
                "Wraith",
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal("There's no fight to act in right now.", narration);
    }

    [Fact]
    public async Task StreamCombatActionResponse_ReturnsTheRejectionReason_WhenActionIsInvalid()
    {
        // Arrange
        var enemy = Builders.MakeCreature(WorldId, name: "Wraith");
        var fight = Builders.MakeFight(WorldId, _player.Id, [_player.Id, enemy.Id]);
        _context.Creatures.Add(enemy);
        _context.Fights.Add(fight);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var narration = await Drain(
            _runner.StreamCombatActionResponse(
                "Nonexistent Move",
                "Wraith",
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal("Item Nonexistent Move not found", narration);
    }

    [Fact]
    public async Task StreamFleeResponse_ReturnsAMessage_WhenNoFightIsActive()
    {
        // Act
        var narration = await Drain(
            _runner.StreamFleeResponse(TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Equal("There's no fight to flee from right now.", narration);
    }
}
