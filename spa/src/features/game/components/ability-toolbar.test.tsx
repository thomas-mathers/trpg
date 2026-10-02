import { HubConnectionState } from '@microsoft/signalr';
import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { AbilityAvailabilityResponse, AbilitySummary } from '@/api/client';
import {
  handleGetCreatureAbilities,
  handleGetPlayerAbilityAvailability,
  handleGetQuestJournal,
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

import { CastTargetingProvider } from '../providers/cast-targeting-provider';
import { AbilityToolbar } from './ability-toolbar';
import { NearbyPanel } from './nearby-panel';

function succeeded(): Promise<ActionResult> {
  return Promise.resolve({ succeeded: true });
}

function ability(name: string, overrides: Partial<AbilitySummary> = {}): AbilitySummary {
  return {
    name,
    skill: 'Restoration',
    description: `${name} description`,
    apCost: 0,
    mpCost: 2,
    cooldownSeconds: 0,
    category: 'Support',
    requiredSkillLevel: 1,
    prerequisites: [],
    requiresTarget: true,
    ...overrides,
  };
}

const scene = {
  worldId: 'world-id',
  exits: [],
  nearbyBuildings: [],
  nearbyProps: [],
  nearbyCaravans: [],
  nearbyCreatures: [
    {
      id: 'tessa-id',
      name: 'Tessa',
      creatureType: 'Human',
      level: 1,
      condition: 'Awake',
      movement: 'Stationary',
      posture: 'Standing',
      reputation: null,
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

function renderToolbar(
  abilities: AbilitySummary[],
  availability: AbilityAvailabilityResponse[] = [],
) {
  server.use(
    handleGetQuestJournal({ body: [] }),
    handleGetCreatureAbilities({ body: abilities }),
    handleGetPlayerAbilityAvailability({ body: availability }),
  );
  const chatHub = { sendCastAbility: vi.fn(succeeded) } as unknown as IChatHub;
  const hubConnection: GameHubConnection = {
    connectionStatus: HubConnectionState.Connected,
    connectionError: false,
    chatHub,
  };

  const result = renderWithProviders(
    <SceneContext.Provider value={scene}>
      <GameHubConnectionContext.Provider value={hubConnection}>
        <CastTargetingProvider>
          <AbilityToolbar />
          <NearbyPanel
            scene={scene}
            onOpenQuestJournal={() => {}}
            onQuestDialogRequested={() => {}}
            onDeliverItemDialogRequested={() => {}}
          />
        </CastTargetingProvider>
      </GameHubConnectionContext.Provider>
    </SceneContext.Provider>,
  );

  return { ...result, chatHub };
}

describe('AbilityToolbar', () => {
  it('casts a targeted ability on the clicked creature', async () => {
    const { user, chatHub } = renderToolbar([ability('Mend')]);

    await user.click(await screen.findByRole('button', { name: 'Mend' }));
    await user.click(screen.getByText('Tessa'));

    expect(chatHub.sendCastAbility).toHaveBeenCalledWith('tessa-id', 'Mend');
  });

  it('casts a targeted ability on the player from the Yourself button', async () => {
    const { user, chatHub } = renderToolbar([ability('Mend')]);

    await user.click(await screen.findByRole('button', { name: 'Mend' }));
    await user.click(screen.getByRole('button', { name: 'Yourself' }));

    expect(chatHub.sendCastAbility).toHaveBeenCalledWith('player-id', 'Mend');
  });

  it('casts an ability that needs no target immediately on the player', async () => {
    const { user, chatHub } = renderToolbar([ability('Ward', { requiresTarget: false })]);

    await user.click(await screen.findByRole('button', { name: 'Ward' }));

    expect(chatHub.sendCastAbility).toHaveBeenCalledWith('player-id', 'Ward');
  });

  it('leaves the creature list inert until an ability is chosen', async () => {
    const { user, chatHub } = renderToolbar([ability('Mend')]);
    await screen.findByRole('button', { name: 'Mend' });

    await user.click(screen.getByText('Tessa'));

    expect(chatHub.sendCastAbility).not.toHaveBeenCalled();
  });

  it('stops targeting when cancelled', async () => {
    const { user, chatHub } = renderToolbar([ability('Mend')]);

    await user.click(await screen.findByRole('button', { name: 'Mend' }));
    await user.click(screen.getByRole('button', { name: 'Cancel' }));
    await user.click(screen.getByText('Tessa'));

    expect(chatHub.sendCastAbility).not.toHaveBeenCalled();
  });

  it('disables an ability the player cannot currently use', async () => {
    renderToolbar([ability('Mend')], [{ name: 'Mend', isUsable: false, reason: 'Not enough MP' }]);

    await waitFor(() => expect(screen.getByRole('button', { name: 'Mend' })).toBeDisabled());
  });

  it('lists offensive abilities and targets a clicked creature without offering Yourself', async () => {
    const { user, chatHub } = renderToolbar([ability('Slash', { category: 'Offensive' })]);

    await user.click(await screen.findByRole('button', { name: 'Slash' }));
    expect(screen.queryByRole('button', { name: 'Yourself' })).not.toBeInTheDocument();
    await user.click(screen.getByText('Tessa'));

    expect(chatHub.sendCastAbility).toHaveBeenCalledWith('tessa-id', 'Slash');
  });
});
