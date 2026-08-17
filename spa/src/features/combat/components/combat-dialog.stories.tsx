import { HubConnectionState, type IStreamResult } from '@microsoft/signalr';
import type { Meta, StoryObj } from '@storybook/react-vite';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { DoorOpen, FlaskConical, Shield, Sparkles, Swords, type LucideIcon } from 'lucide-react';
import { motion } from 'motion/react';
import { forwardRef, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';

import type {
  AbilityAvailabilityResponse,
  AbilityCategory,
  AbilitySummary,
  ConsumableSummary,
} from '@/api/client';
import {
  handleGetCreatureAbilities,
  handleGetCreatureConsumables,
  handleGetPlayerFightAbilities,
} from '@/api/client/msw.gen';
import type {
  ActiveConditions,
  CombatActionResult,
  CombatantState,
  DamageType,
} from '@/api/signalr-client/TRPG.Combat.ClientModels';
import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import { Dialog, DialogContent, DialogTitle } from '@/components/ui/dialog';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { TooltipProvider } from '@/components/ui/tooltip';
import { AbilityPicker } from '@/features/combat/components/ability-picker';
import { CombatantCard } from '@/features/combat/components/combatant-card';
import { ItemPicker } from '@/features/combat/components/item-picker';
import { PlayerIdContext } from '@/features/game/contexts/scene-context';
import { GameChatContext, type GameChat } from '@/features/game/hooks/use-game-chat';
import {
  GameHubConnectionContext,
  type GameHubConnection,
} from '@/features/game/hooks/use-game-hub-connection';
import { gameEventBus } from '@/lib/game-event-bus';

import { CombatDialog } from './combat-dialog';

const noActiveConditions: ActiveConditions = {
  blinded: 0,
  bleeding: 0,
  burning: 0,
  disarmed: 0,
  frozen: 0,
  poisoned: 0,
  silenced: 0,
  snared: 0,
  stunned: 0,
};

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
  activeConditions: noActiveConditions,
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
    name: 'Cleave',
    skill: 'Melee',
    description: 'Swing through an enemy with a wide, punishing arc.',
    apCost: 5,
    mpCost: 0,
    cooldown: 1,
    category: 'Offensive',
    requiredSkillLevel: 2,
    prerequisites: [],
  },
  {
    name: 'Quick Shot',
    skill: 'Archery',
    description: 'Loose a fast shot before your target can react.',
    apCost: 3,
    mpCost: 0,
    cooldown: 0,
    category: 'Offensive',
    requiredSkillLevel: 1,
    prerequisites: [],
  },
  {
    name: 'Ice Lance',
    skill: 'Destruction',
    description: 'Drive a shard of ice through an enemy.',
    apCost: 2,
    mpCost: 4,
    cooldown: 0,
    category: 'Offensive',
    requiredSkillLevel: 2,
    prerequisites: [],
  },
  {
    name: 'Shadow Strike',
    skill: 'Sneak',
    description: 'Exploit an opening with a precise surprise attack.',
    apCost: 4,
    mpCost: 1,
    cooldown: 2,
    category: 'Offensive',
    requiredSkillLevel: 3,
    prerequisites: [],
  },
  {
    name: 'Arcane Burst',
    skill: 'Alteration',
    description: 'Release a focused burst of unstable magic.',
    apCost: 2,
    mpCost: 6,
    cooldown: 1,
    category: 'Offensive',
    requiredSkillLevel: 4,
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
  {
    name: 'Second Wind',
    skill: 'General',
    description: 'Regain your composure and a small amount of health.',
    apCost: 4,
    mpCost: 0,
    cooldown: 2,
    category: 'Support',
    requiredSkillLevel: 1,
    prerequisites: [],
  },
  {
    name: 'Ward',
    skill: 'Restoration',
    description: 'Raise a brief protective ward around yourself.',
    apCost: 3,
    mpCost: 4,
    cooldown: 1,
    category: 'Support',
    requiredSkillLevel: 2,
    prerequisites: [],
  },
  {
    name: 'Focus',
    skill: 'Illusion',
    description: 'Steady your mind and restore magical reserves.',
    apCost: 2,
    mpCost: 0,
    cooldown: 1,
    category: 'Support',
    requiredSkillLevel: 2,
    prerequisites: [],
  },
];

const availability: AbilityAvailabilityResponse[] = abilities.map((ability) => ({
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
  const [, setFight] = useState(initialFight);
  const [isStreaming, setIsStreaming] = useState(initiallyStreaming);

  useEffect(() => {
    setFight(initialFight);
    gameEventBus.emit('CombatStarted', initialFight);
  }, [initialFight]);

  const resolveAction = useCallback(
    (action: SimulatedCombatAction) =>
      new Promise<void>((resolve) => {
        window.setTimeout(() => {
          setFight((current) => {
            const round = simulateRound(current, action);
            gameEventBus.emit('CombatUpdated', round);
            return round.combatants;
          });
          resolve();
        }, 650);
      }),
    [],
  );

  const chatHub: IChatHub = {
    endSession: async () => undefined,
    receiveOpening: noopStream,
    sendChat: noopStream,
    sendWait: noopStream,
    sendFlee: noopStream,
    resolveUseAbilityCombatAction: (targetId, abilityName) =>
      resolveAction({ type: 'UseAbilityAction', targetId, abilityName }),
    resolveUseItemCombatAction: (itemName) => resolveAction({ type: 'UseItemAction', itemName }),
    resolveAttackEncounterAction: noopStream,
    resolveEvadeEncounterAction: noopStream,
    resolveRetreatEncounterAction: noopStream,
  };

  const hubConnection: GameHubConnection = {
    connectionStatus: HubConnectionState.Connected,
    connectionError: false,
    chatHub,
  };

  const gameChat: GameChat = {
    messages: [],
    isStreaming,
    submitNarratedTurn: () => {
      setIsStreaming(true);
      window.setTimeout(() => {
        gameEventBus.emit('CombatUpdated', {
          combatants: initialFight,
          actions: [],
          regenerations: [],
          resourceStates: [],
          outcome: 'Fled',
        });
        setIsStreaming(false);
      }, 650);
    },
  };

  return (
    <QueryClientProvider client={queryClient}>
      <TooltipProvider>
        <PlayerIdContext.Provider value={player.id}>
          <GameHubConnectionContext.Provider value={hubConnection}>
            <GameChatContext.Provider value={gameChat}>
              <div className="bg-background w-[min(100vw-2rem,42rem)] p-4">{children}</div>
            </GameChatContext.Provider>
          </GameHubConnectionContext.Provider>
        </PlayerIdContext.Provider>
      </TooltipProvider>
    </QueryClientProvider>
  );
}

interface WorkbenchProvidersProps {
  children: ReactNode;
  initialFight: CombatantState[];
  initiallyStreaming: boolean;
}

interface CombatDialogStoryProps {
  fight: CombatantState[];
  isStreaming?: boolean;
}

function CombatDialogStory({ fight, isStreaming = false }: CombatDialogStoryProps) {
  return (
    <WorkbenchProviders initialFight={fight} initiallyStreaming={isStreaming}>
      <CombatDialog />
    </WorkbenchProviders>
  );
}

type SimulatedCombatAction =
  | { type: 'UseAbilityAction'; targetId: string; abilityName: string }
  | { type: 'UseItemAction'; itemName: string };

function noopStream(): IStreamResult<string> {
  return { subscribe: () => ({ dispose: () => undefined }) };
}

function simulateRound(fight: CombatantState[], action: SimulatedCombatAction) {
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

  const afterPlayerAction = fight.map(playerAction);
  const playerAfterAction = afterPlayerAction.find((combatant) => combatant.isPlayer)!;
  const playerTarget =
    action.type === 'UseAbilityAction'
      ? afterPlayerAction.find((combatant) => combatant.id === action.targetId)
      : undefined;
  const actions: CombatActionResult[] = [];

  if (action.type === 'UseAbilityAction' && playerTarget && !playerTarget.isPlayer) {
    actions.push(
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
    actions.push(
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
    combatants: afterPlayerAction.map((combatant) =>
      combatant.isPlayer ? { ...combatant, currentHp: playerHp, isAlive: playerHp > 0 } : combatant,
    ),
    actions,
    regenerations: [],
    resourceStates: [],
    outcome: 'Ongoing' as const,
  };
}

function hitEvent(
  attacker: CombatantState,
  target: CombatantState,
  abilityName: string,
  damage: number,
  damageType: DamageType,
): CombatActionResult {
  return {
    outcome: 'Hit',
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
    narration: '',
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

const standardFight: CombatantState[] = [
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
];

const crowdedFight: CombatantState[] = [
  {
    ...player,
    activeConditions: { ...noActiveConditions, burning: 2 },
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
];

const meta = {
  title: 'Combat/Combat Console',
  component: CombatDialogStory,
  parameters: { msw: { handlers } },
} satisfies Meta<typeof CombatDialogStory>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Standard: Story = { args: { fight: standardFight } };

export const CrowdedAndEffected: Story = { args: { fight: crowdedFight } };

export const Resolving: Story = { args: { fight: standardFight, isStreaming: true } };

export const NarrationToastPreview: Story = {
  args: { fight: crowdedFight },
  render: () => (
    <WorkbenchProviders initialFight={crowdedFight} initiallyStreaming={false}>
      <div className="relative">
        <CombatDialog />
        <NarrationToastMock
          actor="Aria"
          ability="Firebolt"
          narration="A streak of fire catches the Goblin Skirmisher squarely in the chest."
        />
      </div>
    </WorkbenchProviders>
  ),
};

export const EncounterDialogPreview: Story = {
  args: { fight: crowdedFight },
  parameters: { layout: 'fullscreen' },
  render: () => (
    <WorkbenchProviders initialFight={crowdedFight} initiallyStreaming={false}>
      <TooltipProvider>
        <CombatEncounterDialogMock fight={crowdedFight} />
      </TooltipProvider>
    </WorkbenchProviders>
  ),
};

export const PlayerHitSequence: Story = {
  args: { fight: crowdedFight },
  parameters: { layout: 'fullscreen' },
  render: () => (
    <TooltipProvider>
      <CombatEncounterDialogMock fight={crowdedFight} outcome="hit" />
    </TooltipProvider>
  ),
};

export const PlayerCriticalSequence: Story = {
  args: { fight: crowdedFight },
  parameters: { layout: 'fullscreen' },
  render: () => (
    <TooltipProvider>
      <CombatEncounterDialogMock fight={crowdedFight} outcome="crit" />
    </TooltipProvider>
  ),
};

export const PlayerBlockSequence: Story = {
  args: { fight: crowdedFight },
  parameters: { layout: 'fullscreen' },
  render: () => (
    <TooltipProvider>
      <CombatEncounterDialogMock fight={crowdedFight} outcome="block" />
    </TooltipProvider>
  ),
};

export const FullRoundSequence: Story = {
  args: { fight: crowdedFight },
  parameters: { layout: 'fullscreen' },
  render: () => (
    <WorkbenchProviders initialFight={crowdedFight} initiallyStreaming={false}>
      <TooltipProvider>
        <FullRoundDialogMock fight={crowdedFight} />
      </TooltipProvider>
    </WorkbenchProviders>
  ),
};

interface NarrationToastMockProps {
  actor: string;
  ability: string;
  narration: string;
  animated?: boolean;
}

function NarrationToastMock({
  actor,
  ability,
  narration,
  animated = false,
}: NarrationToastMockProps) {
  return (
    <motion.div
      aria-live="polite"
      className="pointer-events-none absolute top-14 left-1/2 z-10 w-[calc(100%-2rem)] max-w-md -translate-x-1/2"
      initial={animated ? { opacity: 0, scale: 0.82, y: 18 } : false}
      animate={
        animated
          ? {
              opacity: [0, 1, 1, 0],
              scale: [0.82, 1, 1, 0.88],
              y: [18, 0, 0, -30],
            }
          : { opacity: 1, scale: 1, y: 0 }
      }
      role="status"
      transition={{
        duration: animated ? 1.2 : 0,
        ease: [[0.42, 0, 0.58, 1], 'linear', [0.42, 0, 0.58, 1]],
        times: [0, 0.22, 0.65, 1],
      }}
    >
      <div className="border-border/80 bg-popover/95 shadow-foreground/10 rounded-lg border px-3 py-2.5 shadow-lg backdrop-blur-sm">
        <div className="text-muted-foreground mb-1 flex items-center gap-1.5 text-[10px] font-semibold tracking-wide uppercase">
          <Sparkles className="h-3.5 w-3.5 text-amber-500" />
          <span>{actor}</span>
          <span className="text-muted-foreground/60">•</span>
          <span>{ability}</span>
        </div>
        <p className="text-sm leading-snug">{narration}</p>
      </div>
    </motion.div>
  );
}

type PlayerAttackOutcome = 'hit' | 'crit' | 'block';
type PlayerAttackPhase =
  | 'idle'
  | 'playerWindup'
  | 'targetImpact'
  | 'toast'
  | 'targetRecovery'
  | 'playerRecovery';

function CombatEncounterDialogMock({
  fight,
  outcome,
}: {
  fight: CombatantState[];
  outcome?: PlayerAttackOutcome;
}) {
  const player = fight.find((combatant) => combatant.isPlayer)!;
  const enemies = fight.filter((combatant) => !combatant.isPlayer);
  const [phase, setPhase] = useState<PlayerAttackPhase>('idle');
  const [cycle, setCycle] = useState(0);
  const [popoverContainer, setPopoverContainer] = useState<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!outcome) {
      return;
    }

    setPhase('playerWindup');
    const timers = [
      window.setTimeout(() => setPhase('targetImpact'), 260),
      window.setTimeout(() => setPhase('toast'), 460),
      window.setTimeout(() => setPhase('targetRecovery'), 1100),
      window.setTimeout(() => setPhase('playerRecovery'), 1360),
      window.setTimeout(() => {
        setPhase('idle');
        setCycle((current) => current + 1);
      }, 3000),
    ];

    return () => timers.forEach((timer) => window.clearTimeout(timer));
  }, [cycle, outcome]);

  const isPlayback = outcome !== undefined;
  const phaseAfterWindup = phase !== 'idle' && phase !== 'playerWindup';
  const targetTookHit = outcome !== 'block' && phaseAfterWindup;
  const playerDisplay = isPlayback && phase !== 'idle' ? { ...player, currentMp: 4 } : player;
  const targetDisplay = targetTookHit
    ? {
        ...enemies[0],
        currentHp: Math.max(0, Number(enemies[0].currentHp) - (outcome === 'crit' ? 24 : 18)),
      }
    : enemies[0];
  const playerOffset =
    phase === 'playerWindup' ||
    phase === 'targetImpact' ||
    phase === 'toast' ||
    phase === 'targetRecovery'
      ? 14
      : 0;
  const targetOffset =
    outcome !== 'block' && (phase === 'targetImpact' || phase === 'toast') ? 14 : 0;
  const feedbackVisible =
    phase === 'targetImpact' ||
    phase === 'toast' ||
    phase === 'targetRecovery' ||
    phase === 'playerRecovery';
  const feedback = feedbackVisible && outcome ? outcome : undefined;
  const showToast =
    !isPlayback || phase === 'toast' || phase === 'targetRecovery' || phase === 'playerRecovery';
  const narration =
    outcome === 'crit'
      ? 'Fire erupts through the Goblin Skirmisher, leaving it reeling.'
      : outcome === 'block'
        ? 'The Goblin Skirmisher catches the firebolt behind a battered shield.'
        : 'A streak of fire catches the Goblin Skirmisher squarely in the chest.';

  return (
    <Dialog open onOpenChange={() => undefined}>
      <DialogContent
        showCloseButton={false}
        className="h-[min(100dvh-2rem,56rem)] w-[min(100vw-2rem,72rem)] max-w-[calc(100%-2rem)] gap-0 overflow-hidden p-0 sm:max-w-[72rem]"
      >
        <DialogTitle className="sr-only">Combat encounter</DialogTitle>
        <div ref={setPopoverContainer} className="bg-muted flex h-full min-h-0 flex-col">
          <header className="bg-card flex items-center justify-between border-b px-5 py-3">
            <span className="flex items-center gap-2 text-sm font-semibold">
              <Swords className="h-4 w-4 text-amber-500" />
              Ambush at Ashen Ford
            </span>
            <span className="text-muted-foreground text-xs">Round 4</span>
          </header>

          <InitiativePreview combatants={fight} />

          <div className="relative min-h-0 flex-1 overflow-y-auto p-4 sm:p-5 md:px-10">
            {showToast && (
              <NarrationToastMock
                actor="Aria"
                ability="Firebolt"
                animated={isPlayback}
                narration={narration}
              />
            )}

            <div className="grid min-h-full grid-cols-1 gap-5 pt-14 md:grid-cols-[16rem_16rem] md:justify-between md:pt-12">
              <section className="flex flex-col justify-center">
                <motion.div
                  animate={{ x: playerOffset }}
                  transition={{ duration: 0.18, ease: [0.22, 1, 0.36, 1] }}
                >
                  <CombatantPreviewCard
                    key={cycle}
                    combatant={playerDisplay}
                    isSelf
                    playerLevel={Number(player.level)}
                    turnActive
                    actionState={
                      isPlayback && (phase === 'playerWindup' || phase === 'targetImpact')
                        ? 'attacking'
                        : !isPlayback
                          ? 'attacking'
                          : undefined
                    }
                  />
                </motion.div>
              </section>

              <section className="flex flex-col justify-center">
                <div className="grid gap-4">
                  <motion.div
                    animate={{ x: targetOffset }}
                    transition={{ duration: 0.18, ease: [0.22, 1, 0.36, 1] }}
                  >
                    <CombatantPreviewCard
                      key={cycle}
                      combatant={targetDisplay}
                      feedback={isPlayback ? feedback : 'hit'}
                      loopFeedback={!isPlayback}
                      playerLevel={Number(player.level)}
                    />
                  </motion.div>
                  <CombatantPreviewCard
                    combatant={enemies[1]}
                    feedback={isPlayback ? undefined : 'crit'}
                    playerLevel={Number(player.level)}
                  />
                  <CombatantPreviewCard
                    combatant={enemies[2]}
                    feedback={isPlayback ? undefined : 'miss'}
                    playerLevel={Number(player.level)}
                  />
                  <CombatantPreviewCard
                    combatant={enemies[3]}
                    feedback={isPlayback ? undefined : 'block'}
                    playerLevel={Number(player.level)}
                  />
                </div>
              </section>
            </div>
          </div>

          <div className="bg-card shrink-0 border-t p-3">
            <EncounterActionMenuMock playerId={player.id} popoverContainer={popoverContainer} />
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

type FullRoundPhase =
  | 'windup'
  | 'impact'
  | 'toast'
  | 'defenderRecovery'
  | 'attackerRecovery'
  | 'roundRegen';

function FullRoundDialogMock({ fight }: { fight: CombatantState[] }) {
  const turnOrder = useMemo(() => {
    const player = fight.find((combatant) => combatant.isPlayer)!;
    const enemies = fight.filter((combatant) => !combatant.isPlayer);
    return [
      {
        actorId: player.id,
        targetId: enemies[0].id,
        outcome: 'hit' as const,
        resource: 'Mp',
        cost: 5,
      },
      {
        actorId: enemies[0].id,
        targetId: player.id,
        outcome: 'hit' as const,
        resource: 'Ap',
        cost: 4,
      },
      {
        actorId: enemies[1].id,
        targetId: player.id,
        outcome: 'block' as const,
        resource: 'Mp',
        cost: 3,
      },
      {
        actorId: enemies[3].id,
        targetId: player.id,
        outcome: 'crit' as const,
        resource: 'Ap',
        cost: 4,
      },
    ];
  }, [fight]);
  const [combatants, setCombatants] = useState(fight);
  const [turnIndex, setTurnIndex] = useState(0);
  const [phase, setPhase] = useState<FullRoundPhase>('windup');
  const [cycle, setCycle] = useState(0);
  const [popoverContainer, setPopoverContainer] = useState<HTMLDivElement | null>(null);
  const turn = turnOrder[turnIndex];
  const actor = combatants.find((combatant) => combatant.id === turn.actorId)!;
  const target = combatants.find((combatant) => combatant.id === turn.targetId)!;

  useEffect(() => {
    setPhase('windup');
    setCombatants((current) =>
      adjustResources(
        current,
        turn.actorId,
        turn.resource === 'Ap' ? -turn.cost : 0,
        turn.resource === 'Mp' ? -turn.cost : 0,
      ),
    );

    const timers = [
      window.setTimeout(() => {
        setPhase('impact');
        if (turn.outcome !== 'block') {
          setCombatants((current) =>
            applyDamage(current, turn.targetId, turn.outcome === 'crit' ? 16 : 10),
          );
        }
      }, 220),
      window.setTimeout(() => setPhase('toast'), 420),
      window.setTimeout(() => setPhase('defenderRecovery'), 1020),
      window.setTimeout(() => setPhase('attackerRecovery'), 1280),
      window.setTimeout(() => {
        if (turnIndex < turnOrder.length - 1) {
          setTurnIndex((current) => current + 1);
          return;
        }

        setPhase('roundRegen');
        setCombatants((current) => regenerateAllCombatants(current, fight));
      }, 2400),
      window.setTimeout(() => {
        if (turnIndex === turnOrder.length - 1) {
          setCombatants(fight);
          setTurnIndex(0);
          setCycle((current) => current + 1);
        }
      }, 3300),
    ];

    return () => timers.forEach((timer) => window.clearTimeout(timer));
  }, [cycle, fight, turn, turnIndex, turnOrder.length]);

  const player = combatants.find((combatant) => combatant.isPlayer)!;
  const enemies = combatants.filter((combatant) => !combatant.isPlayer);
  const actorIsPlayer = actor.isPlayer;
  const targetOffset =
    turn.outcome !== 'block' && (phase === 'impact' || phase === 'toast') ? 14 : 0;
  const actorOffset =
    phase === 'windup' || phase === 'impact' || phase === 'toast' || phase === 'defenderRecovery'
      ? actorIsPlayer
        ? 14
        : -14
      : 0;
  const feedbackVisible =
    phase === 'impact' ||
    phase === 'toast' ||
    phase === 'defenderRecovery' ||
    phase === 'attackerRecovery';
  const feedback = feedbackVisible ? turn.outcome : undefined;
  const showToast =
    phase === 'toast' || phase === 'defenderRecovery' || phase === 'attackerRecovery';
  const narration =
    turn.outcome === 'block'
      ? `${target.name} blocks ${actor.name}'s attack.`
      : turn.outcome === 'crit'
        ? `${actor.name} lands a brutal critical strike on ${target.name}.`
        : `${actor.name}'s attack hits ${target.name}.`;

  return (
    <Dialog open onOpenChange={() => undefined}>
      <DialogContent
        showCloseButton={false}
        className="h-[min(100dvh-2rem,56rem)] w-[min(100vw-2rem,72rem)] max-w-[calc(100%-2rem)] gap-0 overflow-hidden p-0 sm:max-w-[72rem]"
      >
        <DialogTitle className="sr-only">Full combat round</DialogTitle>
        <div ref={setPopoverContainer} className="bg-muted flex h-full min-h-0 flex-col">
          <header className="bg-card flex items-center justify-between border-b px-5 py-3">
            <span className="flex items-center gap-2 text-sm font-semibold">
              <Swords className="h-4 w-4 text-amber-500" />
              Ambush at Ashen Ford
            </span>
            <span className="text-muted-foreground text-xs">Full round preview</span>
          </header>

          <InitiativePreview activeCombatantId={actor.id} combatants={combatants} />

          <div className="relative min-h-0 flex-1 overflow-y-auto p-4 sm:p-5 md:px-10">
            {showToast && (
              <NarrationToastMock
                actor={actor.name}
                ability={actorIsPlayer ? 'Firebolt' : 'Claw'}
                animated
                narration={narration}
              />
            )}

            <div className="grid min-h-full grid-cols-1 gap-5 pt-14 md:grid-cols-[16rem_16rem] md:justify-between md:pt-12">
              <section className="flex flex-col justify-center">
                <motion.div
                  animate={{
                    x: actorIsPlayer
                      ? actorOffset
                      : target.id === player.id
                        ? targetOffset * -1
                        : 0,
                  }}
                  transition={{ duration: 0.18, ease: [0.22, 1, 0.36, 1] }}
                >
                  <CombatantPreviewCard
                    key={`${cycle}-${player.id}`}
                    combatant={player}
                    feedback={target.id === player.id ? feedback : undefined}
                    isSelf
                    loopFeedback={false}
                    playerLevel={Number(player.level)}
                    turnActive={actor.id === player.id}
                    actionState={
                      actor.id === player.id && phase !== 'roundRegen' ? 'attacking' : undefined
                    }
                  />
                </motion.div>
              </section>

              <section className="flex flex-col justify-center">
                <div className="grid gap-4">
                  {enemies.map((enemy) => {
                    const isActor = enemy.id === actor.id;
                    const isTarget = enemy.id === target.id;
                    return (
                      <motion.div
                        key={enemy.id}
                        animate={{ x: isActor ? actorOffset : isTarget ? targetOffset : 0 }}
                        transition={{ duration: 0.18, ease: [0.22, 1, 0.36, 1] }}
                      >
                        <CombatantPreviewCard
                          key={`${cycle}-${enemy.id}`}
                          combatant={enemy}
                          feedback={isTarget ? feedback : undefined}
                          loopFeedback={false}
                          playerLevel={Number(player.level)}
                          turnActive={isActor}
                          actionState={isActor && phase !== 'roundRegen' ? 'attacking' : undefined}
                        />
                      </motion.div>
                    );
                  })}
                </div>
              </section>
            </div>
          </div>

          <div className="bg-card shrink-0 border-t p-3">
            <EncounterActionMenuMock playerId={player.id} popoverContainer={popoverContainer} />
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function adjustResources(
  combatants: CombatantState[],
  combatantId: string,
  apDelta: number,
  mpDelta: number,
) {
  return combatants.map((combatant) =>
    combatant.id === combatantId
      ? {
          ...combatant,
          currentAp: Math.max(
            0,
            Math.min(Number(combatant.maximumAp), Number(combatant.currentAp) + apDelta),
          ),
          currentMp: Math.max(
            0,
            Math.min(Number(combatant.maximumMp), Number(combatant.currentMp) + mpDelta),
          ),
        }
      : combatant,
  );
}

function regenerateAllCombatants(
  combatants: CombatantState[],
  initialCombatants: CombatantState[],
) {
  return combatants.map((combatant) => {
    const initialCombatant = initialCombatants.find((entry) => entry.id === combatant.id);
    if (!combatant.isAlive || !initialCombatant) {
      return combatant;
    }

    return {
      ...combatant,
      currentHp: initialCombatant.currentHp,
      currentAp: initialCombatant.currentAp,
      currentMp: initialCombatant.currentMp,
    };
  });
}

function applyDamage(combatants: CombatantState[], combatantId: string, damage: number) {
  return combatants.map((combatant) => {
    if (combatant.id !== combatantId) {
      return combatant;
    }

    const currentHp = Math.max(0, Number(combatant.currentHp) - damage);
    return { ...combatant, currentHp, isAlive: currentHp > 0 };
  });
}

function InitiativePreview({
  combatants,
  activeCombatantId = combatants[0]?.id,
}: {
  combatants: CombatantState[];
  activeCombatantId?: string;
}) {
  return (
    <div className="bg-card flex items-center gap-2 overflow-x-auto border-b px-5 py-2">
      <span className="text-muted-foreground shrink-0 text-[10px] font-semibold tracking-wide uppercase">
        Initiative
      </span>
      <ol className="flex min-w-max items-center gap-1.5">
        {combatants.map((combatant, index) => (
          <li key={combatant.id} className="flex items-center gap-1.5">
            {index > 0 && <span className="text-muted-foreground/50">›</span>}
            <span
              className={`rounded-full px-2 py-0.5 text-[10px] font-medium ${
                combatant.id === activeCombatantId
                  ? 'bg-amber-500/15 text-amber-600'
                  : combatant.isPlayer
                    ? 'bg-muted text-foreground'
                    : 'bg-destructive/10 text-destructive'
              }`}
            >
              {combatant.name}
            </span>
          </li>
        ))}
      </ol>
    </div>
  );
}

type CombatFeedbackPreview = 'hit' | 'crit' | 'miss' | 'block';
type CombatActionPreview = 'attacking' | 'defending';

function CombatantPreviewCard({
  combatant,
  isSelf = false,
  playerLevel,
  turnActive = false,
  actionState,
  feedback,
  loopFeedback = true,
}: {
  combatant: CombatantState;
  isSelf?: boolean;
  playerLevel: number;
  turnActive?: boolean;
  actionState?: CombatActionPreview;
  feedback?: CombatFeedbackPreview;
  loopFeedback?: boolean;
}) {
  const treatment = {
    hit: {
      frame: '',
      label: '−18',
      labelClass: 'text-yellow-400',
    },
    crit: {
      frame: 'scale-[1.02]',
      label: 'CRIT −24',
      labelClass: 'text-red-500',
    },
    miss: {
      frame: 'translate-x-1',
      label: 'MISS',
      labelClass: 'text-muted-foreground italic',
    },
    block: {
      frame: '',
      label: 'BLOCKED',
      labelClass: 'text-sky-500',
    },
  } as const;
  const current = feedback ? treatment[feedback] : undefined;

  return (
    <div className="relative h-[7.75rem] w-64 max-w-full">
      <div
        className={`h-full w-full overflow-hidden rounded-lg border-0 shadow-sm transition-transform [&>div]:h-full [&>div]:border-0 ${
          turnActive ? '' : actionState ? 'shadow-lg' : ''
        } ${current?.frame ?? ''}`}
        style={
          turnActive
            ? {
                boxShadow:
                  '0 0 0 1px rgba(245, 158, 11, 0.12), 0 0 16px 2px rgba(245, 158, 11, 0.16)',
              }
            : undefined
        }
      >
        <CombatantCard combatant={combatant} isSelf={isSelf} playerLevel={playerLevel} />
      </div>
      {current && (
        <motion.span
          animate={{
            opacity: [0, 1, 1, 0],
            scale: [1.5, 1.15, 0.85, 0.7],
            y: [8, 0, -24, -36],
          }}
          className={`pointer-events-none absolute top-1/2 left-1/2 z-10 -translate-x-1/2 font-black tracking-tight whitespace-nowrap [-webkit-text-stroke:1px_rgba(0,0,0,0.85)] [text-shadow:0_1px_0_rgba(0,0,0,0.65),0_2px_3px_rgba(0,0,0,0.4)] ${feedback === 'crit' ? 'text-2xl' : 'text-lg'} ${current.labelClass}`}
          transition={{
            duration: 1,
            ease: 'easeOut',
            repeat: loopFeedback ? Infinity : 0,
            repeatDelay: 1.8,
          }}
        >
          {current.label}
        </motion.span>
      )}
    </div>
  );
}

function SectionLabel({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <h2
      className={`text-muted-foreground text-[10px] font-semibold tracking-wide uppercase ${className}`}
    >
      {children}
    </h2>
  );
}

type EncounterMenu = 'attack' | 'defend' | 'item' | null;

function EncounterActionMenuMock({
  playerId,
  popoverContainer,
}: {
  playerId: string;
  popoverContainer: HTMLDivElement | null;
}) {
  const [openMenu, setOpenMenu] = useState<EncounterMenu>(null);

  return (
    <section className="w-full">
      <SectionLabel className="mb-2">Choose an action</SectionLabel>
      <div className="flex items-stretch justify-between gap-4">
        <div className="grid w-60 grid-cols-3 gap-1.5">
          <PickerPopover
            category="Offensive"
            onOpenChange={(open) => setOpenMenu(open ? 'attack' : null)}
            open={openMenu === 'attack'}
            playerId={playerId}
            popoverContainer={popoverContainer}
            trigger={<EncounterAction icon={Swords} label="Attack" />}
          />
          <PickerPopover
            category="Support"
            onOpenChange={(open) => setOpenMenu(open ? 'defend' : null)}
            open={openMenu === 'defend'}
            playerId={playerId}
            popoverContainer={popoverContainer}
            trigger={<EncounterAction icon={Shield} label="Defend" />}
          />
          <Popover
            open={openMenu === 'item'}
            onOpenChange={(open) => setOpenMenu(open ? 'item' : null)}
          >
            <PopoverTrigger asChild>
              <EncounterAction icon={FlaskConical} label="Item" />
            </PopoverTrigger>
            <PopoverContent
              align="start"
              className="data-closed:slide-out-to-bottom-4 data-closed:zoom-out-100 data-open:slide-in-from-bottom-4 data-open:zoom-in-100 z-[60] max-h-96 w-[min(24rem,calc(100vw-2rem))] overflow-y-auto p-0"
              container={popoverContainer}
              side="top"
            >
              <ItemPicker
                onBack={() => setOpenMenu(null)}
                onChoose={() => undefined}
                playerId={playerId}
                showHeader={false}
              />
            </PopoverContent>
          </Popover>
        </div>
        <EncounterAction className="w-20" destructive icon={DoorOpen} label="Flee" />
      </div>
    </section>
  );
}

function PickerPopover({
  category,
  onOpenChange,
  open,
  playerId,
  popoverContainer,
  trigger,
}: {
  category: AbilityCategory;
  onOpenChange: (open: boolean) => void;
  open: boolean;
  playerId: string;
  popoverContainer: HTMLDivElement | null;
  trigger: ReactNode;
}) {
  return (
    <Popover open={open} onOpenChange={onOpenChange}>
      <PopoverTrigger asChild>{trigger}</PopoverTrigger>
      <PopoverContent
        align="start"
        className="data-closed:slide-out-to-bottom-4 data-closed:zoom-out-100 data-open:slide-in-from-bottom-4 data-open:zoom-in-100 z-[60] max-h-96 w-[min(24rem,calc(100vw-2rem))] overflow-y-auto p-0"
        container={popoverContainer}
        side="top"
      >
        <AbilityPicker
          category={category}
          onBack={() => onOpenChange(false)}
          onChoose={() => undefined}
          playerId={playerId}
          showHeader={false}
        />
      </PopoverContent>
    </Popover>
  );
}

interface EncounterActionProps extends React.ComponentProps<'button'> {
  icon: LucideIcon;
  label: string;
  destructive?: boolean;
}

const EncounterAction = forwardRef<HTMLButtonElement, EncounterActionProps>(
  function EncounterAction({ icon: Icon, label, destructive = false, className, ...props }, ref) {
    return (
      <button
        ref={ref}
        type="button"
        {...props}
        className={`border-border bg-background hover:bg-accent flex flex-col items-center gap-1 rounded-md border py-2 text-xs font-medium shadow-sm ${
          destructive ? 'text-destructive' : ''
        } ${className ?? ''}`}
      >
        <Icon className="h-4 w-4" />
        {label}
      </button>
    );
  },
);
