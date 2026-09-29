import type {
  ActiveBuff,
  ActiveConditions,
  ActiveDot,
  ActiveHot,
} from '@/api/signalr-client/TRPG.Combat.Responses';
import { EffectBadge } from '@/features/combat/components/effect-badge';
import { cn } from '@/lib/utils';

interface EffectBadgesProps {
  activeConditions: ActiveConditions;
  activeDots: ActiveDot[];
  activeHots: ActiveHot[];
  activeBuffs: ActiveBuff[];
  className?: string;
}

export function EffectBadges({
  activeConditions,
  activeDots,
  activeHots,
  activeBuffs,
  className,
}: EffectBadgesProps) {
  const badges = [
    ...Object.entries(activeConditions)
      .filter(([, expiresAt]) => expiresAt !== undefined)
      .map(([type, expiresAt]) => (
        <EffectBadge
          key={`condition-${type}`}
          kind="condition"
          type={type}
          expiresAtGameTimeMilliseconds={Number(expiresAt)}
        />
      )),
    ...activeDots.map((dot, index) => <EffectBadge key={`dot-${index}`} kind="dot" dot={dot} />),
    ...activeHots.map((hot, index) => <EffectBadge key={`hot-${index}`} kind="hot" hot={hot} />),
    ...activeBuffs.map((buff, index) => (
      <EffectBadge key={`buff-${index}`} kind="buff" buff={buff} />
    )),
  ];

  if (badges.length === 0) {
    return null;
  }

  return <div className={cn('flex flex-wrap gap-1', className)}>{badges}</div>;
}
