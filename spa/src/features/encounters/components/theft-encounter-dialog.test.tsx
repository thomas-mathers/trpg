import { HubConnectionState } from '@microsoft/signalr';
import { cleanup, configure, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import type { TheftEncounterState } from '@/features/encounters/encounter';
import { GameChatContext, type GameChat } from '@/features/game/hooks/use-game-chat';
import {
  GameHubConnectionContext,
  type GameHubConnection,
} from '@/features/game/hooks/use-game-hub-connection';
import { gameEventBus } from '@/lib/game-event-bus';
import { renderWithProviders } from '@/test/test-utils';

import { TheftEncounterDialog } from './theft-encounter-dialog';

const encounter: TheftEncounterState = {
  encounterId: 'encounter-id',
  ownerName: 'Tessa',
  itemNames: ['Silver necklace'],
  allowedActions: ['Apologize', 'Fight'],
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
    resolveApologizeTheftEncounterAction: vi.fn(),
    resolveFightTheftEncounterAction: vi.fn(),
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
        <TheftEncounterDialog />
      </GameChatContext.Provider>
    </GameHubConnectionContext.Provider>,
  );

  return { ...result, gameChat, chatHub: hubConnection.chatHub! };
}

beforeAll(() => configure({ asyncUtilTimeout: 2000 }));
afterAll(() => configure({ asyncUtilTimeout: 1000 }));

afterEach(() => {
  gameEventBus.emit('TheftEncounterResolved', {} as never);
  cleanup();
});

async function resolveEncounter() {
  gameEventBus.emit('TheftEncounterResolved', {
    encounterId: encounter.encounterId,
    outcome: 'Apologized',
    ownerName: encounter.ownerName,
    itemNames: encounter.itemNames,
    itemsReturned: true,
  });
  await waitFor(() =>
    expect(screen.queryByRole('dialog', { hidden: true })).not.toBeInTheDocument(),
  );
}

describe('TheftEncounterDialog', () => {
  it('shows a received theft encounter and sends the selected apology action', async () => {
    const { user, gameChat, chatHub } = renderDialog();

    gameEventBus.emit('TheftEncounterStarted', encounter);

    expect(await screen.findByRole('dialog')).toHaveTextContent('Tessa');
    expect(screen.getByRole('dialog')).toHaveTextContent('Silver necklace');
    await user.click(screen.getByRole('button', { name: /Apologize/ }));

    expect(chatHub.resolveApologizeTheftEncounterAction).toHaveBeenCalledOnce();
    expect(gameChat.submitNarratedTurn).toHaveBeenCalledWith(
      'Apologize',
      vi.mocked(chatHub.resolveApologizeTheftEncounterAction).mock.results[0]?.value,
    );
    await resolveEncounter();
  });

  it('stays hidden while detection narration is still streaming', async () => {
    renderDialog({ isStreaming: true });

    gameEventBus.emit('TheftEncounterStarted', encounter);

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });
});
