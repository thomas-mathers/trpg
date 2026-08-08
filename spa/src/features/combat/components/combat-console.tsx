import { DoorOpen, FlaskConical, Shield, Sparkles, Sword, Swords } from 'lucide-react';
import { AnimatePresence, motion } from 'motion/react';
import { forwardRef, useEffect, useRef, useState, type ReactNode } from 'react';
import { toast } from 'sonner';

import type {
  AbilityCategory,
  AbilitySummary,
  CombatantState,
  ConsumableSummary,
} from '@/api/client';
import { Dialog, DialogContent, DialogTitle } from '@/components/ui/dialog';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { TooltipProvider } from '@/components/ui/tooltip';
import {
  formatCombatAction,
  type UseAbilityAction,
  type UseItemAction,
} from '@/features/combat/combat-action';
import { AbilityPicker } from '@/features/combat/components/ability-picker';
import { CombatantCard } from '@/features/combat/components/combatant-card';
import { ItemPicker } from '@/features/combat/components/item-picker';
import { PickerHeader } from '@/features/combat/components/picker-header';
import { usePlayerId, useScene } from '@/features/game/contexts/scene-context';
import { useGameActions } from '@/features/game/game-chat-context';
import { formatLocation } from '@/features/game/scene-format';
import { gameEventBus } from '@/lib/game-event-bus';

import { GameToast } from '../../game/components/game-toast';
import { useCombatState, type CombatFlash } from '../hooks/use-combat-state';

type Mode = 'topmenu' | 'target';

interface EnemyRowProps {
  enemies: CombatantState[];
  playerLevel: number;
  targetable?: boolean;
  onSelectTarget?: (id: string) => void;
  activeAttackerId?: string | null;
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
                  '0 0 0 1px rgba(245, 158, 11, 0.12), 0 0 16px 2px rgba(245, 158, 11, 0.16)',
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
            activeAttackerId === enemy.id
              ? -14
              : activeDefenderId === enemy.id && combatFlashes[enemy.id]?.kind !== 'block'
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

export function CombatConsole() {
  const {
    fight,
    activeAttackerId,
    activeDefenderId,
    activeCombatEvent,
    combatFlashes,
    isPlayingBack,
  } = useCombatState();
  const { isStreaming, submitCombatAction, submitFlee } = useGameActions();
  const playerId = usePlayerId();
  const scene = useScene();
  const [mode, setMode] = useState<Mode>('topmenu');
  const [pendingAbility, setPendingAbility] = useState<AbilitySummary | null>(null);
  const [openMenu, setOpenMenu] = useState<'attack' | 'defend' | 'item' | null>(null);
  const [popoverContainer, setPopoverContainer] = useState<HTMLDivElement | null>(null);
  const [narration] = useState<{
    id: number;
    text: string;
    actor: string;
    ability: string;
  } | null>(null);
  const [pendingNarrations, setPendingNarrations] = useState<string[]>([]);
  const [isNarrationVisible, setIsNarrationVisible] = useState(false);
  const [isSubmittingAction, setIsSubmittingAction] = useState(false);

  const disabled = isPlayingBack || isStreaming || isSubmittingAction;

  useEffect(() => {
    if (!disabled) {
      setMode('topmenu');
    }
  }, [disabled]);

  useEffect(() => {
    if (isStreaming) {
      return;
    }
    setIsSubmittingAction(false);
  }, [isStreaming]);

  const wasInCombatRef = useRef(false);

  useEffect(() => {
    const isInCombat = fight !== null;
    if (isInCombat && !wasInCombatRef.current) {
      setMode('topmenu');
      setPendingAbility(null);
      setOpenMenu(null);
    }
    wasInCombatRef.current = isInCombat;
  }, [fight]);

  useEffect(() => {
    const unsubscribe = gameEventBus.on('CombatNarrations', (narrations) => {
      setPendingNarrations((current) => [...current, ...narrations]);
    });
    return () => {
      unsubscribe();
    };
  }, []);

  useEffect(() => {
    if (!activeDefenderId || isNarrationVisible || pendingNarrations.length === 0) {
      return;
    }

    const text = pendingNarrations[0];
    const show = setTimeout(() => {
      toast.custom(
        (toastId) => (
          <GameToast
            toastId={toastId}
            icon={Sparkles}
            title={`${activeCombatEvent?.attackerName ?? 'Combatant'} · ${activeCombatEvent?.abilityName ?? 'Ability'}`}
            description={text}
          />
        ),
        { duration: 1200 },
      );
      setIsNarrationVisible(true);
      setPendingNarrations((current) => current.slice(1));
    }, 180);
    return () => {
      clearTimeout(show);
    };
  }, [activeCombatEvent, activeDefenderId, isNarrationVisible, pendingNarrations]);

  useEffect(() => {
    if (!isNarrationVisible) {
      return;
    }

    const hide = setTimeout(() => setIsNarrationVisible(false), 1200);
    return () => clearTimeout(hide);
  }, [isNarrationVisible]);

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

  const location = scene ? formatLocation(scene) : '';
  const encounterTitle = location ? `Ambush at ${location}` : 'Combat';

  function chooseTarget(targetId: string) {
    if (!pendingAbility) return;
    const action: UseAbilityAction = {
      type: 'UseAbilityAction',
      targetId,
      abilityName: pendingAbility.name,
    };
    setIsSubmittingAction(true);
    submitCombatAction(action, formatCombatAction(action, fight));
    setPendingAbility(null);
  }

  function chooseSupportAbility(ability: AbilitySummary) {
    const action: UseAbilityAction = {
      type: 'UseAbilityAction',
      targetId: player!.id,
      abilityName: ability.name,
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
          <section className="flex h-[97px] w-full items-center p-3">
            <div className="flex w-full items-stretch justify-between gap-4">
              <div className="grid w-60 grid-cols-3 gap-1.5">
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
                  trigger={<ActionButton icon={Sword} label="Attack" />}
                />
                <ActionPickerPopover
                  category="Support"
                  container={popoverContainer}
                  onChoose={chooseSupportAbility}
                  onOpenChange={(open) => setOpenMenu(open ? 'defend' : null)}
                  open={openMenu === 'defend'}
                  playerId={currentPlayerId}
                  trigger={<ActionButton icon={Shield} label="Defend" />}
                />
                <Popover
                  open={openMenu === 'item'}
                  onOpenChange={(open) => setOpenMenu(open ? 'item' : null)}
                >
                  <PopoverTrigger asChild>
                    <ActionButton icon={FlaskConical} label="Item" />
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
                icon={DoorOpen}
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
          className="h-[min(100dvh-2rem,56rem)] w-[min(100vw-2rem,72rem)] max-w-[calc(100%-2rem)] gap-0 overflow-hidden p-0 shadow-2xl sm:max-w-[72rem]"
        >
          <DialogTitle className="sr-only">{encounterTitle}</DialogTitle>
          <div ref={setPopoverContainer} className="bg-muted flex h-full min-h-0 flex-col">
            <header className="bg-card flex items-center justify-between border-b px-5 py-3">
              <span className="flex items-center gap-2 text-sm font-semibold">
                <Swords className="h-4 w-4 text-amber-500" />
                {encounterTitle}
              </span>
            </header>

            <InitiativeTrack combatants={fight.combatants} activeAttackerId={activeAttackerId} />

            <div className="relative min-h-0 flex-1 overflow-y-auto p-4 sm:p-5 md:px-10">
              <AnimatePresence>
                {narration && (
                  <motion.div
                    key={narration.id}
                    initial={{ opacity: 0, scale: 0.82, y: 18 }}
                    animate={{ opacity: 1, scale: 1, y: 0 }}
                    exit={{ opacity: 0, scale: 0.88, y: -30 }}
                    transition={{ duration: 0.42, ease: [0.42, 0, 0.58, 1] }}
                    className="pointer-events-none absolute top-8 left-1/2 z-20 w-[calc(100%-2rem)] max-w-md -translate-x-1/2"
                  >
                    <div className="border-border/80 bg-popover/95 shadow-foreground/10 rounded-lg border px-3 py-2.5 shadow-lg backdrop-blur-sm">
                      <div className="text-muted-foreground mb-1 flex items-center gap-1.5 text-[10px] font-semibold tracking-wide uppercase">
                        <Sparkles className="h-3.5 w-3.5 text-amber-500" />
                        <span>{narration.actor}</span>
                        <span className="text-muted-foreground/60">•</span>
                        <span>{narration.ability}</span>
                      </div>
                      <p className="text-sm leading-snug">{narration.text}</p>
                    </div>
                  </motion.div>
                )}
              </AnimatePresence>
              <div className="grid min-h-full grid-cols-1 gap-5 pt-14 md:grid-cols-[16rem_16rem] md:justify-between md:pt-12">
                <section className="flex flex-col justify-center">
                  <CombatantFrame
                    active={activeAttackerId === player.id}
                    offset={
                      activeAttackerId === player.id
                        ? 14
                        : activeDefenderId === player.id &&
                            combatFlashes[player.id]?.kind !== 'block'
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
                  key={disabled ? 'resolving' : mode}
                  initial={{ opacity: 0, y: 6 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, y: -4 }}
                  transition={{ duration: 0.36, ease: 'easeOut' }}
                >
                  {disabled ? <ResolvingIndicator /> : renderMode()}
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
                      ? 'bg-amber-500/15 text-amber-600'
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

function ResolvingIndicator() {
  return (
    <div className="text-muted-foreground flex items-center justify-center gap-2 px-2.5 py-6 text-sm">
      Resolving round
      <span className="flex gap-1">
        {[0, 0.12, 0.24].map((delay) => (
          <motion.span
            key={delay}
            animate={{ opacity: [0.45, 1, 0.45], scale: [1, 1.4, 1], y: [0, -6, 0] }}
            className="h-1.5 w-1.5 rounded-full bg-current"
            transition={{ duration: 0.72, delay, ease: 'easeInOut', repeat: Infinity }}
          />
        ))}
      </span>
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
  icon: typeof Sword;
  label: string;
  onClick?: () => void;
  destructive?: boolean;
  className?: string;
}

const ActionButton = forwardRef<HTMLButtonElement, ActionButtonProps>(function ActionButton(
  { icon: Icon, label, onClick, destructive = false, className },
  ref,
) {
  return (
    <button
      ref={ref}
      type="button"
      onClick={onClick}
      className={`border-border bg-card hover:bg-accent flex flex-col items-center gap-1 rounded-md border py-2 text-xs font-medium shadow-sm ${
        destructive ? 'text-destructive' : ''
      } ${className ?? ''}`}
    >
      <Icon className="h-[17px] w-[17px]" />
      <span>{label}</span>
    </button>
  );
});
