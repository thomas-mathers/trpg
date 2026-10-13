import { HubConnectionState } from '@microsoft/signalr';
import { screen } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { QuestJournalEntrySnapshot } from '@/api/client';
import {
  handleGetContainerInventory,
  handleGetCreatureInventory,
  handleGetQuestJournal,
  handleGetSignText,
  handleGetWorkstationInventory,
} from '@/api/client/msw.gen';
import type { ActionResult, SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import { SceneContext } from '@/features/game/contexts/scene-context';
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

const emptyJournal: QuestJournalEntrySnapshot[] = [];
const propSpatial = {
  model: 'ContainerBasic' as const,
  blocksMovement: true,
  placement: { x: 0, y: 0, angle: 0 },
  footprint: { width: 1, depth: 1 },
};

function succeeded(): Promise<ActionResult> {
  return Promise.resolve({ succeeded: true });
}

function buildChatHub(overrides: Partial<IChatHub> = {}): IChatHub {
  return {
    endSession: vi.fn(),
    sendChat: vi.fn(),
    sendWait: vi.fn(succeeded),
    sendSitDown: vi.fn(succeeded),
    sendStandUp: vi.fn(succeeded),
    sendSleep: vi.fn(succeeded),
    sendActivateTrigger: vi.fn(succeeded),
    sendFlee: vi.fn(succeeded),
    ...overrides,
  } as IChatHub;
}

function renderPanel(
  sceneSnapshot: SceneSnapshot,
  onQuestDialogRequested: (dialog: unknown) => void = () => {},
) {
  const chatHub = buildChatHub();
  const hubConnection: GameHubConnection = {
    connectionStatus: HubConnectionState.Connected,
    connectionError: false,
    chatHub,
  };

  const result = renderWithProviders(
    <SceneContext.Provider value={{ scene: sceneSnapshot }}>
      <GameHubConnectionContext.Provider value={hubConnection}>
        <NearbyPanel
          scene={sceneSnapshot}
          onOpenQuestJournal={() => {}}
          onQuestDialogRequested={onQuestDialogRequested}
          onDeliverItemDialogRequested={() => {}}
        />
      </GameHubConnectionContext.Provider>
    </SceneContext.Provider>,
  );

  return { ...result, chatHub };
}

describe('NearbyPanel', () => {
  it('does not list nearby creatures, which are targeted in the world', () => {
    renderPanel(scene('workstation-id'));
    expect(screen.queryByText('Tessa')).not.toBeInTheDocument();
    expect(screen.queryByText('Nearby Creatures')).not.toBeInTheDocument();
  });

  beforeEach(() => {
    server.use(handleGetQuestJournal({ body: emptyJournal }));
  });

  it('keeps seating interactions out of the side panel', () => {
    renderPanel({
      ...scene(undefined),
      nearbyProps: [
        {
          id: 'chair',
          name: 'Wooden Chair',
          type: 'Seat',
          isOccupied: false,
          isOccupiedByPlayer: false,
          ...propSpatial,
          description: '',
        },
      ],
    } as SceneSnapshot);
    expect(screen.queryByText('Nearby Seating')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Sit' })).not.toBeInTheDocument();
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
      nearbyProps: [
        {
          id: 'chest-id',
          name: 'Wooden Chest',
          description: '',
          type: 'Container',
          isOccupied: false,
          isOccupiedByPlayer: false,
          ...propSpatial,
        },
      ],
    };
    const { user } = renderPanel(sceneWithContainer);

    await user.click(screen.getByRole('button', { name: 'Wooden Chest' }));

    expect(await screen.findByRole('heading', { name: 'Transfer Items' })).toBeVisible();
    expect(screen.getByRole('region', { name: "Wooden Chest's inventory" })).toBeVisible();
  });

  it('activates a nearby trigger when its Activate button is clicked', async () => {
    const sceneWithTrigger = {
      ...scene(undefined),
      nearbyProps: [
        {
          id: 'lever-id',
          name: 'Rusty Lever',
          description: '',
          type: 'Trigger',
          isOccupied: false,
          isOccupiedByPlayer: false,
          ...propSpatial,
        },
      ],
    };
    const { user, chatHub } = renderPanel(sceneWithTrigger);
    const fakeStream = {};
    vi.mocked(chatHub.sendActivateTrigger).mockReturnValue(fakeStream as never);

    await user.click(screen.getByRole('button', { name: 'Activate' }));

    expect(chatHub.sendActivateTrigger).toHaveBeenCalledWith('lever-id');
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
        {
          id: 'workstation-id',
          name: 'Trading Counter',
          description: '',
          type: 'Trade',
          isOccupied: false,
          isOccupiedByPlayer: false,
          ...propSpatial,
        },
      ],
    };
    const { user } = renderPanel(sceneWithWorkstation);

    await user.click(screen.getByRole('button', { name: 'Trading Counter' }));

    expect(await screen.findByRole('heading', { name: 'Transfer Items' })).toBeVisible();
    expect(screen.getByRole('region', { name: "Trading Counter's inventory" })).toBeVisible();
  });

  it('opens the sleep dialog from a nearby bed', async () => {
    const sceneWithBed = {
      ...scene(undefined),
      nearbyProps: [
        {
          id: 'bed-id',
          name: 'Bed',
          description: '',
          type: 'Bed',
          isOccupied: false,
          isOccupiedByPlayer: false,
          ...propSpatial,
        },
      ],
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
          isOccupied: false,
          isOccupiedByPlayer: false,
          ...propSpatial,
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
});
