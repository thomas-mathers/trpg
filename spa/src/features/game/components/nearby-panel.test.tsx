import { HubConnectionState } from '@microsoft/signalr';
import { screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { QuestJournalEntrySnapshot, TradeSnapshot } from '@/api/client';
import {
  handleGetContainerInventory,
  handleGetCreatureInventory,
  handleGetQuestJournal,
  handleGetTrade,
} from '@/api/client/msw.gen';
import type { SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import { SceneContext } from '@/features/game/contexts/scene-context';
import { GameChatContext, type GameChat } from '@/features/game/hooks/use-game-chat';
import {
  GameHubConnectionContext,
  type GameHubConnection,
} from '@/features/game/hooks/use-game-hub-connection';
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
        creatureType: 'Human',
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
  playerInventory: { gold: 0, items: [], weight: 0, carryingCapacity: null },
  shopInventory: { gold: 0, items: [], weight: 0, carryingCapacity: null },
};

const emptyJournal: QuestJournalEntrySnapshot[] = [];

function buildChatHub(overrides: Partial<IChatHub> = {}): IChatHub {
  return {
    endSession: vi.fn(),
    receiveOpening: vi.fn(),
    sendChat: vi.fn(),
    sendWait: vi.fn(),
    sendSleep: vi.fn(),
    sendFlee: vi.fn(),
    ...overrides,
  } as IChatHub;
}

function buildGameChat(overrides: Partial<GameChat> = {}): GameChat {
  return {
    messages: [],
    isStreaming: false,
    submitNarratedTurn: vi.fn(),
    ...overrides,
  };
}

function renderPanel(sceneSnapshot: SceneSnapshot) {
  const chatHub = buildChatHub();
  const gameChat = buildGameChat();
  const hubConnection: GameHubConnection = {
    connectionStatus: HubConnectionState.Connected,
    connectionError: false,
    chatHub,
  };

  const result = renderWithProviders(
    <SceneContext.Provider value={sceneSnapshot}>
      <GameHubConnectionContext.Provider value={hubConnection}>
        <GameChatContext.Provider value={gameChat}>
          <NearbyPanel scene={sceneSnapshot} onOpenQuestJournal={() => {}} />
        </GameChatContext.Provider>
      </GameHubConnectionContext.Provider>
    </SceneContext.Provider>,
  );

  return { ...result, chatHub, gameChat };
}

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
    const { user } = renderPanel(scene('workstation-id'));

    await user.click(screen.getByRole('button', { name: 'Actions for Tessa' }));
    await user.click(screen.getByRole('menuitem', { name: 'Trade' }));

    expect(await screen.findByRole('heading', { name: 'Trade with Tessa' })).toBeVisible();
    await waitFor(() =>
      expect(requestedPath).toMatchObject({
        playerId: 'player-id',
        workstationId: 'workstation-id',
      }),
    );
  });

  it('does not show trade when a scene snapshot omits the trade workstation ID', async () => {
    const { user } = renderPanel(scene(undefined));

    await user.click(screen.getByRole('button', { name: 'Actions for Tessa' }));

    expect(screen.queryByRole('menuitem', { name: 'Trade' })).not.toBeInTheDocument();
  });

  it('allows transferring items from a nearby living creature', async () => {
    server.use(
      handleGetCreatureInventory(async ({ params }) =>
        HttpResponse.json({
          gold: 0,
          items:
            params.creatureId === 'merchant-id'
              ? [
                  {
                    $type: 'Gold',
                    itemId: 'coins-id',
                    name: 'Silver coins',
                    description: '',
                    weight: 0,
                    quantity: 10,
                    equippedSlot: null,
                    type: 'Gold',
                    rarity: null,
                    goldValue: 1,
                    modifiers: [],
                    isStackable: true,
                  },
                ]
              : [],
        }),
      ),
    );
    const { user } = renderPanel(scene(undefined));

    await user.click(screen.getByRole('button', { name: 'Tessa' }));

    expect(await screen.findByRole('checkbox', { name: 'Select Silver coins' })).toBeEnabled();
  });

  it('opens a nearby container inventory when clicked', async () => {
    server.use(
      handleGetCreatureInventory({
        body: { gold: 0, items: [], weight: 0, carryingCapacity: null },
      }),
      handleGetContainerInventory({
        body: { gold: 0, items: [], weight: 0, carryingCapacity: null },
      }),
    );
    const sceneWithContainer = {
      ...scene(undefined),
      nearbyProps: [{ id: 'chest-id', name: 'Wooden Chest', description: '', type: 'Container' }],
    };
    const { user } = renderPanel(sceneWithContainer);

    await user.click(screen.getByRole('button', { name: 'Wooden Chest' }));

    expect(await screen.findByRole('heading', { name: 'Transfer Items' })).toBeVisible();
    expect(screen.getByRole('region', { name: "Wooden Chest's inventory" })).toBeVisible();
  });

  it('opens the sleep dialog from a nearby bed', async () => {
    const sceneWithBed = {
      ...scene(undefined),
      hour: 8,
      nearbyProps: [{ id: 'bed-id', name: 'Bed', description: '', type: 'Bed' }],
    };
    const { user } = renderPanel(sceneWithBed);

    await user.click(screen.getByRole('button', { name: 'Actions for Bed' }));
    await user.click(screen.getByRole('menuitem', { name: 'Sleep' }));

    expect(await screen.findByRole('heading', { name: 'Sleep' })).toBeVisible();
    expect(screen.getByLabelText('Sleep until')).toBeVisible();
  });
});
