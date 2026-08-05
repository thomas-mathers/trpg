import { Coins, Weight } from 'lucide-react';

import type { ItemDetail } from '@/api/client';
import { ITEM_TYPE_LABEL, RESOURCE_LABEL } from '@/lib/enum-labels';
import { formatItemModifier } from '@/lib/item-modifier-format';
import { RARITY_COLOR, TYPE_ICON } from '@/lib/item-visuals';

function getStatLines(item: ItemDetail): string[] {
  switch (item.$type) {
    case 'Weapon':
      return [
        `Damage: ${item.minDamage}-${item.maxDamage}`,
        `Range: ${item.range}`,
        `Attacks per Turn: ${item.attacksPerTurn}`,
        ...(item.isTwoHanded ? ['Two-Handed'] : []),
        `Durability: ${item.durabilityCurrent}/${item.durabilityMax}`,
      ];
    case 'Armor':
      return [
        `Defense: ${item.defense}`,
        `Armor Class: ${item.armorClass}`,
        `Durability: ${item.durabilityCurrent}/${item.durabilityMax}`,
      ];
    case 'Shield': {
      const resistances: Array<[string, number]> = [
        ['Magic Resistance', Number(item.magicResistance)],
        ['Fire Resistance', Number(item.fireResistance)],
        ['Ice Resistance', Number(item.iceResistance)],
        ['Lightning Resistance', Number(item.lightningResistance)],
        ['Poison Resistance', Number(item.poisonResistance)],
      ];
      return [
        `Block Chance: ${item.blockChance}%`,
        `Defense: ${item.defense}`,
        ...resistances
          .filter(([, value]) => value !== 0)
          .map(([label, value]) => `${label}: ${value}%`),
        `Durability: ${item.durabilityCurrent}/${item.durabilityMax}`,
      ];
    }
    case 'Consumable':
      return [
        `Restores ${item.restoreAmount} ${RESOURCE_LABEL[item.resource]}`,
        ...(Number(item.duration) > 0 ? [`Duration: ${item.duration} turns`] : []),
      ];
    case 'Accessory':
    case 'Ammunition':
    case 'Gold':
      return [];
  }
}

export function ItemTooltip({ item }: { item: ItemDetail }) {
  const rarityColor = item.rarity ? RARITY_COLOR[item.rarity] : undefined;
  const Icon = TYPE_ICON[item.type];
  const statLines: React.ReactNode[] = [
    <span key="weight" className="inline-flex items-center gap-1">
      Weight: {item.weight}
      <Weight className="size-3" />
    </span>,
    ...(item.goldValue != null
      ? [
          <span key="value" className="inline-flex items-center gap-1">
            Value: {item.goldValue}
            <Coins className="size-3" />
          </span>,
        ]
      : []),
    ...getStatLines(item),
  ];

  return (
    <div className="space-y-1.5">
      <div className="flex items-center gap-1.5">
        <Icon className="text-muted-foreground mr-1 size-8 shrink-0" />
        <div>
          <p className="font-semibold" style={rarityColor ? { color: rarityColor } : undefined}>
            {item.name}
          </p>
          <p className="text-muted-foreground text-xs">{ITEM_TYPE_LABEL[item.type]}</p>
        </div>
      </div>

      {statLines.length > 0 && (
        <div className="border-border/60 space-y-0.5 border-t pt-1.5 text-xs">
          {statLines.map((line, index) => (
            <p key={index}>{line}</p>
          ))}
        </div>
      )}

      {item.description && (
        <p className="text-muted-foreground text-xs italic">{item.description}</p>
      )}

      {item.modifiers.length > 0 && (
        <div className="border-border/60 space-y-0.5 border-t pt-1.5 text-xs">
          {item.modifiers.map((modifier, index) => (
            <p key={index} style={{ color: 'var(--rarity-magic)' }}>
              {formatItemModifier(modifier)}
            </p>
          ))}
        </div>
      )}
    </div>
  );
}
