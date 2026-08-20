import { screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import type { QuestJournalEntrySnapshot, TradeSnapshot } from '@/api/client';
import {
  handleGetContainerInventory,
  handleGetCreatureInventory,
  handleGetQuestJournal,
  handleGetTrade,
} from '@/api/client/msw.gen';
import type { SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { server } from '@/test/server';
import { renderWithProviders } from '@/test/test-utils';

import { NearbyPanel } from './nearby-panel';

function scene(tradeWorkstationId: string | null | undefined): SceneSnapshot {
  return {
    worldId: 'world-id',
    buildingName: 'The General Store',
    exits: [],
    nearbyBuildings: [],
    nearbyProps: [],
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

  it('opens a nearby container inventory when clicked', async () => {
    server.use(
      handleGetCreatureInventory({ body: { gold: 0, items: [] } }),
      handleGetContainerInventory({ body: { gold: 0, items: [] } }),
    );
    const sceneWithContainer = {
      ...scene(undefined),
      nearbyProps: [{ id: 'chest-id', name: 'Wooden Chest', description: '', type: 'Container' }],
    };
    const { user } = renderWithProviders(
      <NearbyPanel scene={sceneWithContainer} onOpenQuestJournal={() => {}} />,
    );

    await user.click(screen.getByRole('button', { name: 'Wooden Chest' }));

    expect(await screen.findByRole('heading', { name: 'Transfer Items' })).toBeVisible();
    expect(screen.getByRole('region', { name: "Wooden Chest's inventory" })).toBeVisible();
  });
});
