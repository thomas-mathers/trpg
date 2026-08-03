import {
  ArrowDown,
  ArrowUp,
  Ban,
  Droplet,
  EyeOff,
  Flame,
  Link2,
  type LucideIcon,
  Snowflake,
  Sparkles,
  VolumeX,
} from 'lucide-react';

import type { ActiveBuff, ActiveDot, ActiveHot, AttributeName, AmountType } from '@/api/client';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';

// activeConditions keys aren't a generated union (Dictionary<ConditionType,int>
// serializes as a plain string-keyed object) — default to Sparkles for anything
// not in this map rather than failing on an unrecognized status.
const CONDITION_ICON: Record<string, LucideIcon> = {
  Blinded: EyeOff,
  Bleeding: Droplet,
  Burning: Flame,
  Disarmed: Ban,
  Frozen: Snowflake,
  Poisoned: Droplet,
  Silenced: VolumeX,
  Snared: Link2,
  Stunned: Sparkles,
};

const ATTRIBUTE_LABEL: Record<AttributeName, string> = {
  MaximumHp: 'Maximum HP',
  MaximumAp: 'Maximum AP',
  MaximumMp: 'Maximum MP',
  Strength: 'Strength',
  Defense: 'Defense',
  Dexterity: 'Dexterity',
  Endurance: 'Endurance',
  Stamina: 'Stamina',
  Mana: 'Mana',
  Intelligence: 'Intelligence',
  PhysicalResistance: 'Physical Resistance',
  FireResistance: 'Fire Resistance',
  IceResistance: 'Ice Resistance',
  LightningResistance: 'Lightning Resistance',
  PoisonResistance: 'Poison Resistance',
  MagicResistance: 'Magic Resistance',
  MovementSpeed: 'Movement Speed',
};

function turnLabel(turns: number): string {
  return `${turns} turn${turns === 1 ? '' : 's'} remaining`;
}

function formatAmount(amount: number, amountType: AmountType): string {
  const sign = amount > 0 ? '+' : '';
  const unit = amountType === 'Percent' ? '%' : '';
  return `${sign}${amount}${unit}`;
}

type EffectBadgeProps =
  | { kind: 'condition'; type: string; remainingTurns: number | string }
  | { kind: 'dot'; dot: ActiveDot }
  | { kind: 'hot'; hot: ActiveHot }
  | { kind: 'buff'; buff: ActiveBuff };

interface Description {
  icon: LucideIcon;
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
        icon: CONDITION_ICON[props.type] ?? Sparkles,
        variant: 'status',
        title: props.type,
        body: turnLabel(turns),
        turns,
      };
    }
    case 'dot': {
      const turns = Number(props.dot.remainingTurns);
      return {
        icon: Flame,
        variant: 'dot',
        title: props.dot.abilityName,
        body: `${props.dot.amount} ${props.dot.damageType.toLowerCase()} damage per turn — ${turnLabel(turns)}`,
        turns,
      };
    }
    case 'hot': {
      const turns = Number(props.hot.remainingTurns);
      return {
        icon: Droplet,
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
        icon: isDebuff ? ArrowDown : ArrowUp,
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
  dot: 'bg-red-500/15 text-red-500',
  debuff: 'bg-red-500/15 text-red-500',
  hot: 'bg-green-500/15 text-green-500',
  buff: 'bg-green-500/15 text-green-500',
};

export function EffectBadge(props: EffectBadgeProps) {
  const { icon: Icon, variant, title, body, turns } = describe(props);

  return (
    <Popover>
      <PopoverTrigger asChild>
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
      </PopoverTrigger>
      <PopoverContent side="top" className="w-auto max-w-56 p-2 text-xs">
        <p className="flex items-center gap-1.5 font-semibold">
          <Icon className="h-3 w-3 shrink-0" />
          {title}
        </p>
        <p className="text-muted-foreground mt-1">{body}</p>
      </PopoverContent>
    </Popover>
  );
}
