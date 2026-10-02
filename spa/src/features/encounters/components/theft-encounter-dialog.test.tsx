import { HubConnectionState } from '@microsoft/signalr';
import { cleanup, configure, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

import type { ActionResult } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import type { TheftEncounterState } from '@/features/encounters/encounter';
import {
  GameHubConnectionContext,
  type GameHubConnection,
} from '@/features/game/hooks/use-game-hub-connection';
import { gameEventBus } from '@/lib/game-event-bus';
import { renderWithProviders } from '@/test/test-utils';

import { TheftEncounterDialog } from './theft-encounter-dialog';

const encounter: TheftEncounterState = {
  encounterId: 'encounter-id',
  confrontingName: 'Tessa',
  itemNames: ['Silver necklace'],
  allowedActions: ['Apologize', 'Flee'],
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
    resolveApologizeTheftEncounterAction: vi.fn(succeeded),
    resolveFleeTheftEncounterAction: vi.fn(succeeded),
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
      <TheftEncounterDialog />
    </GameHubConnectionContext.Provider>,
  );

  return { ...result, chatHub: hubConnection.chatHub! };
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
    confrontingName: encounter.confrontingName,
    itemNames: encounter.itemNames,
    itemsReturned: true,
  });
  await waitFor(() =>
    expect(screen.queryByRole('dialog', { hidden: true })).not.toBeInTheDocument(),
  );
}

describe('TheftEncounterDialog', () => {
  it('shows a received theft encounter and sends the selected apology action', async () => {
    const { user, chatHub } = renderDialog();

    gameEventBus.emit('TheftEncounterStarted', encounter);

    expect(await screen.findByRole('dialog')).toHaveTextContent('Tessa');
    expect(screen.getByRole('dialog')).toHaveTextContent('Silver necklace');
    await user.click(screen.getByRole('button', { name: /Apologize/ }));

    expect(chatHub.resolveApologizeTheftEncounterAction).toHaveBeenCalledOnce();
    await resolveEncounter();
  });

  it('sends the selected flee action', async () => {
    const { user, chatHub } = renderDialog();

    gameEventBus.emit('TheftEncounterStarted', encounter);

    await user.click(await screen.findByRole('button', { name: /Flee/ }));

    expect(chatHub.resolveFleeTheftEncounterAction).toHaveBeenCalledOnce();
    await resolveEncounter();
  });
});
