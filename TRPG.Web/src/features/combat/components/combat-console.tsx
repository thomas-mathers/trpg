import { DoorOpen, FlaskConical, Shield, Sword, Swords } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';

import type {
  AbilityCategory,
  AbilitySummary,
  CombatantState,
  ConsumableSummary,
} from '@/api/client';
import {
  formatCombatAction,
  type UseAbilityAction,
  type UseItemAction,
} from '@/features/combat/combat-action';
import { AbilityPicker } from '@/features/combat/components/ability-picker';
import { AnimatedHeight } from '@/features/combat/components/animated-height';
import { CombatantCard } from '@/features/combat/components/combatant-card';
import { ItemPicker } from '@/features/combat/components/item-picker';
import { PickerHeader } from '@/features/combat/components/picker-header';
import { usePlayerId } from '@/features/game/contexts/scene-context';
import { useGameActions } from '@/features/game/game-chat-context';

import { useCombatState, type CombatFlash } from '../hooks/use-combat-state';

type Mode = 'topmenu' | 'ability' | 'target' | 'item';

interface EnemyRowProps {
  enemies: CombatantState[];
  playerLevel: number;
  targetable?: boolean;
  onSelectTarget?: (id: string) => void;
  activeAttackerId?: string | null;
  combatFlashes?: Record<string, CombatFlash>;
}

function EnemyRow({
  enemies,
  playerLevel,
  targetable = false,
  onSelectTarget,
  activeAttackerId = null,
  combatFlashes = {},
}: EnemyRowProps) {
  return (
    <div className="flex max-h-52 flex-wrap gap-2 overflow-y-auto p-2.5 pb-1">
      {enemies.map((enemy) => (
        <CombatantCard
          key={enemy.id}
          combatant={enemy}
          playerLevel={playerLevel}
          targetable={targetable}
          onSelect={() => onSelectTarget?.(enemy.id)}
          flash={combatFlashes[enemy.id]}
          isActing={activeAttackerId === enemy.id}
        />
      ))}
    </div>
  );
}

export function CombatConsole() {
  const { fight, activeAttackerId, combatFlashes, isPlayingBack } = useCombatState();
  const { isStreaming, submitCombatAction, submitFlee } = useGameActions();
  const playerId = usePlayerId();
  const [mode, setMode] = useState<Mode>('topmenu');
  const [abilityCategory, setAbilityCategory] = useState<AbilityCategory | null>(null);
  const [pendingAbility, setPendingAbility] = useState<AbilitySummary | null>(null);

  const disabled = isPlayingBack || isStreaming;

  useEffect(() => {
    if (!disabled) {
      setMode('topmenu');
    }
  }, [disabled]);

  const wasInCombatRef = useRef(false);

  useEffect(() => {
    const isInCombat = fight !== null;
    if (isInCombat && !wasInCombatRef.current) {
      setMode('topmenu');
      setAbilityCategory(null);
      setPendingAbility(null);
    }
    wasInCombatRef.current = isInCombat;
  }, [fight]);

  if (!fight || !playerId) {
    return null;
  }

  const currentPlayerId = playerId;
  const player = fight.combatants.find((c) => c.isPlayer);
  const enemies = fight.combatants.filter((c) => !c.isPlayer);
  const playerLevel = player ? Number(player.level) : 1;

  if (!player) {
    return null;
  }

  function chooseAbility(ability: AbilitySummary) {
    if (abilityCategory === 'Support') {
      const action: UseAbilityAction = {
        type: 'UseAbilityAction',
        targetId: player!.id,
        abilityName: ability.name,
      };
      submitCombatAction(action, formatCombatAction(action, fight));
      return;
    }
    setPendingAbility(ability);
    setMode('target');
  }

  function chooseTarget(targetId: string) {
    if (!pendingAbility) {
      return;
    }
    const action: UseAbilityAction = {
      type: 'UseAbilityAction',
      targetId,
      abilityName: pendingAbility.name,
    };
    submitCombatAction(action, formatCombatAction(action, fight));
  }

  function chooseItem(item: ConsumableSummary) {
    const action: UseItemAction = { type: 'UseItemAction', itemName: item.name };
    submitCombatAction(action, formatCombatAction(action, fight));
  }

  function renderMode() {
    switch (mode) {
      case 'topmenu':
        return (
          <div className="grid grid-cols-4 gap-1.5 p-2.5">
            <ActionButton
              icon={Sword}
              label="Attack"
              onClick={() => {
                setAbilityCategory('Offensive');
                setMode('ability');
              }}
            />
            <ActionButton
              icon={Shield}
              label="Defend"
              onClick={() => {
                setAbilityCategory('Support');
                setMode('ability');
              }}
            />
            <ActionButton icon={FlaskConical} label="Item" onClick={() => setMode('item')} />
            <ActionButton icon={DoorOpen} label="Flee" onClick={submitFlee} destructive />
          </div>
        );
      case 'ability':
        return abilityCategory ? (
          <AbilityPicker
            playerId={currentPlayerId}
            category={abilityCategory}
            onBack={() => setMode('topmenu')}
            onChoose={chooseAbility}
          />
        ) : null;
      case 'target':
        return (
          <div className="p-2.5">
            <PickerHeader
              onBack={() => setMode('ability')}
              title={`${pendingAbility?.name ?? ''} — tap a target`}
            />
            <p className="text-muted-foreground pb-2 text-center text-xs">
              Choose an enemy card above to strike.
            </p>
          </div>
        );
      case 'item':
        return (
          <ItemPicker
            playerId={currentPlayerId}
            onBack={() => setMode('topmenu')}
            onChoose={chooseItem}
          />
        );
    }
  }

  return (
    <div className="bg-muted overflow-hidden rounded-lg border shadow-md">
      <div className="border-border bg-card flex items-center border-b px-3 py-2">
        <span className="text-muted-foreground flex items-center gap-1.5 text-xs font-semibold tracking-wide uppercase">
          <Swords className="h-3.5 w-3.5" />
          Combat
        </span>
      </div>

      <EnemyRow
        enemies={enemies}
        playerLevel={playerLevel}
        targetable={mode === 'target'}
        onSelectTarget={chooseTarget}
        activeAttackerId={activeAttackerId}
        combatFlashes={combatFlashes}
      />
      <div className="px-2.5">
        <CombatantCard
          combatant={player}
          isSelf
          playerLevel={playerLevel}
          flash={combatFlashes[player.id]}
          isActing={activeAttackerId === player.id}
        />
      </div>

      <AnimatedHeight>{disabled ? <ResolvingIndicator /> : renderMode()}</AnimatedHeight>
    </div>
  );
}

function ResolvingIndicator() {
  return (
    <div className="text-muted-foreground flex items-center justify-center gap-2 px-2.5 py-6 text-sm">
      Resolving action
      <span className="flex gap-1">
        <span className="h-1 w-1 animate-bounce rounded-full bg-current [animation-delay:-0.3s]" />
        <span className="h-1 w-1 animate-bounce rounded-full bg-current [animation-delay:-0.15s]" />
        <span className="h-1 w-1 animate-bounce rounded-full bg-current" />
      </span>
    </div>
  );
}

interface ActionButtonProps {
  icon: typeof Sword;
  label: string;
  onClick: () => void;
  destructive?: boolean;
}

function ActionButton({ icon: Icon, label, onClick, destructive = false }: ActionButtonProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`border-border bg-card hover:bg-accent flex flex-col items-center gap-1 rounded-md border py-2.5 text-xs font-medium shadow-sm ${
        destructive ? 'text-destructive' : ''
      }`}
    >
      <Icon className="h-[17px] w-[17px]" />
      <span>{label}</span>
    </button>
  );
}
