import type { IconType } from 'react-icons';
import {
  GiBleedingWound,
  GiBlindfold,
  GiCaltrops,
  GiFlame,
  GiHealing,
  GiMute,
  GiPoisonBottle,
  GiSnowflake1,
  GiStarSwirl,
  GiStarsStack,
  GiSwordBreak,
  GiWeightLiftingDown,
  GiWeightLiftingUp,
} from 'react-icons/gi';

import type { ActiveBuff, ActiveDot, ActiveHot } from '@/api/client';
import {
  HoverPopover,
  HoverPopoverContent,
  HoverPopoverTrigger,
} from '@/components/ui/hover-popover';
import { ATTRIBUTE_LABEL } from '@/features/inventory/display-names';
import { formatAmount } from '@/lib/formatting';
import { cn } from '@/lib/utils';

const CONDITION_ICON: Record<string, IconType> = {
  Blinded: GiBlindfold,
  Bleeding: GiBleedingWound,
  Burning: GiFlame,
  Disarmed: GiSwordBreak,
  Frozen: GiSnowflake1,
  Poisoned: GiPoisonBottle,
  Silenced: GiMute,
  Snared: GiCaltrops,
  Stunned: GiStarSwirl,
};

function turnLabel(turns: number): string {
  return `${turns} turn${turns === 1 ? '' : 's'} remaining`;
}

type EffectBadgeProps =
  | { kind: 'condition'; type: string; remainingTurns: number | string }
  | { kind: 'dot'; dot: ActiveDot }
  | { kind: 'hot'; hot: ActiveHot }
  | { kind: 'buff'; buff: ActiveBuff };

interface Description {
  icon: IconType;
  variant: 'status' | 'dot' | 'hot' | 'buff' | 'debuff';
  title: string;
  body: string;
  turns: number;
}

function describe(props: EffectBadgeProps): Description {
  switch (props.kind) {
    case 'condition': {
      const turns = Number(props.remainingTurns);
      return {
        icon: CONDITION_ICON[props.type] ?? GiStarsStack,
        variant: 'status',
        title: props.type,
        body: turnLabel(turns),
        turns,
      };
    }
    case 'dot': {
      const turns = Number(props.dot.remainingTurns);
      return {
        icon: GiFlame,
        variant: 'dot',
        title: props.dot.abilityName,
        body: `${props.dot.amount} ${props.dot.damageType.toLowerCase()} damage per turn — ${turnLabel(turns)}`,
        turns,
      };
    }
    case 'hot': {
      const turns = Number(props.hot.remainingTurns);
      return {
        icon: GiHealing,
        variant: 'hot',
        title: props.hot.abilityName,
        body: `Heals ${props.hot.amount} per turn — ${turnLabel(turns)}`,
        turns,
      };
    }
    case 'buff': {
      const turns = Number(props.buff.remainingTurns);
      const isDebuff = Number(props.buff.amount) < 0;
      const attribute = ATTRIBUTE_LABEL[props.buff.attribute] ?? props.buff.attribute;
      return {
        icon: isDebuff ? GiWeightLiftingDown : GiWeightLiftingUp,
        variant: isDebuff ? 'debuff' : 'buff',
        title: props.buff.abilityName,
        body: `${formatAmount(Number(props.buff.amount), props.buff.amountType)} ${attribute} — ${turnLabel(turns)}`,
        turns,
      };
    }
  }
}

const VARIANT_CLASSES: Record<Description['variant'], string> = {
  status: 'bg-muted text-muted-foreground',
  dot: 'bg-hp/15 text-hp',
  debuff: 'bg-hp/15 text-hp',
  hot: 'bg-heal/15 text-heal',
  buff: 'bg-heal/15 text-heal',
};

export function EffectBadge(props: EffectBadgeProps) {
  const { icon: Icon, variant, title, body, turns } = describe(props);

  return (
    <HoverPopover>
      <HoverPopoverTrigger asChild>
        <button
          type="button"
          className={cn(
            'relative flex h-[22px] w-[22px] shrink-0 items-center justify-center rounded-md outline-none',
            'focus-visible:ring-ring focus-visible:ring-2',
            VARIANT_CLASSES[variant],
          )}
          aria-label={`${title} — ${body}`}
        >
          <Icon className="h-3 w-3" />
          <span className="bg-card border-border text-foreground absolute -right-1 -bottom-1.5 min-w-[10px] rounded-[5px] border px-[2px] text-[8px] leading-tight font-bold tabular-nums">
            {turns}
          </span>
        </button>
      </HoverPopoverTrigger>
      <HoverPopoverContent side="top" className="w-auto max-w-56 p-2 text-xs">
        <p className="flex items-center gap-1.5 font-semibold">
          <Icon className="h-3 w-3 shrink-0" />
          {title}
        </p>
        <p className="text-muted-foreground mt-1">{body}</p>
      </HoverPopoverContent>
    </HoverPopover>
  );
}
