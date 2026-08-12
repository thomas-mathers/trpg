import { screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import type { QuestJournalEntrySnapshot, SceneSnapshot, TradeSnapshot } from '@/api/client';
import { handleGetQuestJournal, handleGetTrade } from '@/api/client/msw.gen';
import { server } from '@/test/server';
import { renderWithProviders } from '@/test/test-utils';

import { NearbyPanel } from './nearby-panel';

function scene(tradeWorkstationId: string | null | undefined): SceneSnapshot {
  return {
    worldId: 'world-id',
    buildingName: 'The General Store',
    exits: [],
    nearbyBuildings: [],
    nearbyCreatures: [
      {
        id: 'merchant-id',
        name: 'Tessa',
        level: 1,
        state: 'Idle',
        reputation: null,
        tradeWorkstationId,
      },
    ],
    playerStatus: { id: 'player-id', level: 1 },
  } as unknown as SceneSnapshot;
}

const emptyTrade: TradeSnapshot = {
  playerInventory: { gold: 0, items: [] },
  shopInventory: { gold: 0, items: [] },
};

const emptyJournal: QuestJournalEntrySnapshot[] = [];

describe('NearbyPanel', () => {
  beforeEach(() => {
    server.use(handleGetQuestJournal({ body: emptyJournal }));
  });

  it('opens trade for a worker assigned to a trade workstation', async () => {
    let requestedPath: { playerId: string; workstationId: string } | undefined;
    server.use(
      handleGetTrade(({ params }) => {
        requestedPath = params;
        return HttpResponse.json(emptyTrade);
      }),
    );
    const { user } = renderWithProviders(
      <NearbyPanel scene={scene('workstation-id')} onOpenQuestJournal={() => {}} />,
    );

    await user.click(screen.getByRole('button', { name: 'Trade' }));

    expect(await screen.findByRole('heading', { name: 'Trade with Tessa' })).toBeVisible();
    await waitFor(() =>
      expect(requestedPath).toMatchObject({
        playerId: 'player-id',
        workstationId: 'workstation-id',
      }),
    );
  });

  it('does not show trade when a scene snapshot omits the trade workstation ID', () => {
    renderWithProviders(<NearbyPanel scene={scene(undefined)} onOpenQuestJournal={() => {}} />);

    expect(screen.queryByRole('button', { name: 'Trade' })).not.toBeInTheDocument();
  });
});
