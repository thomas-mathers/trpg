import type { Meta, StoryObj } from '@storybook/react-vite';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useCallback, useEffect, useState, type ReactNode } from 'react';

import type {
  AbilityAvailability,
  AbilitySummary,
  CombatantState,
  ConsumableSummary,
  DamageType,
  FightState,
} from '@/api/client';
import {
  handleGetCreatureAbilities,
  handleGetCreatureConsumables,
  handleGetPlayerFightAbilities,
} from '@/api/client/msw.gen';
import { TooltipProvider } from '@/components/ui/tooltip';
import type { PlayerCombatAction } from '@/features/combat/combat-action';
import type { CombatRoundEvent } from '@/features/combat/combat-round-event';
import { PlayerIdContext } from '@/features/game/contexts/scene-context';
import { GameChatContext } from '@/features/game/game-chat-context';
import type { GameChat } from '@/features/game/hooks/use-game-chat';
import { gameEventBus } from '@/lib/game-event-bus';

import { CombatConsole } from './combat-console';

const player: CombatantState = {
  id: 'player-id',
  name: 'Aria',
  level: 8,
  isPlayer: true,
  isAlive: true,
  currentHp: 62,
  maximumHp: 80,
  currentAp: 14,
  maximumAp: 20,
  currentMp: 9,
  maximumMp: 15,
  activeConditions: {},
  activeDots: [],
  activeHots: [],
  activeBuffs: [],
};

const abilities: AbilitySummary[] = [
  {
    name: 'Power Strike',
    skill: 'Melee',
    description: 'A heavy blow that deals increased physical damage.',
    apCost: 4,
    mpCost: 0,
    cooldown: 0,
    category: 'Offensive',
    requiredSkillLevel: 0,
    prerequisites: [],
  },
  {
    name: 'Firebolt',
    skill: 'Destruction',
    description: 'Hurl a bolt of fire at an enemy.',
    apCost: 2,
    mpCost: 5,
    cooldown: 0,
    category: 'Offensive',
    requiredSkillLevel: 0,
    prerequisites: [],
  },
  {
    name: 'First Aid',
    skill: 'Restoration',
    description: 'Restore a small amount of health.',
    apCost: 3,
    mpCost: 2,
    cooldown: 0,
    category: 'Support',
    requiredSkillLevel: 0,
    prerequisites: [],
  },
];

const availability: AbilityAvailability[] = abilities.map((ability) => ({
  name: ability.name,
  isUsable: true,
  reason: null,
}));

const consumables: ConsumableSummary[] = [
  {
    itemId: 'healing-potion',
    name: 'Healing Potion',
    quantity: 3,
    resource: 'Hp',
    restoreAmount: 25,
  },
  { itemId: 'mana-potion', name: 'Mana Potion', quantity: 1, resource: 'Mp', restoreAmount: 15 },
];

const handlers = [
  handleGetCreatureAbilities({ body: abilities }),
  handleGetPlayerFightAbilities({ body: availability }),
  handleGetCreatureConsumables({ body: consumables }),
];

function WorkbenchProviders({
  children,
  initialFight,
  initiallyStreaming,
}: WorkbenchProvidersProps) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
      }),
  );
  const [fight, setFight] = useState(initialFight);
  const [isStreaming, setIsStreaming] = useState(initiallyStreaming);

  useEffect(() => {
    gameEventBus.emit('CombatStarted', initialFight);
  }, [initialFight]);

  const resolveAction = useCallback((action: PlayerCombatAction) => {
    setIsStreaming(true);
    window.setTimeout(() => {
      setFight((current) => {
        const round = simulateRound(current, action);
        gameEventBus.emit('CombatUpdated', round);
        return round.fightState;
      });
      setIsStreaming(false);
    }, 650);
  }, []);

  const gameChat: GameChat = {
    messages: [],
    isConnected: true,
    isStreaming,
    submitChatMessage: () => undefined,
    submitCombatAction: resolveAction,
    submitFlee: () => {
      setIsStreaming(true);
      window.setTimeout(() => {
        gameEventBus.emit('CombatEnded', 'Fled');
        setIsStreaming(false);
      }, 650);
    },
    endSession: async () => undefined,
  };

  return (
    <QueryClientProvider client={queryClient}>
      <TooltipProvider>
        <PlayerIdContext.Provider value={player.id}>
          <GameChatContext.Provider value={{ ...gameChat, isStreaming }}>
            <div className="bg-background w-[min(100vw-2rem,42rem)] p-4">{children}</div>
          </GameChatContext.Provider>
        </PlayerIdContext.Provider>
      </TooltipProvider>
    </QueryClientProvider>
  );
}

interface WorkbenchProvidersProps {
  children: ReactNode;
  initialFight: FightState;
  initiallyStreaming: boolean;
}

interface CombatConsoleStoryProps {
  fight: FightState;
  isStreaming?: boolean;
}

function CombatConsoleStory({ fight, isStreaming = false }: CombatConsoleStoryProps) {
  return (
    <WorkbenchProviders initialFight={fight} initiallyStreaming={isStreaming}>
      <CombatConsole />
    </WorkbenchProviders>
  );
}

function simulateRound(fight: FightState, action: PlayerCombatAction) {
  const playerAction = (combatant: CombatantState) => {
    if (action.type === 'UseItemAction') {
      const item = consumables.find((entry) => entry.name === action.itemName);
      if (!item || !combatant.isPlayer) {
        return combatant;
      }
      return restoreResource(combatant, item.resource, item.restoreAmount);
    }

    if (combatant.isPlayer) {
      const ability = abilities.find((entry) => entry.name === action.abilityName);
      const afterCost = ability ? spendAbilityCost(combatant, ability) : combatant;
      return action.targetId === player.id ? restoreResource(afterCost, 'Hp', 14) : afterCost;
    }

    if (combatant.id !== action.targetId) {
      return combatant;
    }

    const damage = action.abilityName === 'Firebolt' ? 18 : 14;
    const currentHp = Math.max(0, combatant.currentHp - damage);
    return { ...combatant, currentHp, isAlive: currentHp > 0 };
  };

  const afterPlayerAction = fight.combatants.map(playerAction);
  const playerAfterAction = afterPlayerAction.find((combatant) => combatant.isPlayer)!;
  const playerTarget = afterPlayerAction.find((combatant) => combatant.id === action.targetId);
  const events: CombatRoundEvent[] = [];

  if (action.type === 'UseAbilityAction' && playerTarget && !playerTarget.isPlayer) {
    events.push(
      hitEvent(
        playerAfterAction,
        playerTarget,
        action.abilityName,
        action.abilityName === 'Firebolt' ? 18 : 14,
        action.abilityName === 'Firebolt' ? 'Fire' : 'Physical',
      ),
    );
  }

  let playerHp = playerAfterAction.currentHp;
  for (const enemy of afterPlayerAction.filter(
    (combatant) => !combatant.isPlayer && combatant.isAlive,
  )) {
    playerHp = Math.max(0, playerHp - 6);
    events.push(
      hitEvent(
        enemy,
        {
          ...playerAfterAction,
          currentHp: playerHp,
          isAlive: playerHp > 0,
        },
        'Claw',
        6,
        'Physical',
      ),
    );
  }

  return {
    fightState: {
      combatants: afterPlayerAction.map((combatant) =>
        combatant.isPlayer
          ? { ...combatant, currentHp: playerHp, isAlive: playerHp > 0 }
          : combatant,
      ),
    },
    events,
  };
}

function hitEvent(
  attacker: CombatantState,
  target: CombatantState,
  abilityName: string,
  damage: number,
  damageType: DamageType,
): CombatRoundEvent {
  return {
    type: 'CombatHitEvent',
    attackerId: attacker.id,
    attackerName: attacker.name,
    abilityName,
    targetId: target.id,
    targetName: target.name,
    damage,
    damageType,
    isCritical: false,
    killed: !target.isAlive,
    targetRemainingHp: target.currentHp,
    targetMaximumHp: target.maximumHp,
    appliedConditions: [],
  };
}

function spendAbilityCost(combatant: CombatantState, ability: AbilitySummary): CombatantState {
  return {
    ...combatant,
    currentAp: Math.max(0, combatant.currentAp - ability.apCost),
    currentMp: Math.max(0, combatant.currentMp - ability.mpCost),
  };
}

function restoreResource(
  combatant: CombatantState,
  resource: ConsumableSummary['resource'],
  amount: number,
): CombatantState {
  switch (resource) {
    case 'Hp':
      return {
        ...combatant,
        currentHp: Math.min(combatant.maximumHp, combatant.currentHp + amount),
      };
    case 'Ap':
      return {
        ...combatant,
        currentAp: Math.min(combatant.maximumAp, combatant.currentAp + amount),
      };
    case 'Mp':
      return {
        ...combatant,
        currentMp: Math.min(combatant.maximumMp, combatant.currentMp + amount),
      };
  }
}

const standardFight: FightState = {
  combatants: [
    player,
    {
      ...player,
      id: 'goblin-id',
      name: 'Goblin Skirmisher',
      level: 7,
      isPlayer: false,
      currentHp: 35,
      maximumHp: 45,
    },
  ],
};

const crowdedFight: FightState = {
  combatants: [
    {
      ...player,
      activeConditions: { burning: 2 },
      activeHots: [{ abilityName: 'Regeneration', amount: 4, remainingTurns: 3 }],
    },
    {
      ...player,
      id: 'goblin-id',
      name: 'Goblin Skirmisher',
      level: 7,
      isPlayer: false,
      currentHp: 35,
      maximumHp: 45,
    },
    {
      ...player,
      id: 'cultist-id',
      name: 'Ashen Cultist',
      level: 10,
      isPlayer: false,
      currentHp: 51,
      maximumHp: 60,
      activeDots: [{ abilityName: 'Burning', amount: 6, damageType: 'Fire', remainingTurns: 2 }],
    },
    {
      ...player,
      id: 'hound-id',
      name: 'Dire Hound',
      level: 12,
      isPlayer: false,
      currentHp: 0,
      maximumHp: 52,
      isAlive: false,
    },
    {
      ...player,
      id: 'raider-id',
      name: 'Raider Captain With An Intentionally Long Name',
      level: 14,
      isPlayer: false,
      currentHp: 88,
      maximumHp: 100,
    },
  ],
};

const meta = {
  title: 'Combat/Combat Console',
  component: CombatConsoleStory,
  parameters: { msw: { handlers } },
} satisfies Meta<typeof CombatConsoleStory>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Standard: Story = { args: { fight: standardFight } };

export const CrowdedAndEffected: Story = { args: { fight: crowdedFight } };

export const Resolving: Story = { args: { fight: standardFight, isStreaming: true } };
