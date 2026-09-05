import { HubConnectionState } from '@microsoft/signalr';
import { cleanup, configure, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import type { SuspicionEncounterState } from '@/features/encounters/encounter';
import { GameChatContext, type GameChat } from '@/features/game/hooks/use-game-chat';
import {
  GameHubConnectionContext,
  type GameHubConnection,
} from '@/features/game/hooks/use-game-hub-connection';
import { gameEventBus } from '@/lib/game-event-bus';
import { renderWithProviders } from '@/test/test-utils';

import { SuspicionEncounterDialog } from './suspicion-encounter-dialog';

const encounter: SuspicionEncounterState = {
  encounterId: 'encounter-id',
  guardName: 'Officer Brann',
  locationName: 'Market Square',
  cause: 'Sneaking',
  allowedActions: ['Comply', 'Flee'],
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
    resolveComplySuspicionAction: vi.fn(),
    resolveFleeSuspicionAction: vi.fn(),
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
        <SuspicionEncounterDialog />
      </GameChatContext.Provider>
    </GameHubConnectionContext.Provider>,
  );

  return { ...result, gameChat, chatHub: hubConnection.chatHub! };
}

beforeAll(() => configure({ asyncUtilTimeout: 2000 }));
afterAll(() => configure({ asyncUtilTimeout: 1000 }));

afterEach(() => {
  gameEventBus.emit('SuspicionEncounterResolved', {} as never);
  cleanup();
});

async function resolveEncounter() {
  gameEventBus.emit('SuspicionEncounterResolved', {
    encounterId: encounter.encounterId,
    outcome: 'Complied',
    guardName: encounter.guardName,
    locationName: encounter.locationName,
  });
  await waitFor(() =>
    expect(screen.queryByRole('dialog', { hidden: true })).not.toBeInTheDocument(),
  );
}

describe('SuspicionEncounterDialog', () => {
  it('shows a received suspicion encounter and sends the selected comply action', async () => {
    const { user, gameChat, chatHub } = renderDialog();

    gameEventBus.emit('SuspicionEncounterStarted', encounter);

    expect(await screen.findByRole('dialog')).toHaveTextContent('Officer Brann');
    expect(screen.getByRole('dialog')).toHaveTextContent('Market Square');
    await user.click(screen.getByRole('button', { name: /Comply/ }));

    expect(chatHub.resolveComplySuspicionAction).toHaveBeenCalledOnce();
    expect(gameChat.submitNarratedTurn).toHaveBeenCalledWith(
      'Comply',
      vi.mocked(chatHub.resolveComplySuspicionAction).mock.results[0]?.value,
    );
    await resolveEncounter();
  });

  it('sends the selected flee action', async () => {
    const { user, gameChat, chatHub } = renderDialog();

    gameEventBus.emit('SuspicionEncounterStarted', encounter);

    await user.click(await screen.findByRole('button', { name: /Flee/ }));

    expect(chatHub.resolveFleeSuspicionAction).toHaveBeenCalledOnce();
    expect(gameChat.submitNarratedTurn).toHaveBeenCalledWith(
      'Flee',
      vi.mocked(chatHub.resolveFleeSuspicionAction).mock.results[0]?.value,
    );
    await resolveEncounter();
  });

  it('stays hidden while narration is still streaming', async () => {
    renderDialog({ isStreaming: true });

    gameEventBus.emit('SuspicionEncounterStarted', encounter);

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });
});
