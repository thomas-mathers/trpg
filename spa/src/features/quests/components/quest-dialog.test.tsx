import { screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import { handleAcceptQuest, handleGetQuestJournal } from '@/api/client/msw.gen';
import { gameEventBus, type QuestDialogRequested } from '@/lib/game-event-bus';
import { server } from '@/test/server';
import { renderWithProviders } from '@/test/test-utils';

import { QuestDialog } from './quest-dialog';

const quest: QuestDialogRequested = {
  worldId: 'world-id',
  questId: 'quest-id',
  name: 'A Dangerous Delivery',
  description: 'Help with a dangerous errand.',
  goldReward: 100,
  objectives: [
    {
      name: 'Defeat Hulking Thunderfoot',
      description: 'Defeat Hulking Thunderfoot.',
      requiredAmount: 1,
    },
  ],
  mode: 'Offer',
};

describe('QuestDialog', () => {
  it('accepts the offered quest and closes', async () => {
    const onClose = vi.fn();
    let requestUrl: URL | undefined;
    server.use(
      handleAcceptQuest(({ request }) => {
        requestUrl = new URL(request.url);
        return new HttpResponse(null, { status: 204 });
      }),
      handleGetQuestJournal({ body: [] }),
    );

    const { user } = renderWithProviders(
      <QuestDialog playerId="player-id" quest={quest} onClose={onClose} />,
    );

    await user.click(screen.getByRole('button', { name: 'Accept quest' }));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(requestUrl?.searchParams.get('worldId')).toBe('world-id');
  });

  it('renders when requested through the game event bus', async () => {
    const listener = vi.fn();
    const unsubscribe = gameEventBus.on('QuestDialogRequested', listener);

    gameEventBus.emit('QuestDialogRequested', quest);

    expect(listener).toHaveBeenCalledWith(quest);
    unsubscribe();
  });
});
