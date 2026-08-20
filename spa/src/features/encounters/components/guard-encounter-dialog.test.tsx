import { HubConnectionState } from '@microsoft/signalr';
import { configure, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import type { GuardEncounterState } from '@/features/encounters/encounter';
import { GameChatContext, type GameChat } from '@/features/game/hooks/use-game-chat';
import {
  GameHubConnectionContext,
  type GameHubConnection,
} from '@/features/game/hooks/use-game-hub-connection';
import { gameEventBus } from '@/lib/game-event-bus';
import { renderWithProviders } from '@/test/test-utils';

import { GuardEncounterDialog } from './guard-encounter-dialog';

const encounter: GuardEncounterState = {
  encounterId: 'encounter-id',
  guardName: 'Officer Brann',
  locationName: 'Market Square',
  fineAmount: 120,
  jailHours: 8,
  recentOffenses: ['Killed a guard'],
  allowedActions: ['PayFine', 'GoToJail', 'ResistArrest'],
  canAffordFine: true,
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
    resolvePayFineEncounterAction: vi.fn(),
    resolveGoToJailEncounterAction: vi.fn(),
    resolveResistArrestEncounterAction: vi.fn(),
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
        <GuardEncounterDialog />
      </GameChatContext.Provider>
    </GameHubConnectionContext.Provider>,
  );

  return { ...result, gameChat, chatHub: hubConnection.chatHub! };
}

// The dialog waits past its reveal delay before appearing, so give findBy/waitFor more time.
beforeAll(() => configure({ asyncUtilTimeout: 2000 }));
afterAll(() => configure({ asyncUtilTimeout: 1000 }));

afterEach(() => gameEventBus.emit('GuardEncounterResolved', {} as never));

describe('GuardEncounterDialog', () => {
  it('shows a received guard encounter', async () => {
    renderDialog();

    gameEventBus.emit('GuardEncounterStarted', encounter);

    expect(await screen.findByRole('dialog')).toHaveTextContent('Officer Brann');
    expect(screen.getByRole('dialog')).toHaveTextContent('Market Square');
    expect(screen.getByText('Killed a guard')).toBeInTheDocument();
  });

  it('stays hidden while narration is still streaming', async () => {
    renderDialog({ isStreaming: true });

    gameEventBus.emit('GuardEncounterStarted', encounter);

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('appears once narration finishes streaming', async () => {
    const { rerender } = renderDialog({ isStreaming: true });

    gameEventBus.emit('GuardEncounterStarted', encounter);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    rerender(
      <GameHubConnectionContext.Provider value={buildGameHubConnection()}>
        <GameChatContext.Provider value={buildGameChat({ isStreaming: false })}>
          <GuardEncounterDialog />
        </GameChatContext.Provider>
      </GameHubConnectionContext.Provider>,
    );

    expect(await screen.findByRole('dialog')).toHaveTextContent('Officer Brann');
  });

  it('sends the selected typed encounter action', async () => {
    const { user, gameChat, chatHub } = renderDialog();

    gameEventBus.emit('GuardEncounterStarted', encounter);
    await user.click(await screen.findByRole('button', { name: /pay 120 gold/i }));

    expect(chatHub.resolvePayFineEncounterAction).toHaveBeenCalledOnce();
    expect(gameChat.submitNarratedTurn).toHaveBeenCalledWith(
      'Pay the fine',
      vi.mocked(chatHub.resolvePayFineEncounterAction).mock.results[0]?.value,
    );
  });

  it('disables paying the fine when the player cannot afford it', async () => {
    renderDialog();

    gameEventBus.emit('GuardEncounterStarted', { ...encounter, canAffordFine: false });

    expect(await screen.findByRole('button', { name: /pay 120 gold/i })).toBeDisabled();
  });

  it('closes when the encounter resolves', async () => {
    renderDialog();

    gameEventBus.emit('GuardEncounterStarted', encounter);

    expect(await screen.findByRole('dialog')).toBeInTheDocument();
    gameEventBus.emit('GuardEncounterResolved', {
      encounterId: encounter.encounterId,
      outcome: 'PaidFine',
      guardName: encounter.guardName,
      locationName: encounter.locationName,
      fineAmount: encounter.fineAmount,
    });

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });
});
