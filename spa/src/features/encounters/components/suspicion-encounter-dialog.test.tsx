import { HubConnectionState } from '@microsoft/signalr';
import { cleanup, configure, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

import type { ActionResult } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import type { SuspicionEncounterState } from '@/features/encounters/encounter';
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
    resolveComplySuspicionAction: vi.fn(succeeded),
    resolveFleeSuspicionAction: vi.fn(succeeded),
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
      <SuspicionEncounterDialog />
    </GameHubConnectionContext.Provider>,
  );

  return { ...result, chatHub: hubConnection.chatHub! };
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
    const { user, chatHub } = renderDialog();

    gameEventBus.emit('SuspicionEncounterStarted', encounter);

    expect(await screen.findByRole('dialog')).toHaveTextContent('Officer Brann');
    expect(screen.getByRole('dialog')).toHaveTextContent('Market Square');
    await user.click(screen.getByRole('button', { name: /Comply/ }));

    expect(chatHub.resolveComplySuspicionAction).toHaveBeenCalledOnce();
    await resolveEncounter();
  });

  it('sends the selected flee action', async () => {
    const { user, chatHub } = renderDialog();

    gameEventBus.emit('SuspicionEncounterStarted', encounter);

    await user.click(await screen.findByRole('button', { name: /Flee/ }));

    expect(chatHub.resolveFleeSuspicionAction).toHaveBeenCalledOnce();
    await resolveEncounter();
  });
});
