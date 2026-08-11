import type { ItemDetail } from '@/api/client';

const numberFormatter = new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 });

export function ItemDamageCell({ item }: { item: ItemDetail }) {
  const value = item.$type === 'Weapon' ? `${item.minDamage}–${item.maxDamage}` : null;
  return <ItemStatisticCell value={value} />;
}

export function ItemDefenseCell({ item }: { item: ItemDetail }) {
  const value = item.$type === 'Armor' || item.$type === 'Shield' ? item.defense : null;
  return <ItemStatisticCell value={value} />;
}

export function ItemStatisticCell({ value }: { value: number | string | null }) {
  const displayValue = typeof value === 'number' ? numberFormatter.format(value) : (value ?? '—');

  return (
    <td className="overflow-hidden px-2 py-1.5 text-right align-middle font-mono text-sm tabular-nums">
      <div
        className="flex h-5 w-full min-w-0 items-center justify-end truncate"
        title={String(displayValue)}
      >
        {displayValue}
      </div>
    </td>
  );
}
