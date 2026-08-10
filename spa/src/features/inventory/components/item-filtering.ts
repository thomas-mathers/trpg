import type { ItemDetail } from '@/api/client';
import type { SortState } from '@/features/inventory/components/sortable-header';
import type { ItemCategory } from '@/features/inventory/item-visuals';

import type { ItemTableSortKey } from './item-table';

export function filterAndSortItems(
  items: readonly ItemDetail[],
  search: string,
  categories: ReadonlySet<ItemCategory>,
  equippedOnly: boolean,
  sort: SortState<ItemTableSortKey>,
): ItemDetail[] {
  const query = search.trim().toLowerCase();
  const filtered = items.filter((item) => {
    if (query && !item.name.toLowerCase().includes(query)) {
      return false;
    }
    if (equippedOnly && item.equippedSlot == null) {
      return false;
    }
    return categories.size === 0 || categories.has(item.$type);
  });
  const direction = sort.dir === 'asc' ? 1 : -1;
  return [...filtered].sort((left, right) => {
    if (sort.key === 'name') {
      return left.name.localeCompare(right.name) * direction;
    }
    if (sort.key === 'quantity') {
      return (Number(left.quantity) - Number(right.quantity)) * direction;
    }
    if (sort.key === 'value') {
      return (
        (Number(left.goldValue) * Number(left.quantity) -
          Number(right.goldValue) * Number(right.quantity)) *
        direction
      );
    }
    if (sort.key === 'damage') {
      const averageDamage = (item: ItemDetail) =>
        item.$type === 'Weapon' ? (item.minDamage + item.maxDamage) / 2 : 0;
      const averageDifference = averageDamage(left) - averageDamage(right);
      if (averageDifference !== 0) {
        return averageDifference * direction;
      }
      return (
        ((left.$type === 'Weapon' ? left.maxDamage : 0) -
          (right.$type === 'Weapon' ? right.maxDamage : 0)) *
        direction
      );
    }
    if (sort.key === 'defense') {
      return (
        ((left.$type === 'Armor' || left.$type === 'Shield' ? left.defense : 0) -
          (right.$type === 'Armor' || right.$type === 'Shield' ? right.defense : 0)) *
        direction
      );
    }
    return (
      (Number(left.weight) * Number(left.quantity) -
        Number(right.weight) * Number(right.quantity)) *
      direction
    );
  });
}
