import { byRole } from 'testing-library-selector';
import { describe, expect, it, vi } from 'vitest';

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

import { CombatConsole } from './combat-console';

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

function renderConsole({ isStreaming = false } = {}) {
  const submitCombatAction = vi.fn();
  const submitFlee = vi.fn();
  const gameChat: GameChat = {
    messages: [],
    isConnected: true,
    connectionStatus: 'connected',
    isStreaming,
    submitChatMessage: vi.fn(),
    submitCombatAction,
    submitFlee,
    endSession: vi.fn(),
  };
  const result = renderWithProviders(
    <GameChatContext.Provider value={gameChat}>
      <SceneProvider sessionId="session-id">
        <CombatConsole />
      </SceneProvider>
    </GameChatContext.Provider>,
  );

  gameEventBus.emit('SceneSnapshot', { playerStatus: { id: 'player-id' } } as SceneSnapshot);
  gameEventBus.emit('CombatStarted', fight);

  return { submitCombatAction, submitFlee, ...result };
}

describe('CombatConsole', () => {
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

    expect(submitCombatAction).toHaveBeenCalledWith(
      { type: 'UseAbilityAction', targetId: 'goblin-id', abilityName: 'Power Strike' },
      'Used Power Strike on Goblin',
    );
  });

  it('targets the player when choosing a support ability', async () => {
    server.use(
      handleGetCreatureAbilities({ body: [ability('First Aid', 'Support')] }),
      handleGetPlayerFightAbilities({ body: [] }),
    );
    const { submitCombatAction, user } = renderConsole();

    await user.click(await ui.defend.find());
    await user.click(await ui.ability('First Aid').find());

    expect(submitCombatAction).toHaveBeenCalledWith(
      { type: 'UseAbilityAction', targetId: 'player-id', abilityName: 'First Aid' },
      'Used First Aid',
    );
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

    expect(submitCombatAction).toHaveBeenCalledWith(
      { type: 'UseItemAction', itemName: 'Healing Potion' },
      'Used Healing Potion',
    );
  });

  it('disables actions while the combat-starting chat turn finishes', async () => {
    renderConsole({ isStreaming: true });

    expect(await ui.attack.find()).toBeDisabled();
  });
});
