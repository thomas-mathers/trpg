import { HubConnectionState } from '@microsoft/signalr';
import { configure, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import type { ShakedownEncounterState } from '@/features/encounters/encounter';
import { GameChatContext, type GameChat } from '@/features/game/hooks/use-game-chat';
import {
  GameHubConnectionContext,
  type GameHubConnection,
} from '@/features/game/hooks/use-game-hub-connection';
import { gameEventBus } from '@/lib/game-event-bus';
import { renderWithProviders } from '@/test/test-utils';

import { ShakedownEncounterDialog } from './shakedown-encounter-dialog';

const encounter: ShakedownEncounterState = {
  encounterId: 'encounter-id',
  factionName: 'The Broken Toll',
  locationName: 'The Old Road',
  tollAmount: 25,
  members: [
    { name: 'Mara Vane', creatureType: 'Human', level: 3 },
    { name: 'Tolliver', creatureType: 'Human', level: 2 },
  ],
  allowedActions: ['Intimidate', 'PayToll', 'Fight', 'Flee'],
  canAffordToll: true,
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
    resolveIntimidateEncounterAction: vi.fn(),
    resolvePayTollEncounterAction: vi.fn(),
    resolveFightEncounterAction: vi.fn(),
    resolveFleeShakedownEncounterAction: vi.fn(),
    ...overrides,
  } as IChatHub;
}

function renderDialog(overrides: Partial<GameChat> = {}) {
  const gameChat = buildGameChat(overrides);
  const hubConnection: GameHubConnection = {
    connectionStatus: HubConnectionState.Connected,
    connectionError: false,
    chatHub: buildChatHub(),
  };
  const result = renderWithProviders(
    <GameHubConnectionContext.Provider value={hubConnection}>
      <GameChatContext.Provider value={gameChat}>
        <ShakedownEncounterDialog />
      </GameChatContext.Provider>
    </GameHubConnectionContext.Provider>,
  );

  return { ...result, gameChat, chatHub: hubConnection.chatHub! };
}

beforeAll(() => configure({ asyncUtilTimeout: 2000 }));
afterAll(() => configure({ asyncUtilTimeout: 1000 }));
afterEach(() => gameEventBus.emit('ShakedownEncounterResolved', {} as never));

describe('ShakedownEncounterDialog', () => {
  it('shows the bandits and toll demand', async () => {
    renderDialog();

    gameEventBus.emit('ShakedownEncounterStarted', encounter);

    expect(await screen.findByRole('dialog')).toHaveTextContent('The Broken Toll');
    expect(screen.getByRole('dialog')).toHaveTextContent('25 gold');
    expect(screen.getByText('Mara Vane')).toBeInTheDocument();
    expect(screen.getByText('Tolliver')).toBeInTheDocument();
  });

  it('sends each selected shakedown action through its typed hub method', async () => {
    const { user, gameChat, chatHub } = renderDialog();

    gameEventBus.emit('ShakedownEncounterStarted', encounter);
    await user.click(await screen.findByRole('button', { name: /intimidate/i }));
    await user.click(screen.getByRole('button', { name: /pay 25 gold/i }));
    await user.click(screen.getByRole('button', { name: /^fight/i }));
    await user.click(screen.getByRole('button', { name: /^flee/i }));

    expect(chatHub.resolveIntimidateEncounterAction).toHaveBeenCalledOnce();
    expect(chatHub.resolvePayTollEncounterAction).toHaveBeenCalledOnce();
    expect(chatHub.resolveFightEncounterAction).toHaveBeenCalledOnce();
    expect(chatHub.resolveFleeShakedownEncounterAction).toHaveBeenCalledOnce();
    expect(gameChat.submitNarratedTurn).toHaveBeenCalledTimes(4);
  });

  it('disables paying when the player cannot afford the toll', async () => {
    renderDialog();

    gameEventBus.emit('ShakedownEncounterStarted', { ...encounter, canAffordToll: false });

    expect(await screen.findByRole('button', { name: /pay 25 gold/i })).toBeDisabled();
  });

  it('closes when the encounter resolves', async () => {
    renderDialog();

    gameEventBus.emit('ShakedownEncounterStarted', encounter);
    expect(await screen.findByRole('dialog')).toBeInTheDocument();

    gameEventBus.emit('ShakedownEncounterResolved', {
      encounterId: encounter.encounterId,
      outcome: 'PaidToll',
      factionName: encounter.factionName,
      locationName: encounter.locationName,
      tollAmount: encounter.tollAmount,
      memberNames: encounter.members.map((member) => member.name),
    });

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });
});
