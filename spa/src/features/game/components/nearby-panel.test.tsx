import { HubConnectionState } from '@microsoft/signalr';
import { screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { QuestJournalEntrySnapshot, TradeSnapshot } from '@/api/client';
import {
  handleGetContainerInventory,
  handleGetCreatureInventory,
  handleGetQuestDialog,
  handleGetQuestJournal,
  handleGetSignText,
  handleGetTrade,
  handleGetWorkstationInventory,
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
    nearbyCaravans: [],
    nearbyCreatures: [
      {
        id: 'merchant-id',
        name: 'Tessa',
        creatureType: 'Human',
        level: 1,
        state: 'Idle',
        reputation: null,
        tradeWorkstationId,
        questMarkers: [],
        readyToDeliver: false,
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
    sendActivateTrigger: vi.fn(),
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

function renderPanel(
  sceneSnapshot: SceneSnapshot,
  onQuestDialogRequested: (dialog: unknown) => void = () => {},
) {
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
          <NearbyPanel
            scene={sceneSnapshot}
            onOpenQuestJournal={() => {}}
            onQuestDialogRequested={onQuestDialogRequested}
            onDeliverItemDialogRequested={() => {}}
          />
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

  it('shows a nearby beast inventory as read-only', async () => {
    server.use(
      handleGetCreatureInventory({
        body: { gold: 0, items: [], weight: 0, carryingCapacity: null },
      }),
    );
    const sceneWithBeast = {
      ...scene(undefined),
      nearbyCreatures: [
        {
          id: 'wolf-id',
          name: 'Ravenous Snarler',
          creatureType: 'Beast',
          level: 3,
          state: 'Idle',
          reputation: null,
          tradeWorkstationId: undefined,
        },
      ],
    } as unknown as SceneSnapshot;
    const { user } = renderPanel(sceneWithBeast);

    await user.click(screen.getByRole('button', { name: 'Ravenous Snarler' }));

    expect(await screen.findByRole('heading', { name: 'Inspect Inventory' })).toBeVisible();
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

  it('activates a nearby trigger when its Activate button is clicked', async () => {
    const sceneWithTrigger = {
      ...scene(undefined),
      nearbyProps: [{ id: 'lever-id', name: 'Rusty Lever', description: '', type: 'Trigger' }],
    };
    const { user, chatHub, gameChat } = renderPanel(sceneWithTrigger);
    const fakeStream = {};
    vi.mocked(chatHub.sendActivateTrigger).mockReturnValue(fakeStream as never);

    await user.click(screen.getByRole('button', { name: 'Activate' }));

    expect(chatHub.sendActivateTrigger).toHaveBeenCalledWith('lever-id');
    expect(gameChat.submitNarratedTurn).toHaveBeenCalledWith('Activate Rusty Lever', fakeStream);
  });

  it('opens a nearby trade workstation inventory when clicked', async () => {
    server.use(
      handleGetCreatureInventory({
        body: { gold: 0, items: [], weight: 0, carryingCapacity: null },
      }),
      handleGetWorkstationInventory({
        body: { gold: 0, items: [], weight: 0, carryingCapacity: null },
      }),
    );
    const sceneWithWorkstation = {
      ...scene(undefined),
      nearbyProps: [
        { id: 'workstation-id', name: 'Trading Counter', description: '', type: 'Trade' },
      ],
    };
    const { user } = renderPanel(sceneWithWorkstation);

    await user.click(screen.getByRole('button', { name: 'Trading Counter' }));

    expect(await screen.findByRole('heading', { name: 'Transfer Items' })).toBeVisible();
    expect(screen.getByRole('region', { name: "Trading Counter's inventory" })).toBeVisible();
  });

  it('requests the quest dialog for a nearby creature with a quest marker', async () => {
    server.use(
      handleGetQuestDialog({
        body: {
          questId: 'quest-id',
          name: 'A Dangerous Delivery',
          description: 'Help with a dangerous errand.',
          goldReward: 100,
          objectives: [],
          mode: 'Offer',
        },
      }),
    );
    const sceneWithQuestGiver = {
      ...scene(undefined),
      nearbyCreatures: [
        {
          id: 'giver-id',
          name: 'Giver',
          creatureType: 'Human',
          level: 1,
          state: 'Idle',
          reputation: null,
          questMarkers: [
            { questId: 'quest-id', name: 'A Dangerous Delivery', marker: 'Available' },
          ],
          readyToDeliver: false,
        },
      ],
    } as unknown as SceneSnapshot;
    const onQuestDialogRequested = vi.fn();
    const { user } = renderPanel(sceneWithQuestGiver, onQuestDialogRequested);

    await user.click(screen.getByRole('button', { name: 'Actions for Giver' }));
    await user.click(screen.getByRole('menuitem', { name: 'Quest: A Dangerous Delivery' }));

    await waitFor(() =>
      expect(onQuestDialogRequested).toHaveBeenCalledWith(
        expect.objectContaining({ questId: 'quest-id', mode: 'Offer', worldId: 'world-id' }),
      ),
    );
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

  it('opens the sign dialog and fetches its live text from a nearby sign', async () => {
    server.use(
      handleGetSignText(() =>
        HttpResponse.json({
          text: 'Caravan schedule:\nClockwise: next arrival Duskday, Frostwane 6 - 15:00',
        }),
      ),
    );
    const sceneWithSign = {
      ...scene(undefined),
      nearbyProps: [
        {
          id: 'sign-id',
          name: 'Caravan Schedule',
          description: 'A wooden signpost listing caravan arrival times.',
          type: 'Sign',
        },
      ],
    };
    const { user } = renderPanel(sceneWithSign);

    await user.click(screen.getByRole('button', { name: 'Caravan Schedule' }));

    expect(await screen.findByRole('heading', { name: 'Caravan Schedule' })).toBeVisible();
    expect(
      await screen.findByText(/Clockwise: next arrival Duskday, Frostwane 6 - 15:00/),
    ).toBeVisible();
  });

  it('does not show a Caravans section when no caravan is nearby', () => {
    renderPanel(scene(undefined));

    expect(screen.queryByText('Caravans')).not.toBeInTheDocument();
  });

  it('opens the caravan dialog for a lingering caravan', async () => {
    const sceneWithCaravan = {
      ...scene(undefined),
      nearbyCaravans: [
        {
          caravanId: 'caravan-id',
          routeName: 'The Capital Circuit',
          ticketFeeGold: 10,
          minutesUntilDeparture: 15,
          destinations: [
            {
              locationId: 'destination-id',
              locationName: 'Stonebridge',
              travelTimeHours: 4,
              hasTicket: false,
            },
          ],
        },
      ],
    } as unknown as SceneSnapshot;
    const { user } = renderPanel(sceneWithCaravan);

    await user.click(screen.getByRole('button', { name: 'The Capital Circuit' }));

    expect(await screen.findByRole('heading', { name: 'The Capital Circuit' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'Buy ticket' })).toBeVisible();
  });
});
