import { useEffect, useMemo, useState, type Dispatch, type SetStateAction } from 'react';

import type { ItemDetail } from '@/api/client';
import type { ItemCategory } from '@/features/inventory/item-visuals';

export type ItemTableSortKey = 'name' | 'damage' | 'defense' | 'quantity' | 'value' | 'weight';

export interface ItemTableSortState {
  key: ItemTableSortKey;
  dir: 'asc' | 'desc';
}

interface UseItemTableOptions {
  resetKey?: string;
}

export interface ItemTableState {
  itemCount: number;
  visibleItems: ItemDetail[];
  search: string;
  setSearch: Dispatch<SetStateAction<string>>;
  categories: ReadonlySet<ItemCategory>;
  onCategoriesChange: (categories: ReadonlySet<ItemCategory>) => void;
  equippedOnly: boolean;
  setEquippedOnly: Dispatch<SetStateAction<boolean>>;
  sort: ItemTableSortState;
  onToggleSort: (key: ItemTableSortKey) => void;
  clearFilters: () => void;
}

function defaultSort(): ItemTableSortState {
  return { key: 'name', dir: 'asc' };
}

export function useItemTable(items: readonly ItemDetail[], { resetKey }: UseItemTableOptions = {}) {
  const [search, setSearch] = useState('');
  const [categories, setCategories] = useState<ReadonlySet<ItemCategory>>(new Set());
  const [equippedOnly, setEquippedOnly] = useState(false);
  const [sort, setSort] = useState<ItemTableSortState>(defaultSort);

  useEffect(() => {
    if (resetKey !== undefined) {
      setSearch('');
      setCategories(new Set());
      setEquippedOnly(false);
      setSort(defaultSort());
    }
  }, [resetKey]);

  const onCategoriesChange = (next: ReadonlySet<ItemCategory>) => {
    setCategories(next);
    setSort((current) => (isSortKeyVisible(current.key, next) ? current : defaultSort()));
  };

  const onToggleSort = (key: ItemTableSortKey) => {
    setSort((current) =>
      current.key === key
        ? { key, dir: current.dir === 'asc' ? 'desc' : 'asc' }
        : { key, dir: key === 'name' ? 'asc' : 'desc' },
    );
  };

  const clearFilters = () => {
    setSearch('');
    setCategories(new Set());
    setEquippedOnly(false);
    setSort((current) => (isSortKeyVisible(current.key, new Set()) ? current : defaultSort()));
  };

  const visibleItems = useMemo(
    () => filterAndSortItems(items, search, categories, equippedOnly, sort),
    [items, search, categories, equippedOnly, sort],
  );

  return {
    itemCount: items.length,
    visibleItems,
    search,
    setSearch,
    categories,
    onCategoriesChange,
    equippedOnly,
    setEquippedOnly,
    sort,
    onToggleSort,
    clearFilters,
  } satisfies ItemTableState;
}

function isSortKeyVisible(key: ItemTableSortKey, categories: ReadonlySet<ItemCategory>) {
  if (key === 'damage') {
    return categories.size === 0 || categories.has('Weapon');
  }
  if (key === 'defense') {
    return categories.size === 0 || categories.has('Armor') || categories.has('Shield');
  }
  return true;
}

function filterAndSortItems(
  items: readonly ItemDetail[],
  search: string,
  categories: ReadonlySet<ItemCategory>,
  equippedOnly: boolean,
  sort: ItemTableSortState,
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
