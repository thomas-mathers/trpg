import { HubConnectionState } from '@microsoft/signalr';
import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { AbilityAvailabilityResponse, AbilitySummary } from '@/api/client';
import {
  handleGetCreatureAbilities,
  handleGetPlayerAbilityAvailability,
  handleGetQuestJournal,
} from '@/api/client/msw.gen';
import type {
  ActionResult,
  CreatureStatusSnapshot,
  SceneSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import { SceneContext } from '@/features/game/contexts/scene-context';
import {
  GameHubConnectionContext,
  type GameHubConnection,
} from '@/features/game/hooks/use-game-hub-connection';
import { TargetContext } from '@/features/game/hooks/use-target';
import { server } from '@/test/server';
import { renderWithProviders } from '@/test/test-utils';

import { CastTargetingProvider } from '../providers/cast-targeting-provider';
import { AbilityToolbar } from './ability-toolbar';

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

const tessa = {
  id: 'tessa-id',
  name: 'Tessa',
  creatureType: 'Human',
  level: 1,
  condition: 'Awake',
} as unknown as CreatureStatusSnapshot;

const scene = {
  worldId: 'world-id',
  exits: [],
  nearbyBuildings: [],
  nearbyProps: [],
  nearbyCaravans: [],
  nearbyCreatures: [tessa],
  playerStatus: { id: 'player-id', level: 1 },
} as unknown as SceneSnapshot;

function renderToolbar(
  abilities: AbilitySummary[],
  availability: AbilityAvailabilityResponse[] = [],
  target?: CreatureStatusSnapshot,
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
    <SceneContext.Provider value={{ scene: scene }}>
      <GameHubConnectionContext.Provider value={hubConnection}>
        <TargetContext.Provider value={{ target, setTargetId: () => {} }}>
          <CastTargetingProvider>
            <AbilityToolbar />
          </CastTargetingProvider>
        </TargetContext.Provider>
      </GameHubConnectionContext.Provider>
    </SceneContext.Provider>,
  );

  return { ...result, chatHub };
}

describe('AbilityToolbar', () => {
  it('casts a targeted ability on the current target', async () => {
    const { user, chatHub } = renderToolbar([ability('Mend')], [], tessa);

    await user.click(await screen.findByRole('button', { name: 'Mend' }));

    expect(chatHub.sendCastAbility).toHaveBeenCalledWith('tessa-id', 'Mend');
  });

  it('casts a targeted support ability on the player when nothing is targeted', async () => {
    const { user, chatHub } = renderToolbar([ability('Mend')]);

    await user.click(await screen.findByRole('button', { name: 'Mend' }));

    expect(chatHub.sendCastAbility).toHaveBeenCalledWith('player-id', 'Mend');
  });

  it('casts an ability that needs no target immediately on the player', async () => {
    const { user, chatHub } = renderToolbar(
      [ability('Ward', { requiresTarget: false })],
      [],
      tessa,
    );

    await user.click(await screen.findByRole('button', { name: 'Ward' }));

    expect(chatHub.sendCastAbility).toHaveBeenCalledWith('player-id', 'Ward');
  });

  it('disables an offensive ability until a target is selected', async () => {
    renderToolbar([ability('Slash', { category: 'Offensive' })]);

    await waitFor(() => expect(screen.getByRole('button', { name: 'Slash' })).toBeDisabled());
  });

  it('casts an offensive ability on the current target', async () => {
    const { user, chatHub } = renderToolbar(
      [ability('Slash', { category: 'Offensive' })],
      [],
      tessa,
    );

    await user.click(await screen.findByRole('button', { name: 'Slash' }));

    expect(chatHub.sendCastAbility).toHaveBeenCalledWith('tessa-id', 'Slash');
  });

  it('disables an ability the player cannot currently use', async () => {
    renderToolbar(
      [ability('Mend')],
      [{ name: 'Mend', isUsable: false, reason: 'Not enough MP' }],
      tessa,
    );

    await waitFor(() => expect(screen.getByRole('button', { name: 'Mend' })).toBeDisabled());
  });
});
