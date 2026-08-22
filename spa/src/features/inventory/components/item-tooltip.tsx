import { GiTwoCoins, GiWeight } from 'react-icons/gi';

import type { ItemDetail } from '@/api/client';
import { formatItemModifier } from '@/features/inventory/item-modifier-format';
import { RARITY_COLOR, TYPE_ICON } from '@/features/inventory/item-visuals';

import { RESOURCE_LABEL, ITEM_TYPE_LABEL } from '../display-names';

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
    case 'Shield':
      return [
        `Block Chance: ${item.blockChance}%`,
        `Defense: ${item.defense}`,
        `Durability: ${item.durabilityCurrent}/${item.durabilityMax}`,
      ];
    case 'Consumable':
      return [
        `Restores ${item.restoreAmount} ${RESOURCE_LABEL[item.resource]}`,
        ...(Number(item.duration) > 0 ? [`Duration: ${item.duration} turns`] : []),
      ];
    case 'Accessory':
    case 'Ammunition':
    case 'Gold':
    case 'Key':
      return [];
  }
}

export function ItemTooltip({ item }: { item: ItemDetail }) {
  const rarityColor = item.rarity ? RARITY_COLOR[item.rarity] : undefined;
  const Icon = TYPE_ICON[item.type];
  const statLines: React.ReactNode[] = [
    <span key="weight" className="inline-flex items-center gap-1">
      Weight: {item.weight}
      <GiWeight className="size-4" />
    </span>,
    <span key="value" className="inline-flex items-center gap-1">
      Value: {item.goldValue}
      <GiTwoCoins className="size-4" />
    </span>,
    ...getStatLines(item),
  ];

  return (
    <div className="space-y-1.5">
      <div className="flex items-center gap-1.5">
        <Icon className="text-muted-foreground mr-1 size-8 shrink-0" />
        <div className="min-w-0 flex-1">
          <p
            className="truncate font-semibold"
            style={rarityColor ? { color: rarityColor } : undefined}
            title={item.name}
          >
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
