import { AnimatePresence, motion } from 'motion/react';
import { forwardRef, useEffect, useLayoutEffect, useRef, useState, type ReactNode } from 'react';
import type { IconType } from 'react-icons';
import {
  GiBubblingFlask,
  GiCrossedSwords,
  GiShield,
  GiSparkles,
  GiSprint,
  GiSwordBrandish,
} from 'react-icons/gi';
import { toast } from 'sonner';

import type { AbilityCategory, AbilitySummary, ConsumableSummary } from '@/api/client';
import type { CombatantState } from '@/api/signalr-client/TRPG.Combat.Responses';
import { Dialog, DialogContent, DialogTitle } from '@/components/ui/dialog';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { TooltipProvider } from '@/components/ui/tooltip';
import { AbilityPicker } from '@/features/combat/components/ability-picker';
import { CombatantCard } from '@/features/combat/components/combatant-card';
import { ItemPicker } from '@/features/combat/components/item-picker';
import { PickerHeader } from '@/features/combat/components/picker-header';
import { usePlayerId, useScene } from '@/features/game/contexts/scene-context';
import { formatLocation } from '@/features/game/scene-format';

import { GameToast } from '../../game/components/game-toast';
import { useCombat, type CombatFlash } from '../hooks/use-combat';

type Mode = 'topmenu' | 'target';

interface EnemyRowProps {
  enemies: CombatantState[];
  playerLevel: number;
  targetable?: boolean;
  onSelectTarget?: (id: string) => void;
  activeAttackerId?: string | null;
  activeActionIsAttack?: boolean;
  activeDefenderId?: string | null;
  combatFlashes?: Record<string, CombatFlash>;
}

function CombatantFrame({
  active,
  children,
  offset = 0,
  targetable = false,
}: {
  active: boolean;
  children: ReactNode;
  offset?: number;
  targetable?: boolean;
}) {
  return (
    <motion.div
      animate={{ x: offset }}
      className="group relative h-[7.75rem] w-64 max-w-full"
      transition={{ duration: 0.18, ease: [0.22, 1, 0.36, 1] }}
    >
      <div
        className="h-full w-full overflow-hidden rounded-lg border-0 shadow-sm [&>div]:h-full [&>div]:border-0"
        style={
          active
            ? {
                boxShadow:
                  '0 0 0 1px color-mix(in oklch, var(--stamina) 12%, transparent), 0 0 16px 2px color-mix(in oklch, var(--stamina) 16%, transparent)',
              }
            : undefined
        }
      >
        {children}
      </div>
      {targetable && <TargetBrackets />}
    </motion.div>
  );
}

function TargetBrackets() {
  return (
    <span className="pointer-events-none absolute -inset-2 z-20 opacity-0 transition-opacity duration-150 group-focus-within:opacity-100 group-hover:opacity-100">
      <span className="border-foreground/85 absolute top-0 left-0 h-3 w-3 border-t-2 border-l-2" />
      <span className="border-foreground/85 absolute top-0 right-0 h-3 w-3 border-t-2 border-r-2" />
      <span className="border-foreground/85 absolute bottom-0 left-0 h-3 w-3 border-b-2 border-l-2" />
      <span className="border-foreground/85 absolute right-0 bottom-0 h-3 w-3 border-r-2 border-b-2" />
    </span>
  );
}

function EnemyRow({
  enemies,
  playerLevel,
  targetable = false,
  onSelectTarget,
  activeAttackerId = null,
  activeActionIsAttack = false,
  activeDefenderId = null,
  combatFlashes = {},
}: EnemyRowProps) {
  return (
    <div className="grid gap-4">
      {enemies.map((enemy) => (
        <CombatantFrame
          key={enemy.id}
          active={activeAttackerId === enemy.id}
          targetable={targetable}
          offset={
            activeAttackerId === enemy.id && activeActionIsAttack
              ? -14
              : activeDefenderId === enemy.id &&
                  (combatFlashes[enemy.id]?.kind === 'hit' ||
                    combatFlashes[enemy.id]?.kind === 'crit')
                ? 14
                : 0
          }
        >
          <CombatantCard
            combatant={enemy}
            playerLevel={playerLevel}
            targetable={targetable}
            onSelect={() => onSelectTarget?.(enemy.id)}
            flash={combatFlashes[enemy.id]}
            isActing={false}
            className="h-full w-full flex-none border-0"
          />
        </CombatantFrame>
      ))}
    </div>
  );
}

export function CombatDialog() {
  const {
    fight,
    activeAttackerId,
    activeActionIsAttack,
    activeDefenderId,
    activeCombatEvent,
    combatFlashes,
    isRevealed,
    disabled,
    submitUseAbilityCombatAction,
    submitUseItemCombatAction,
    submitFlee,
  } = useCombat();
  const playerId = usePlayerId();
  const scene = useScene();
  const [mode, setMode] = useState<Mode>('topmenu');
  const [pendingAbility, setPendingAbility] = useState<AbilitySummary | null>(null);
  const [openMenu, setOpenMenu] = useState<'attack' | 'defend' | 'item' | null>(null);
  const [popoverContainer, setPopoverContainer] = useState<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!disabled) {
      setMode('topmenu');
    }
  }, [disabled]);

  const actionMenuRef = useRef<HTMLDivElement>(null);
  const shouldFocusInitialAttackRef = useRef(false);
  const wasInCombatRef = useRef(false);

  useLayoutEffect(() => {
    const isInCombat = fight !== null;
    if (isInCombat && !wasInCombatRef.current) {
      setMode('topmenu');
      setPendingAbility(null);
      setOpenMenu(null);
      shouldFocusInitialAttackRef.current = true;
    }
    if (!isInCombat) {
      shouldFocusInitialAttackRef.current = false;
    }
    wasInCombatRef.current = isInCombat;
  }, [fight]);

  useLayoutEffect(() => {
    if (fight && !disabled && shouldFocusInitialAttackRef.current) {
      actionMenuRef.current
        ?.querySelector<HTMLButtonElement>('[data-combat-action="attack"]')
        ?.focus();
      shouldFocusInitialAttackRef.current = false;
    }
  }, [disabled, fight]);

  useEffect(() => {
    const narration = activeCombatEvent?.narration;
    if (!narration || !activeCombatEvent) {
      return;
    }

    toast.custom(
      (toastId) => (
        <GameToast
          toastId={toastId}
          icon={GiSparkles}
          title={`${activeCombatEvent.attackerName} · ${activeCombatEvent.abilityName}`}
          description={narration}
        />
      ),
      { duration: 1200 },
    );
  }, [activeCombatEvent]);

  if (!fight || !playerId || !isRevealed) {
    return null;
  }

  const currentPlayerId = playerId;
  const player = fight.find((c) => c.isPlayer);
  const enemies = fight.filter((c) => !c.isPlayer);
  const playerLevel = player ? Number(player.level) : 1;

  if (!player) {
    return null;
  }

  const location = scene ? formatLocation(scene) : '';
  const encounterTitle = location ? `Ambush at ${location}` : 'Combat';

  function chooseTarget(targetId: string) {
    if (!pendingAbility) return;
    submitUseAbilityCombatAction(targetId, pendingAbility.name);
    setPendingAbility(null);
  }

  function chooseSupportAbility(ability: AbilitySummary) {
    submitUseAbilityCombatAction(player!.id, ability.name);
  }

  function chooseItem(item: ConsumableSummary) {
    submitUseItemCombatAction(item.name);
  }

  function renderMode() {
    switch (mode) {
      case 'topmenu':
        return (
          <section className="flex h-[97px] w-full items-center p-3">
            <div className="flex w-full items-stretch justify-between gap-4">
              <div ref={actionMenuRef} className="grid w-60 grid-cols-3 gap-1.5">
                <ActionPickerPopover
                  category="Offensive"
                  container={popoverContainer}
                  onChoose={(ability) => {
                    setPendingAbility(ability);
                    setOpenMenu(null);
                  }}
                  onOpenChange={(open) => setOpenMenu(open ? 'attack' : null)}
                  open={openMenu === 'attack'}
                  playerId={currentPlayerId}
                  trigger={
                    <ActionButton
                      combatAction="attack"
                      disabled={disabled}
                      icon={GiSwordBrandish}
                      label="Attack"
                    />
                  }
                />
                <ActionPickerPopover
                  category="Support"
                  container={popoverContainer}
                  onChoose={chooseSupportAbility}
                  onOpenChange={(open) => setOpenMenu(open ? 'defend' : null)}
                  open={openMenu === 'defend'}
                  playerId={currentPlayerId}
                  trigger={<ActionButton disabled={disabled} icon={GiShield} label="Defend" />}
                />
                <Popover
                  open={openMenu === 'item'}
                  onOpenChange={(open) => setOpenMenu(open ? 'item' : null)}
                >
                  <PopoverTrigger asChild>
                    <ActionButton disabled={disabled} icon={GiBubblingFlask} label="Item" />
                  </PopoverTrigger>
                  <PopoverContent
                    align="start"
                    className="data-closed:slide-out-to-bottom-4 data-closed:zoom-out-100 data-open:slide-in-from-bottom-4 data-open:zoom-in-100 z-[60] max-h-96 w-[min(24rem,calc(100vw-2rem))] overflow-y-auto p-0"
                    container={popoverContainer}
                    side="top"
                  >
                    <ItemPicker
                      playerId={currentPlayerId}
                      onBack={() => setOpenMenu(null)}
                      onChoose={chooseItem}
                      showHeader={false}
                    />
                  </PopoverContent>
                </Popover>
              </div>
              <ActionButton
                className="w-20"
                disabled={disabled}
                icon={GiSprint}
                label="Flee"
                onClick={submitFlee}
                destructive
              />
            </div>
          </section>
        );
      case 'target':
        return (
          <section className="flex h-[97px] items-center p-3">
            <PickerHeader
              className="h-8"
              onBack={() => setMode('topmenu')}
              title={`${pendingAbility?.name ?? 'Ability'} — choose a target`}
            />
          </section>
        );
    }
  }

  return (
    <TooltipProvider>
      <Dialog open onOpenChange={() => undefined}>
        <DialogContent
          showCloseButton={false}
          onOpenAutoFocus={(event) => {
            event.preventDefault();
            const dialog = event.currentTarget as HTMLElement | null;
            dialog?.querySelector<HTMLButtonElement>('[data-combat-action="attack"]')?.focus();
          }}
          className="h-[min(100dvh-2rem,56rem)] w-[min(100vw-2rem,72rem)] max-w-[calc(100%-2rem)] gap-0 overflow-hidden p-0 shadow-2xl sm:max-w-[72rem]"
        >
          <DialogTitle className="sr-only">{encounterTitle}</DialogTitle>
          <div ref={setPopoverContainer} className="bg-muted flex h-full min-h-0 flex-col">
            <header className="chrome-surface text-chrome-foreground chrome-scope flex items-center justify-between px-5 py-3">
              <span className="flex items-center gap-2 text-sm font-semibold">
                <GiCrossedSwords className="text-stamina h-4 w-4" />
                {encounterTitle}
              </span>
            </header>

            <InitiativeTrack combatants={fight} activeAttackerId={activeAttackerId} />

            <div className="relative min-h-0 flex-1 overflow-y-auto p-4 sm:p-5 md:px-10">
              <div className="grid min-h-full grid-cols-1 gap-5 pt-14 md:grid-cols-[16rem_16rem] md:justify-between md:pt-12">
                <section className="flex flex-col justify-center">
                  <CombatantFrame
                    active={activeAttackerId === player.id}
                    offset={
                      activeAttackerId === player.id && activeActionIsAttack
                        ? 14
                        : activeDefenderId === player.id &&
                            (combatFlashes[player.id]?.kind === 'hit' ||
                              combatFlashes[player.id]?.kind === 'crit')
                          ? -14
                          : 0
                    }
                  >
                    <CombatantCard
                      combatant={player}
                      isSelf
                      playerLevel={playerLevel}
                      flash={combatFlashes[player.id]}
                      isActing={false}
                      className="h-full w-full flex-none border-0"
                    />
                  </CombatantFrame>
                </section>
                <section className="flex flex-col justify-center">
                  <EnemyRow
                    enemies={enemies}
                    playerLevel={playerLevel}
                    targetable={pendingAbility !== null}
                    onSelectTarget={chooseTarget}
                    activeAttackerId={activeAttackerId}
                    activeActionIsAttack={activeActionIsAttack}
                    activeDefenderId={activeDefenderId}
                    combatFlashes={combatFlashes}
                  />
                </section>
              </div>
            </div>

            <motion.div
              layout="size"
              className="bg-card relative shrink-0 origin-top overflow-hidden border-t"
              transition={{ duration: 0.55, ease: [0.22, 1, 0.36, 1] }}
            >
              <AnimatePresence initial={false} mode="popLayout">
                <motion.div
                  key={mode}
                  initial={{ opacity: 0, y: 6 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, y: -4 }}
                  transition={{ duration: 0.36, ease: 'easeOut' }}
                >
                  {renderMode()}
                </motion.div>
              </AnimatePresence>
            </motion.div>
          </div>
        </DialogContent>
      </Dialog>
    </TooltipProvider>
  );
}

interface InitiativeTrackProps {
  combatants: CombatantState[];
  activeAttackerId: string | null;
}

function InitiativeTrack({ combatants, activeAttackerId }: InitiativeTrackProps) {
  return (
    <div className="border-border bg-card flex items-center gap-2 overflow-x-auto border-b px-5 py-2">
      <span className="text-muted-foreground shrink-0 text-[10px] font-semibold tracking-wide uppercase">
        Initiative
      </span>
      <ol className="flex min-w-max items-center gap-1.5">
        {combatants
          .filter((combatant) => combatant.isAlive)
          .map((combatant, index) => {
            const isActive = combatant.id === activeAttackerId;
            return (
              <li key={combatant.id} className="flex items-center gap-1.5">
                {index > 0 && <span className="text-muted-foreground/50 text-xs">›</span>}
                <span
                  className={`rounded-full px-2 py-0.5 text-[10px] font-medium transition-colors ${
                    isActive
                      ? 'bg-stamina/15 text-stamina'
                      : combatant.isPlayer
                        ? 'bg-muted text-foreground'
                        : 'bg-destructive/10 text-destructive'
                  }`}
                >
                  {combatant.name}
                </span>
              </li>
            );
          })}
      </ol>
    </div>
  );
}

function ActionPickerPopover({
  category,
  container,
  onChoose,
  onOpenChange,
  open,
  playerId,
  trigger,
}: {
  category: AbilityCategory;
  container: HTMLDivElement | null;
  onChoose: (ability: AbilitySummary) => void;
  onOpenChange: (open: boolean) => void;
  open: boolean;
  playerId: string;
  trigger: React.ReactNode;
}) {
  return (
    <Popover open={open} onOpenChange={onOpenChange}>
      <PopoverTrigger asChild>{trigger}</PopoverTrigger>
      <PopoverContent
        align="start"
        className="data-closed:slide-out-to-bottom-4 data-closed:zoom-out-100 data-open:slide-in-from-bottom-4 data-open:zoom-in-100 z-[60] max-h-96 w-[min(24rem,calc(100vw-2rem))] overflow-y-auto p-0"
        container={container}
        side="top"
      >
        <AbilityPicker
          category={category}
          onBack={() => onOpenChange(false)}
          onChoose={onChoose}
          playerId={playerId}
          showHeader={false}
        />
      </PopoverContent>
    </Popover>
  );
}

interface ActionButtonProps {
  icon: IconType;
  label: string;
  onClick?: () => void;
  combatAction?: string;
  disabled?: boolean;
  destructive?: boolean;
  className?: string;
}

const ActionButton = forwardRef<HTMLButtonElement, ActionButtonProps>(function ActionButton(
  { icon: Icon, label, onClick, combatAction, disabled = false, destructive = false, className },
  ref,
) {
  return (
    <button
      ref={ref}
      type="button"
      onClick={onClick}
      disabled={disabled}
      data-combat-action={combatAction}
      className={`border-border bg-card hover:bg-accent flex flex-col items-center gap-1 rounded-md border py-2 text-xs font-medium shadow-sm ${
        destructive ? 'text-destructive' : ''
      } ${className ?? ''}`}
    >
      <Icon className="h-[17px] w-[17px]" />
      <span>{label}</span>
    </button>
  );
});
