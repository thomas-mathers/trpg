import { HubConnectionState } from '@microsoft/signalr';
import { screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { ItemDetail } from '@/api/client';
import {
  handleGetContainerInventory,
  handleGetCreatureInventory,
  handleGetQuestJournal,
  handleTransferInventory,
} from '@/api/client/msw.gen';
import type { ActionResult, SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import { SidebarProvider } from '@/components/ui/sidebar';
import { SceneContext } from '@/features/game/contexts/scene-context';
import {
  GameHubConnectionContext,
  type GameHubConnection,
} from '@/features/game/hooks/use-game-hub-connection';
import { server } from '@/test/server';
import { renderWithProviders } from '@/test/test-utils';

import { NearbySidebar } from './nearby-sidebar';

const scene = {
  worldId: 'world-id',
  buildingName: 'The General Store',
  exits: [],
  nearbyBuildings: [],
  nearbyProps: [{ id: 'chest-id', name: 'Wooden Chest', description: '', type: 'Container' }],
  nearbyCaravans: [],
  nearbyCreatures: [],
  playerStatus: { id: 'player-id', level: 1 },
} as unknown as SceneSnapshot;

const item = {
  $type: 'Gold',
  itemId: 'item-id',
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
} as ItemDetail;

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
    startTheftEncounter: vi.fn(succeeded),
    ...overrides,
  } as IChatHub;
}

function renderSidebar() {
  const chatHub = buildChatHub();
  const hubConnection: GameHubConnection = {
    connectionStatus: HubConnectionState.Connected,
    connectionError: false,
    chatHub,
  };
  const result = renderWithProviders(
    <GameHubConnectionContext.Provider value={hubConnection}>
      <SceneContext.Provider value={scene}>
        <SidebarProvider>
          <NearbySidebar
            onOpenQuestJournal={() => {}}
            onQuestDialogRequested={() => {}}
            onDeliverItemDialogRequested={() => {}}
          />
        </SidebarProvider>
      </SceneContext.Provider>
    </GameHubConnectionContext.Provider>,
  );

  return { ...result, chatHub };
}

describe('NearbySidebar', () => {
  beforeEach(() => {
    vi.stubGlobal('matchMedia', () => ({
      matches: false,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    }));
  });

  afterEach(() => vi.unstubAllGlobals());

  it('starts theft narration after a caught container transfer closes', async () => {
    server.use(
      handleGetQuestJournal({ body: [] }),
      handleGetCreatureInventory({
        body: { gold: 0, items: [], weight: 0, carryingCapacity: null },
      }),
      handleGetContainerInventory({
        body: { gold: 0, items: [item], weight: 0, carryingCapacity: null },
      }),
      handleTransferInventory(() => HttpResponse.json({ theftEncounterId: 'theft-encounter-id' })),
    );
    const { chatHub, user } = renderSidebar();

    await user.click(screen.getByRole('button', { name: 'Wooden Chest' }));
    await user.click(await screen.findByRole('checkbox', { name: 'Select Silver coins' }));
    await user.click(screen.getByTitle('Move selected items to your inventory'));
    await user.click(screen.getByRole('button', { name: 'Confirm Transfer' }));

    await waitFor(() =>
      expect(chatHub.startTheftEncounter).toHaveBeenCalledWith('theft-encounter-id'),
    );
  });
});
