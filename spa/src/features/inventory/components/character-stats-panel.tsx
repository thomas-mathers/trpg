import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { ArrowDown, ArrowUp } from 'lucide-react';

import {
  type AttributeName,
  type EffectiveAttributesResponse,
  type EquipmentSlot,
  getCreatureBasicAttackDamageOptions,
  previewCreatureBasicAttackDamageOptions,
  previewCreatureEquipmentOptions,
  getCreatureAttributesOptions,
} from '@/api/client';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';

import { ATTRIBUTE_LABEL } from '../display-names';

export interface EquipItemPreview {
  itemId: string;
  slot: EquipmentSlot;
}

const PERCENT_ATTRIBUTES = new Set<AttributeName>([
  'PhysicalResistance',
  'FireResistance',
  'IceResistance',
  'LightningResistance',
  'PoisonResistance',
  'MagicResistance',
]);

const STAT_GROUPS: Array<{ label: string; attributes: AttributeName[] }> = [
  {
    label: 'Attributes',
    attributes: [
      'Strength',
      'Dexterity',
      'Intelligence',
      'Endurance',
      'Stamina',
      'Mana',
      'Defense',
    ],
  },
  {
    label: 'Vitals',
    attributes: ['MaximumHp', 'MaximumAp', 'MaximumMp', 'MovementSpeed'],
  },
  {
    label: 'Resistances',
    attributes: [
      'PhysicalResistance',
      'FireResistance',
      'IceResistance',
      'LightningResistance',
      'PoisonResistance',
      'MagicResistance',
    ],
  },
];

function toAttributeMap(stats: EffectiveAttributesResponse): Record<AttributeName, number> {
  return {
    Strength: Number(stats.strength),
    Dexterity: Number(stats.dexterity),
    Intelligence: Number(stats.intelligence),
    Endurance: Number(stats.endurance),
    Stamina: Number(stats.stamina),
    Mana: Number(stats.mana),
    Defense: Number(stats.defense),
    MaximumHp: Number(stats.maximumHp),
    MaximumAp: Number(stats.maximumAp),
    MaximumMp: Number(stats.maximumMp),
    MovementSpeed: Number(stats.movementSpeed),
    PhysicalResistance: Number(stats.physicalResistance),
    FireResistance: Number(stats.fireResistance),
    IceResistance: Number(stats.iceResistance),
    LightningResistance: Number(stats.lightningResistance),
    PoisonResistance: Number(stats.poisonResistance),
    MagicResistance: Number(stats.magicResistance),
  };
}

function formatNumber(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
}

function StatRow({
  label,
  current,
  delta,
  isPercent = false,
  loading = false,
}: {
  label: string;
  current: number;
  delta: number | null;
  isPercent?: boolean;
  loading?: boolean;
}) {
  const suffix = isPercent ? '%' : '';

  return (
    <div className="flex items-center justify-between text-sm">
      <span className="text-muted-foreground">{label}</span>
      {loading ? (
        <Skeleton className="h-4 w-10" />
      ) : (
        <span className="flex items-center gap-1.5 font-medium tabular-nums">
          {formatNumber(current)}
          {suffix}
          {delta != null && delta !== 0 && (
            <span
              className={cn(
                'inline-flex items-center gap-0.5',
                delta > 0 ? 'text-heal' : 'text-destructive',
              )}
            >
              {delta > 0 ? <ArrowUp className="size-3" /> : <ArrowDown className="size-3" />}
              {formatNumber(Math.abs(delta))}
              {suffix}
            </span>
          )}
        </span>
      )}
    </div>
  );
}

export function CharacterStatsPanel({
  creatureId,
  previewItem,
}: {
  creatureId: string;
  previewItem: EquipItemPreview | null;
}) {
  const { data: current } = useQuery({
    ...getCreatureAttributesOptions({ path: { creatureId } }),
    placeholderData: keepPreviousData,
  });
  const { data: preview } = useQuery({
    ...previewCreatureEquipmentOptions({
      path: { creatureId },
      query: previewItem ?? { itemId: '', slot: 'RightHand' },
    }),
    enabled: previewItem != null,
    placeholderData: keepPreviousData,
  });
  const { data: currentDamage } = useQuery({
    ...getCreatureBasicAttackDamageOptions({ path: { creatureId } }),
    placeholderData: keepPreviousData,
  });
  const { data: previewDamage } = useQuery({
    ...previewCreatureBasicAttackDamageOptions({
      path: { creatureId },
      query: previewItem ?? { itemId: '', slot: 'RightHand' },
    }),
    enabled: previewItem != null,
    placeholderData: keepPreviousData,
  });

  const loading = !current || !currentDamage;
  const currentMap = current ? toAttributeMap(current) : null;
  const previewMap = previewItem && preview ? toAttributeMap(preview) : null;
  const currentDamagePerTurn = currentDamage ? Number(currentDamage.damagePerTurn) : 0;
  const previewDamagePerTurn =
    previewItem && previewDamage ? Number(previewDamage.damagePerTurn) : null;

  return (
    <div className="w-56 shrink-0 space-y-4 border-l pl-4">
      <div className="space-y-1">
        <p className="text-muted-foreground text-xs font-semibold uppercase">Combat</p>
        <StatRow
          label="Basic Attack"
          current={currentDamagePerTurn}
          delta={previewDamagePerTurn != null ? previewDamagePerTurn - currentDamagePerTurn : null}
          loading={loading}
        />
      </div>
      {STAT_GROUPS.map((group) => (
        <div key={group.label} className="space-y-1">
          <p className="text-muted-foreground text-xs font-semibold uppercase">{group.label}</p>
          {group.attributes.map((attribute) => (
            <StatRow
              key={attribute}
              label={ATTRIBUTE_LABEL[attribute]}
              current={currentMap ? currentMap[attribute] : 0}
              delta={
                currentMap && previewMap ? previewMap[attribute] - currentMap[attribute] : null
              }
              isPercent={PERCENT_ATTRIBUTES.has(attribute)}
              loading={loading}
            />
          ))}
        </div>
      ))}
    </div>
  );
}
