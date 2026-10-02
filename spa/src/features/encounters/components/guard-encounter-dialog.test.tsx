import { HubConnectionState } from '@microsoft/signalr';
import { configure, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

import type { ActionResult } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import type { GuardEncounterState } from '@/features/encounters/encounter';
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

function succeeded(): Promise<ActionResult> {
  return Promise.resolve({ succeeded: true });
}

function buildChatHub(overrides: Partial<IChatHub> = {}): IChatHub {
  return {
    endSession: vi.fn(),
    sendChat: vi.fn(),
    sendWait: vi.fn(succeeded),
    sendFlee: vi.fn(succeeded),
    resolveUseAbilityCombatAction: vi.fn().mockResolvedValue(undefined),
    resolveUseItemCombatAction: vi.fn().mockResolvedValue(undefined),
    resolvePayFineEncounterAction: vi.fn(succeeded),
    resolveGoToJailEncounterAction: vi.fn(succeeded),
    resolveResistArrestEncounterAction: vi.fn(succeeded),
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

function renderDialog() {
  const hubConnection = buildGameHubConnection();
  const result = renderWithProviders(
    <GameHubConnectionContext.Provider value={hubConnection}>
      <GuardEncounterDialog />
    </GameHubConnectionContext.Provider>,
  );

  return { ...result, chatHub: hubConnection.chatHub! };
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

  it('sends the selected typed encounter action', async () => {
    const { user, chatHub } = renderDialog();

    gameEventBus.emit('GuardEncounterStarted', encounter);
    await user.click(await screen.findByRole('button', { name: /pay 120 gold/i }));

    expect(chatHub.resolvePayFineEncounterAction).toHaveBeenCalledOnce();
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
