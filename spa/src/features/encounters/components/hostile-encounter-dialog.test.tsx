import { configure, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

import type { HostileEncounterState } from '@/features/encounters/encounter';
import { GameChatContext } from '@/features/game/game-chat-context';
import type { GameChat } from '@/features/game/hooks/use-game-chat';
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

function renderDialog({ isStreaming = false } = {}) {
  const submitEncounterAction = vi.fn();
  const gameChat: GameChat = {
    messages: [],
    isConnected: true,
    connectionStatus: 'connected',
    isStreaming,
    submitChatMessage: vi.fn(),
    submitEncounterAction,
    submitFlee: vi.fn(),
    submitCombatAction: vi.fn(),
    endSession: vi.fn(),
  };

  const result = renderWithProviders(
    <GameChatContext.Provider value={gameChat}>
      <HostileEncounterDialog />
    </GameChatContext.Provider>,
  );

  return { ...result, submitEncounterAction };
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

    const submitEncounterAction = vi.fn();
    rerender(
      <GameChatContext.Provider
        value={{
          messages: [],
          isConnected: true,
          connectionStatus: 'connected',
          isStreaming: false,
          submitChatMessage: vi.fn(),
          submitEncounterAction,
          submitFlee: vi.fn(),
          submitCombatAction: vi.fn(),
          endSession: vi.fn(),
        }}
      >
        <HostileEncounterDialog />
      </GameChatContext.Provider>,
    );

    expect(await screen.findByRole('dialog')).toHaveTextContent('Goblin Raiders');
  });

  it('sends the selected typed encounter action', async () => {
    const { user, submitEncounterAction } = renderDialog();

    gameEventBus.emit('EncounterStarted', encounter);
    await user.click(await screen.findByRole('button', { name: /evade/i }));

    expect(submitEncounterAction).toHaveBeenCalledWith({ type: 'EvadeEncounterAction' }, 'Evade');
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
