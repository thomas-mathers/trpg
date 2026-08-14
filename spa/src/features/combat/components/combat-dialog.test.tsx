import { act, configure, screen, waitFor } from '@testing-library/react';
import { byRole } from 'testing-library-selector';
import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest';

import type {
  AbilitySummary,
  CombatantState,
  ConsumableSummary,
  FightState,
  SceneSnapshot,
} from '@/api/client';
import {
  handleGetCreatureAbilities,
  handleGetCreatureConsumables,
  handleGetPlayerFightAbilities,
} from '@/api/client/msw.gen';
import { GameChatContext } from '@/features/game/game-chat-context';
import type { GameChat } from '@/features/game/hooks/use-game-chat';
import { SceneProvider } from '@/features/game/providers/scene-provider';
import { gameEventBus } from '@/lib/game-event-bus';
import { server } from '@/test/server';
import { renderWithProviders } from '@/test/test-utils';

import { CombatDialog } from './combat-dialog';

const player: CombatantState = {
  id: 'player-id',
  name: 'Player',
  level: 1,
  isPlayer: true,
  isAlive: true,
  currentHp: 10,
  maximumHp: 10,
  currentAp: 10,
  maximumAp: 10,
  currentMp: 10,
  maximumMp: 10,
  activeConditions: {},
  activeDots: [],
  activeHots: [],
  activeBuffs: [],
};

const goblin: CombatantState = { ...player, id: 'goblin-id', name: 'Goblin', isPlayer: false };
const fight: FightState = { combatants: [player, goblin] };

const ui = {
  attack: byRole('button', { name: 'Attack' }),
  defend: byRole('button', { name: 'Defend' }),
  item: byRole('button', { name: 'Item' }),
  flee: byRole('button', { name: 'Flee' }),
  ability: (name: string) => byRole('button', { name: new RegExp(name) }),
  consumable: (name: string) => byRole('button', { name: new RegExp(name) }),
  target: (name: string) => byRole('button', { name: new RegExp(name) }),
};

function ability(name: string, category: AbilitySummary['category']): AbilitySummary {
  return {
    name,
    skill: 'Melee',
    description: `${name} description`,
    apCost: 1,
    mpCost: 0,
    cooldown: 0,
    category,
    requiredSkillLevel: 0,
    prerequisites: [],
  };
}

function buildGameChat(overrides: Partial<GameChat> = {}): GameChat {
  return {
    messages: [],
    isConnected: true,
    connectionStatus: 'connected',
    isStreaming: false,
    submitChatMessage: vi.fn(),
    submitEncounterAction: vi.fn(),
    submitFlee: vi.fn(),
    submitCombatAction: vi.fn().mockResolvedValue(undefined),
    endSession: vi.fn(),
    ...overrides,
  };
}

// The combat dialog waits past its reveal delay before appearing, so give findBy/waitFor more time.
beforeAll(() => configure({ asyncUtilTimeout: 2000 }));
afterAll(() => configure({ asyncUtilTimeout: 1000 }));

function renderConsole(overrides: Partial<GameChat> = {}) {
  const gameChat = buildGameChat(overrides);
  const result = renderWithProviders(
    <GameChatContext.Provider value={gameChat}>
      <SceneProvider sessionId="session-id">
        <CombatDialog />
      </SceneProvider>
    </GameChatContext.Provider>,
  );

  gameEventBus.emit('SceneSnapshot', { playerStatus: { id: 'player-id' } } as SceneSnapshot);
  gameEventBus.emit('CombatStarted', fight);

  return {
    submitFlee: gameChat.submitFlee,
    submitCombatAction: gameChat.submitCombatAction,
    ...result,
  };
}

describe('CombatDialog', () => {
  it('focuses Attack when combat begins', async () => {
    renderConsole();

    expect(await ui.attack.find()).toHaveFocus();
  });

  it('submits flee actions', async () => {
    const { submitFlee, user } = renderConsole();

    await user.click(await ui.flee.find());

    expect(submitFlee).toHaveBeenCalledOnce();
  });

  it('chooses an offensive ability after targeting an enemy', async () => {
    server.use(
      handleGetCreatureAbilities({ body: [ability('Power Strike', 'Offensive')] }),
      handleGetPlayerFightAbilities({ body: [] }),
    );
    const { submitCombatAction, user } = renderConsole();

    await user.click(await ui.attack.find());
    await user.click(await ui.ability('Power Strike').find());
    await user.click(await ui.target('Goblin').find());

    expect(submitCombatAction).toHaveBeenCalledWith({
      type: 'UseAbilityAction',
      targetId: 'goblin-id',
      abilityName: 'Power Strike',
    });
  });

  it('targets the player when choosing a support ability', async () => {
    server.use(
      handleGetCreatureAbilities({ body: [ability('First Aid', 'Support')] }),
      handleGetPlayerFightAbilities({ body: [] }),
    );
    const { submitCombatAction, user } = renderConsole();

    await user.click(await ui.defend.find());
    await user.click(await ui.ability('First Aid').find());

    expect(submitCombatAction).toHaveBeenCalledWith({
      type: 'UseAbilityAction',
      targetId: 'player-id',
      abilityName: 'First Aid',
    });
  });

  it('submits the chosen item', async () => {
    const potion: ConsumableSummary = {
      itemId: 'potion-id',
      name: 'Healing Potion',
      quantity: 1,
      resource: 'Hp',
      restoreAmount: 25,
    };
    server.use(handleGetCreatureConsumables({ body: [potion] }));
    const { submitCombatAction, user } = renderConsole();

    await user.click(await ui.item.find());
    await user.click(await ui.consumable('Healing Potion').find());

    expect(submitCombatAction).toHaveBeenCalledWith({
      type: 'UseItemAction',
      itemName: 'Healing Potion',
    });
  });

  it('stays hidden while the combat-starting chat turn finishes', async () => {
    renderConsole({ isStreaming: true });

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('disables actions during a round submission but keeps the dialog open', async () => {
    const potion: ConsumableSummary = {
      itemId: 'potion-id',
      name: 'Healing Potion',
      quantity: 1,
      resource: 'Hp',
      restoreAmount: 25,
    };
    server.use(handleGetCreatureConsumables({ body: [potion] }));
    let resolveAction: () => void = () => undefined;
    const submitCombatAction = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          resolveAction = resolve;
        }),
    );
    const { user } = renderConsole({ submitCombatAction });

    await user.click(await ui.item.find());
    await user.click(await ui.consumable('Healing Potion').find());

    expect(ui.attack.get()).toBeDisabled();
    expect(screen.getByRole('dialog', { name: 'Combat' })).toBeInTheDocument();

    await act(async () => resolveAction());

    await waitFor(() => expect(ui.attack.get()).not.toBeDisabled());
  });
});
