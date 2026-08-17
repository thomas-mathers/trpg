import { Crown, Droplet, Heart, type LucideIcon, Skull, Zap } from 'lucide-react';
import { motion } from 'motion/react';

import type { CombatantState } from '@/api/signalr-client/TRPG.Combat.Responses';
import { EffectBadge } from '@/features/combat/components/effect-badge';
import type { CombatFlash } from '@/features/combat/hooks/use-combat';
import { useStatDelta } from '@/features/combat/hooks/use-stat-delta';
import { isDangerous } from '@/features/combat/threat-level';
import { cn } from '@/lib/utils';

interface CombatantCardProps {
  combatant: CombatantState;
  isSelf?: boolean;
  playerLevel: number;
  targetable?: boolean;
  onSelect?: () => void;
  flash?: CombatFlash;
  isActing?: boolean;
  className?: string;
}

export function CombatantCard({
  combatant,
  isSelf = false,
  playerLevel,
  targetable = false,
  onSelect,
  flash,
  isActing = false,
  className,
}: CombatantCardProps) {
  const hpDelta = useStatDelta(Number(combatant.currentHp));
  const apDelta = useStatDelta(Number(combatant.currentAp));
  const mpDelta = useStatDelta(Number(combatant.currentMp));
  const inferredHit = !flash && hpDelta !== null && hpDelta < 0;
  const isHit = flash?.kind === 'hit' || inferredHit;
  const isCrit = flash?.kind === 'crit';
  const isBlocked = flash?.kind === 'block';

  const danger = !isSelf && combatant.isAlive && isDangerous(Number(combatant.level), playerLevel);
  const canTarget = targetable && combatant.isAlive;

  const badges = [
    ...Object.entries(combatant.activeConditions)
      .filter(([, turns]) => Number(turns) > 0)
      .map(([type, turns]) => (
        <EffectBadge
          key={`condition-${type}`}
          kind="condition"
          type={type}
          remainingTurns={Number(turns)}
        />
      )),
    ...combatant.activeDots.map((dot, index) => (
      <EffectBadge key={`dot-${index}`} kind="dot" dot={dot} />
    )),
    ...combatant.activeHots.map((hot, index) => (
      <EffectBadge key={`hot-${index}`} kind="hot" hot={hot} />
    )),
    ...combatant.activeBuffs.map((buff, index) => (
      <EffectBadge key={`buff-${index}`} kind="buff" buff={buff} />
    )),
  ];

  return (
    <div
      role={canTarget ? 'button' : undefined}
      tabIndex={canTarget ? 0 : undefined}
      onClick={canTarget ? onSelect : undefined}
      onKeyDown={
        canTarget
          ? (event) => {
              if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                onSelect?.();
              }
            }
          : undefined
      }
      className={cn(
        'bg-card relative min-w-[156px] overflow-hidden rounded-lg p-2.5 text-left shadow-sm transition-[box-shadow,transform] duration-150',
        !combatant.isAlive && 'opacity-45',
        canTarget &&
          'cursor-crosshair outline-none focus-visible:ring-2 focus-visible:ring-foreground/50',
        isHit && 'combat-flash-hit',
        isCrit && 'combat-flash-crit',
        isBlocked && 'combat-dodge',
        isActing && 'shadow-lg',
        className,
      )}
    >
      {isActing && (
        <span
          className={
            isSelf
              ? 'combat-player-turn absolute inset-0 rounded-lg'
              : 'combat-windup absolute inset-0 rounded-lg'
          }
        />
      )}
      {flash && <FlashFloat flash={flash} />}

      <div className="mb-1.5 flex items-baseline justify-between gap-1.5">
        <span className="flex min-w-0 items-center gap-1">
          {danger && (
            <Crown className="h-3 w-3 shrink-0" aria-label="Much more powerful than you" />
          )}
          {!combatant.isAlive && (
            <Skull className="text-muted-foreground h-3.5 w-3.5 shrink-0" aria-label="Defeated" />
          )}
          <span className="flex min-w-0 items-center gap-1 text-[13px] font-semibold">
            <span className="truncate">
              {combatant.name}
              {isSelf && ' (you)'}
            </span>
          </span>
        </span>
        <span className="text-muted-foreground shrink-0 text-[11px]">Lv {combatant.level}</span>
      </div>

      <StatBar
        icon={Heart}
        colorClass="text-red-500"
        fillClass="bg-red-500"
        current={Number(combatant.currentHp)}
        max={Number(combatant.maximumHp)}
        delta={hpDelta}
        hideDelta={Boolean(flash)}
      />
      <StatBar
        icon={Zap}
        colorClass="text-amber-500"
        fillClass="bg-amber-500"
        current={Number(combatant.currentAp)}
        max={Number(combatant.maximumAp)}
        delta={apDelta}
      />
      <StatBar
        icon={Droplet}
        colorClass="text-blue-500"
        fillClass="bg-blue-500"
        current={Number(combatant.currentMp)}
        max={Number(combatant.maximumMp)}
        delta={mpDelta}
      />

      {badges.length > 0 && <div className="mt-1.5 flex flex-wrap gap-1">{badges}</div>}
    </div>
  );
}

interface StatBarProps {
  icon: LucideIcon;
  colorClass: string;
  fillClass: string;
  current: number;
  max: number;
  delta: number | null;
  hideDelta?: boolean;
}

function StatBar({
  icon: Icon,
  colorClass,
  fillClass,
  current,
  max,
  delta,
  hideDelta = false,
}: StatBarProps) {
  const pct = max > 0 ? Math.max(0, Math.min(100, Math.round((current / max) * 100))) : 0;

  return (
    <div className="relative mt-1 flex items-center gap-1.5 first:mt-0">
      <Icon className={cn('h-[11px] w-[11px] shrink-0', colorClass)} />
      <span className="bg-muted h-[5px] flex-1 overflow-hidden rounded-full">
        <span
          className={cn(
            'block h-full rounded-full transition-[width] duration-500 ease-out',
            fillClass,
          )}
          style={{ width: `${pct}%` }}
        />
      </span>
      <span className="text-muted-foreground w-10 shrink-0 text-right text-[10px] tabular-nums">
        {current}/{max}
      </span>
      {!hideDelta && delta !== null && delta !== 0 && (
        <span
          className={cn(
            'combat-float-up pointer-events-none absolute -top-0.5 right-0 text-[13px] font-bold tabular-nums [text-shadow:0_1px_2px_rgba(0,0,0,0.5)]',
            delta > 0 ? 'text-green-500' : colorClass,
          )}
        >
          {delta > 0 ? `+${delta}` : delta}
        </span>
      )}
    </div>
  );
}

function FlashFloat({ flash }: { flash: CombatFlash }) {
  const treatment = {
    hit: { label: `−${flash.damage}`, className: 'text-yellow-400 text-lg' },
    crit: { label: `CRIT −${flash.damage}`, className: 'text-red-500 text-2xl' },
    miss: { label: 'MISS', className: 'text-muted-foreground text-lg italic' },
    block: { label: 'BLOCKED', className: 'text-sky-500 text-lg' },
  } as const;
  const current = treatment[flash.kind];

  return (
    <motion.span
      key={flash.nonce}
      animate={{ opacity: [0, 1, 1, 0], scale: [1.5, 1.15, 0.85, 0.7], y: [8, 0, -24, -36] }}
      className={cn(
        'pointer-events-none absolute top-1/2 left-1/2 z-10 -translate-x-1/2 font-black tracking-tight whitespace-nowrap [-webkit-text-stroke:1px_rgba(0,0,0,0.85)] [text-shadow:0_1px_0_rgba(0,0,0,0.65),0_2px_3px_rgba(0,0,0,0.4)]',
        current.className,
      )}
      transition={{ duration: 1, ease: 'easeOut' }}
    >
      {current.label}
    </motion.span>
  );
}
