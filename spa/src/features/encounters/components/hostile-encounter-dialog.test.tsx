import { HubConnectionState } from '@microsoft/signalr';
import { configure, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

import type { ActionResult } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import type { HostileEncounterState } from '@/features/encounters/encounter';
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
  allowedActions: ['Attack', 'Flee'],
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
    resolveAttackEncounterAction: vi.fn(succeeded),
    resolveFleeEncounterAction: vi.fn(succeeded),
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
      <HostileEncounterDialog />
    </GameHubConnectionContext.Provider>,
  );

  return { ...result, chatHub: hubConnection.chatHub! };
}

// The dialog waits past its reveal delay before appearing, so give findBy/waitFor more time.
beforeAll(() => configure({ asyncUtilTimeout: 2000 }));
afterAll(() => configure({ asyncUtilTimeout: 1000 }));

afterEach(() => gameEventBus.emit('HostileEncounterResolved', {} as never));

describe('HostileEncounterDialog', () => {
  it('shows a received hostile encounter', async () => {
    renderDialog();

    gameEventBus.emit('HostileEncounterStarted', encounter);

    expect(await screen.findByRole('dialog')).toHaveTextContent('Goblin Raiders');
    expect(screen.getByRole('dialog')).toHaveTextContent('The Old Road');
    expect(screen.getByText('Snag')).toBeInTheDocument();
    expect(screen.getByText('Rusk')).toBeInTheDocument();
  });

  it('sends the selected typed encounter action', async () => {
    const { user, chatHub } = renderDialog();

    gameEventBus.emit('HostileEncounterStarted', encounter);
    await user.click(await screen.findByRole('button', { name: /flee/i }));

    expect(chatHub.resolveFleeEncounterAction).toHaveBeenCalledOnce();
  });

  it('closes when the encounter resolves', async () => {
    renderDialog();

    gameEventBus.emit('HostileEncounterStarted', encounter);

    expect(await screen.findByRole('dialog')).toBeInTheDocument();
    gameEventBus.emit('HostileEncounterResolved', {
      encounterId: encounter.encounterId,
      outcome: 'Fled',
      factionName: encounter.factionName,
      locationName: encounter.locationName,
      memberNames: encounter.members.map((member) => member.name),
    });

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });
});
