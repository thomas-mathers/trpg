import { DoorOpen, FlaskConical, Shield, Sword, Swords } from 'lucide-react';
import { useEffect, useState } from 'react';

import type { AbilityCategory, AbilitySummary, ConsumableSummary, FightState } from '@/api/client';
import { AbilityPicker } from '@/features/combat/components/ability-picker';
import { AnimatedHeight } from '@/components/animated-height';
import { CombatantCard } from '@/features/combat/components/combatant-card';
import { CombatOutcomeScreen } from '@/features/combat/components/combat-outcome-screen';
import { EnemyRow } from '@/features/combat/components/enemy-row';
import { ItemPicker } from '@/features/combat/components/item-picker';
import { PickerHeader } from '@/features/combat/components/picker-header';
import type { CombatCardEffect } from '@/features/combat/hooks/use-combat-state';
import type { UseAbilityAction, UseItemAction } from '@/features/combat/combat-action';
import type { CombatOutcome } from '@/features/combat/combat-outcome';

type Mode = 'topmenu' | 'ability' | 'target' | 'item';

interface CombatConsoleProps {
  playerId: string;
  fight: FightState;
  disabled: boolean;
  onUseAbility: (action: UseAbilityAction) => void;
  onUseItem: (action: UseItemAction) => void;
  onFlee: () => void;
  activeAttackerId?: string | null;
  cardEffects?: Record<string, CombatCardEffect>;
  outcome?: CombatOutcome | null;
  onDismissOutcome?: () => void;
  onExitToMenu?: () => void;
}

export function CombatConsole({
  playerId,
  fight,
  disabled,
  onUseAbility,
  onUseItem,
  onFlee,
  activeAttackerId = null,
  cardEffects = {},
  outcome = null,
  onDismissOutcome = () => {},
  onExitToMenu = () => {},
}: CombatConsoleProps) {
  const [mode, setMode] = useState<Mode>('topmenu');
  const [abilityCategory, setAbilityCategory] = useState<AbilityCategory | null>(null);
  const [pendingAbility, setPendingAbility] = useState<AbilitySummary | null>(null);

  const player = fight.combatants.find((c) => c.isPlayer);
  const enemies = fight.combatants.filter((c) => !c.isPlayer);
  const playerLevel = player ? Number(player.level) : 1;

  useEffect(() => {
    if (!disabled) {
      setMode('topmenu');
    }
  }, [disabled]);

  if (!player) {
    return null;
  }

  function chooseAbility(ability: AbilitySummary) {
    if (abilityCategory === 'Support') {
      onUseAbility({ type: 'UseAbilityAction', targetId: player!.id, abilityName: ability.name });
      return;
    }
    setPendingAbility(ability);
    setMode('target');
  }

  function chooseTarget(targetId: string) {
    if (!pendingAbility) {
      return;
    }
    onUseAbility({ type: 'UseAbilityAction', targetId, abilityName: pendingAbility.name });
  }

  function chooseItem(item: ConsumableSummary) {
    onUseItem({ type: 'UseItemAction', itemName: item.name });
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
        cardEffects={cardEffects}
      />
      <div className="px-2.5">
        <CombatantCard
          combatant={player}
          isSelf
          playerLevel={playerLevel}
          effect={cardEffects[player.id]}
          isActing={activeAttackerId === player.id}
        />
      </div>

      <AnimatedHeight>
        {outcome ? (
          <CombatOutcomeScreen
            outcome={outcome}
            onContinue={onDismissOutcome}
            onExitToMenu={onExitToMenu}
          />
        ) : disabled ? (
          <ResolvingIndicator />
        ) : mode === 'topmenu' ? (
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
            <ActionButton icon={DoorOpen} label="Flee" onClick={onFlee} destructive />
          </div>
        ) : mode === 'ability' && abilityCategory ? (
          <AbilityPicker
            playerId={playerId}
            category={abilityCategory}
            onBack={() => setMode('topmenu')}
            onChoose={chooseAbility}
          />
        ) : mode === 'target' ? (
          <div className="p-2.5">
            <PickerHeader
              onBack={() => setMode('ability')}
              title={`${pendingAbility?.name ?? ''} — tap a target`}
            />
            <p className="text-muted-foreground pb-2 text-center text-xs">
              Choose an enemy card above to strike.
            </p>
          </div>
        ) : (
          <ItemPicker playerId={playerId} onBack={() => setMode('topmenu')} onChoose={chooseItem} />
        )}
      </AnimatedHeight>
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
