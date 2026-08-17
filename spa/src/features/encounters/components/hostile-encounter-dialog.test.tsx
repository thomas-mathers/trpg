import { HubConnectionState } from '@microsoft/signalr';
import { configure, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import type { HostileEncounterState } from '@/features/encounters/encounter';
import { GameChatContext, type GameChat } from '@/features/game/hooks/use-game-chat';
import {
  GameHubConnectionContext,
  type GameHubConnection,
} from '@/features/game/hooks/use-game-hub-connection';
import { gameEventBus } from '@/lib/game-event-bus';
import { renderWithProviders } from '@/test/test-utils';

import { HostileEncounterDialog } from './hostile-encounter-dialog';

const encounter: HostileEncounterState = {
  encounterId: 'encounter-id',
  factionName: 'Goblin Raiders',
  locationName: 'The Old Road',
  members: [
    { name: 'Snag', creatureType: 'Goblin', level: 2 },
    { name: 'Rusk', creatureType: 'Goblin', level: 3 },
  ],
  allowedActions: ['Attack', 'Evade', 'Retreat'],
};

function buildGameChat(overrides: Partial<GameChat> = {}): GameChat {
  return {
    messages: [],
    isStreaming: false,
    submitNarratedTurn: vi.fn(),
    ...overrides,
  };
}

function buildChatHub(overrides: Partial<IChatHub> = {}): IChatHub {
  return {
    endSession: vi.fn(),
    receiveOpening: vi.fn(),
    sendChat: vi.fn(),
    sendWait: vi.fn(),
    sendFlee: vi.fn(),
    resolveUseAbilityCombatAction: vi.fn().mockResolvedValue(undefined),
    resolveUseItemCombatAction: vi.fn().mockResolvedValue(undefined),
    resolveAttackEncounterAction: vi.fn(),
    resolveEvadeEncounterAction: vi.fn(),
    resolveRetreatEncounterAction: vi.fn(),
    ...overrides,
  } as IChatHub;
}

function buildGameHubConnection(overrides: Partial<GameHubConnection> = {}): GameHubConnection {
  return {
    connectionStatus: HubConnectionState.Connected,
    connectionError: false,
    chatHub: buildChatHub(),
    ...overrides,
  };
}

function renderDialog(overrides: Partial<GameChat> = {}) {
  const gameChat = buildGameChat(overrides);
  const hubConnection = buildGameHubConnection();
  const result = renderWithProviders(
    <GameHubConnectionContext.Provider value={hubConnection}>
      <GameChatContext.Provider value={gameChat}>
        <HostileEncounterDialog />
      </GameChatContext.Provider>
    </GameHubConnectionContext.Provider>,
  );

  return { ...result, gameChat, chatHub: hubConnection.chatHub! };
}

// The dialog waits past its reveal delay before appearing, so give findBy/waitFor more time.
beforeAll(() => configure({ asyncUtilTimeout: 2000 }));
afterAll(() => configure({ asyncUtilTimeout: 1000 }));

afterEach(() => gameEventBus.emit('EncounterResolved', {} as never));

describe('HostileEncounterDialog', () => {
  it('shows a received hostile encounter', async () => {
    renderDialog();

    gameEventBus.emit('EncounterStarted', encounter);

    expect(await screen.findByRole('dialog')).toHaveTextContent('Goblin Raiders');
    expect(screen.getByRole('dialog')).toHaveTextContent('The Old Road');
    expect(screen.getByText('Snag')).toBeInTheDocument();
    expect(screen.getByText('Rusk')).toBeInTheDocument();
  });

  it('stays hidden while narration is still streaming', async () => {
    renderDialog({ isStreaming: true });

    gameEventBus.emit('EncounterStarted', encounter);

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('appears once narration finishes streaming', async () => {
    const { rerender } = renderDialog({ isStreaming: true });

    gameEventBus.emit('EncounterStarted', encounter);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    rerender(
      <GameHubConnectionContext.Provider value={buildGameHubConnection()}>
        <GameChatContext.Provider value={buildGameChat({ isStreaming: false })}>
          <HostileEncounterDialog />
        </GameChatContext.Provider>
      </GameHubConnectionContext.Provider>,
    );

    expect(await screen.findByRole('dialog')).toHaveTextContent('Goblin Raiders');
  });

  it('sends the selected typed encounter action', async () => {
    const { user, gameChat, chatHub } = renderDialog();

    gameEventBus.emit('EncounterStarted', encounter);
    await user.click(await screen.findByRole('button', { name: /evade/i }));

    expect(chatHub.resolveEvadeEncounterAction).toHaveBeenCalledOnce();
    expect(gameChat.submitNarratedTurn).toHaveBeenCalledWith(
      'Evade',
      vi.mocked(chatHub.resolveEvadeEncounterAction).mock.results[0]?.value,
    );
  });

  it('closes when the encounter resolves', async () => {
    renderDialog();

    gameEventBus.emit('EncounterStarted', encounter);

    expect(await screen.findByRole('dialog')).toBeInTheDocument();
    gameEventBus.emit('EncounterResolved', {
      encounterId: encounter.encounterId,
      outcome: 'Evaded',
      factionName: encounter.factionName,
      locationName: encounter.locationName,
      memberNames: encounter.members.map((member) => member.name),
    });

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });
});
