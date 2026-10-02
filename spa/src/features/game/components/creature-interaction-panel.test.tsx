import { HubConnectionState } from '@microsoft/signalr';
import { act, fireEvent, screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { TradeSnapshot } from '@/api/client';
import {
  handleGetCreatureInventory,
  handleGetQuestDialog,
  handleGetTrade,
} from '@/api/client/msw.gen';
import { handleBeginCreatureInteraction, handleEndCreatureInteraction } from '@/api/client/msw.gen';
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

import { CreatureInteractionPanel } from './creature-interaction-panel';

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
        condition: 'Awake',
        movement: 'Stationary',
        posture: 'Standing',
        reputation: null,
        tradeWorkstationId,
        questMarkers: [],
        readyToDeliver: false,
        activeConditions: {},
        activeDots: [],
        activeHots: [],
        activeBuffs: [],
      },
    ],
    playerStatus: { id: 'player-id', level: 1 },
  } as unknown as SceneSnapshot;
}

const emptyTrade: TradeSnapshot = {
  playerInventory: { gold: 0, items: [], weight: 0, carryingCapacity: null },
  shopInventory: { gold: 0, items: [], weight: 0, carryingCapacity: null },
};

function buildChatHub(overrides: Partial<IChatHub> = {}): IChatHub {
  return {
    endSession: vi.fn(),
    sendChat: vi.fn(),
    sendWait: vi.fn(),
    sendSitDown: vi.fn(),
    sendStandUp: vi.fn(),
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
  const onClose = vi.fn();
  const hubConnection: GameHubConnection = {
    connectionStatus: HubConnectionState.Connected,
    connectionError: false,
    chatHub,
  };

  const result = renderWithProviders(
    <SceneContext.Provider value={{ scene: sceneSnapshot, setMovementSpeed: () => {} }}>
      <GameHubConnectionContext.Provider value={hubConnection}>
        <GameChatContext.Provider value={gameChat}>
          <CreatureInteractionPanel
            scene={sceneSnapshot}
            creature={sceneSnapshot.nearbyCreatures[0]}
            onClose={onClose}
            onQuestDialogRequested={onQuestDialogRequested}
            onDeliverItemDialogRequested={() => {}}
          />
        </GameChatContext.Provider>
      </GameHubConnectionContext.Provider>
    </SceneContext.Provider>,
  );

  return { ...result, chatHub, gameChat, onClose };
}

describe('CreatureInteractionPanel', () => {
  beforeEach(() => {
    server.use(
      handleBeginCreatureInteraction(() => new HttpResponse(null, { status: 204 })),
      handleEndCreatureInteraction(() => new HttpResponse(null, { status: 204 })),
    );
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

    await user.click(screen.getByRole('button', { name: 'Trade' }));

    expect(await screen.findByRole('heading', { name: 'Trade with Tessa' })).toBeVisible();
    await waitFor(() =>
      expect(requestedPath).toMatchObject({
        playerId: 'player-id',
        workstationId: 'workstation-id',
      }),
    );
  });

  it('does not show trade when a scene snapshot omits the trade workstation ID', async () => {
    renderPanel(scene(undefined));

    expect(screen.queryByRole('button', { name: 'Trade' })).not.toBeInTheDocument();
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

    await user.click(screen.getByRole('button', { name: 'Inspect' }));

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
          condition: 'Awake',
          movement: 'Stationary',
          posture: 'Standing',
          reputation: null,
          tradeWorkstationId: undefined,
          activeConditions: {},
          activeDots: [],
          activeHots: [],
          activeBuffs: [],
        },
      ],
    } as unknown as SceneSnapshot;
    const { user } = renderPanel(sceneWithBeast);

    await user.click(screen.getByRole('button', { name: 'Inspect' }));

    expect(await screen.findByRole('heading', { name: 'Inspect Inventory' })).toBeVisible();
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
          condition: 'Awake',
          movement: 'Stationary',
          posture: 'Standing',
          reputation: null,
          questMarkers: [
            {
              questId: 'quest-id',
              name: 'A Dangerous Delivery',
              marker: 'Available',
            },
          ],
          readyToDeliver: false,
          activeConditions: {},
          activeDots: [],
          activeHots: [],
          activeBuffs: [],
        },
      ],
    } as unknown as SceneSnapshot;
    const onQuestDialogRequested = vi.fn();
    const { user } = renderPanel(sceneWithQuestGiver, onQuestDialogRequested);

    await user.click(screen.getByRole('button', { name: 'Quest: A Dangerous Delivery' }));

    await waitFor(() =>
      expect(onQuestDialogRequested).toHaveBeenCalledWith(
        expect.objectContaining({
          questId: 'quest-id',
          mode: 'Offer',
          worldId: 'world-id',
        }),
      ),
    );
  });

  it('opens NPC chat only after Talk and addresses free-form speech to that NPC', async () => {
    const { user, chatHub } = renderPanel(scene(undefined));
    expect(screen.queryByPlaceholderText('Say something to Tessa…')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Talk' }));
    const input = await screen.findByPlaceholderText('Say something to Tessa…');
    expect(chatHub.sendChat).toHaveBeenCalledWith(
      'I begin a conversation with "Tessa". Greet me as this person.',
    );
    await user.type(input, 'Hello{Enter}');
    expect(chatHub.sendChat).toHaveBeenLastCalledWith('Speaking to "Tessa": Hello');
  });

  it('waits for the conversation closing turn before leaving', async () => {
    const { user, chatHub, gameChat, onClose } = renderPanel(scene(undefined));
    await user.click(screen.getByRole('button', { name: 'Talk' }));
    await user.click(await screen.findByRole('button', { name: 'End conversation' }));
    expect(chatHub.sendChat).toHaveBeenLastCalledWith(
      'I end my conversation with "Tessa". Save and close this conversation.',
    );
    expect(onClose).not.toHaveBeenCalled();
    const settle = vi.mocked(gameChat.submitNarratedTurn).mock.calls.at(-1)?.[3];
    settle?.();
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('keeps the conversation open when its closing turn fails', async () => {
    const { user, gameChat, onClose } = renderPanel(scene(undefined));
    await user.click(screen.getByRole('button', { name: 'Talk' }));
    await user.click(await screen.findByRole('button', { name: 'End conversation' }));
    const call = vi.mocked(gameChat.submitNarratedTurn).mock.calls.at(-1)!;
    call[2]?.(new Error('Disconnected'));
    call[3]?.();
    expect(onClose).not.toHaveBeenCalled();
    expect(await screen.findByRole('alert')).toHaveTextContent('Could not end the conversation');
  });
  it('closes only the inspect dialog when Escape is pressed', async () => {
    server.use(
      handleGetCreatureInventory({
        body: { gold: 0, items: [], weight: 0, carryingCapacity: null },
      }),
    );
    const { user, onClose } = renderPanel(scene(undefined));
    await user.click(screen.getByRole('button', { name: 'Inspect' }));
    await screen.findByRole('dialog', { name: 'Transfer Items' });
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('dialog', { name: 'Transfer Items' })).not.toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('does not consume Escape already handled by a dialog', async () => {
    const { onClose } = renderPanel(scene(undefined));
    const event = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });
    event.preventDefault();
    await act(async () => {
      fireEvent(window, event);
    });
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(onClose).not.toHaveBeenCalled();
  });
});
