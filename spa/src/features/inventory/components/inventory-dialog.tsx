import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { PackageOpen } from 'lucide-react';
import { useState } from 'react';

import {
  getCreatureBasicAttackDamageOptions,
  getCreatureInventoryOptions,
  getCreatureAttributesOptions,
  equipCreatureItemMutation,
  unequipCreatureItemMutation,
} from '@/api/client';
import type { EquipmentSlot, ItemDetail, ItemType } from '@/api/client';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Empty, EmptyContent, EmptyMedia, EmptyTitle } from '@/components/ui/empty';
import {
  HoverPopover,
  HoverPopoverContent,
  HoverPopoverTextTrigger,
} from '@/components/ui/hover-popover';
import {
  CharacterStatsPanel,
  type EquipItemPreview,
} from '@/features/inventory/components/character-stats-panel';
import { InventoryFilters } from '@/features/inventory/components/inventory-filters';
import {
  isItemTableSortKeyVisible,
  ItemTable,
  ItemTableSkeleton,
  type ItemTableSortKey,
} from '@/features/inventory/components/item-table';
import { ItemTooltip } from '@/features/inventory/components/item-tooltip';
import type { SortState } from '@/features/inventory/components/sortable-header';
import { type ItemCategory, RARITY_COLOR, TYPE_ICON } from '@/features/inventory/item-visuals';

import { EQUIPMENT_SLOT_LABEL } from '../display-names';

const WEAPON_TYPES = new Set<ItemType>([
  'Dagger',
  'Sword',
  'Axe',
  'Mace',
  'Hammer',
  'Staff',
  'Wand',
  'Bow',
  'Crossbow',
  'Javelin',
  'GreatSword',
  'GreatAxe',
  'GreatHammer',
]);

const ARMOR_SLOTS = new Set<ItemType>(['Helm', 'Chest', 'Boots', 'Gloves']);

function equipSlotFor(item: ItemDetail, equippedSlots: Set<EquipmentSlot>): EquipmentSlot | null {
  if (WEAPON_TYPES.has(item.type)) {
    return 'RightHand';
  }
  if (item.type === 'Shield' || item.type === 'Arrow' || item.type === 'Bolt') {
    return 'LeftHand';
  }
  if (ARMOR_SLOTS.has(item.type)) {
    return item.type as EquipmentSlot;
  }
  if (item.type === 'Necklace' || item.type === 'Belt') {
    return item.type;
  }
  if (item.type === 'Ring') {
    return equippedSlots.has('LeftRing') ? 'RightRing' : 'LeftRing';
  }
  return null;
}

type SortKey = ItemTableSortKey;

function sortItems(
  items: ItemDetail[],
  search: string,
  categories: ReadonlySet<ItemCategory>,
  equippedOnly: boolean,
  sort: SortState<SortKey>,
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
  const dir = sort.dir === 'asc' ? 1 : -1;
  return [...filtered].sort((a, b) => {
    if (sort.key === 'name') {
      return a.name.localeCompare(b.name) * dir;
    }
    if (sort.key === 'quantity') {
      return (Number(a.quantity) - Number(b.quantity)) * dir;
    }
    if (sort.key === 'value') {
      return (
        (Number(a.goldValue) * Number(a.quantity) - Number(b.goldValue) * Number(b.quantity)) * dir
      );
    }
    if (sort.key === 'damage') {
      const averageDamage = (item: ItemDetail) =>
        item.$type === 'Weapon' ? (item.minDamage + item.maxDamage) / 2 : 0;
      const averageDifference = averageDamage(a) - averageDamage(b);
      if (averageDifference !== 0) {
        return averageDifference * dir;
      }
      return (
        ((a.$type === 'Weapon' ? a.maxDamage : 0) - (b.$type === 'Weapon' ? b.maxDamage : 0)) * dir
      );
    }
    if (sort.key === 'defense') {
      return (
        ((a.$type === 'Armor' || a.$type === 'Shield' ? a.defense : 0) -
          (b.$type === 'Armor' || b.$type === 'Shield' ? b.defense : 0)) *
        dir
      );
    }
    return (Number(a.weight) * Number(a.quantity) - Number(b.weight) * Number(b.quantity)) * dir;
  });
}

interface InventoryDialogProps {
  playerId: string;
  open: boolean;
  onClose: () => void;
}

export function InventoryDialog({ playerId, open, onClose }: InventoryDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent
        className="flex h-[min(94vh,880px)] flex-col gap-4 md:max-w-5xl"
        onPointerDownOutside={(event) => event.preventDefault()}
      >
        <InventoryDialogBody playerId={playerId} onClose={onClose} />
      </DialogContent>
    </Dialog>
  );
}

function InventoryDialogBody({ playerId, onClose }: { playerId: string; onClose: () => void }) {
  const queryClient = useQueryClient();
  const inventoryOptions = getCreatureInventoryOptions({
    path: { creatureId: playerId },
  });
  const inventory = useQuery(inventoryOptions);

  const [search, setSearch] = useState('');
  const [categories, setCategories] = useState<ReadonlySet<ItemCategory>>(new Set());
  const [equippedOnly, setEquippedOnly] = useState(false);
  const [sort, setSort] = useState<SortState<SortKey>>({ key: 'name', dir: 'asc' });
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null);

  const handleCategoriesChange = (next: ReadonlySet<ItemCategory>) => {
    setCategories(next);
    if (!isItemTableSortKeyVisible(sort.key, next)) {
      setSort({ key: 'name', dir: 'asc' });
    }
  };

  const clearFilters = () => {
    setSearch('');
    setCategories(new Set());
    setEquippedOnly(false);
    if (!isItemTableSortKeyVisible(sort.key, new Set())) {
      setSort({ key: 'name', dir: 'asc' });
    }
  };

  const invalidateCreatureData = () => {
    queryClient.invalidateQueries({ queryKey: inventoryOptions.queryKey });
    queryClient.invalidateQueries({
      queryKey: getCreatureAttributesOptions({ path: { creatureId: playerId } }).queryKey,
    });
    queryClient.invalidateQueries({
      queryKey: getCreatureBasicAttackDamageOptions({
        path: { creatureId: playerId },
      }).queryKey,
    });
    queryClient.invalidateQueries({
      predicate: (query) => {
        const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;
        return id === 'previewCreatureEquipment' || id === 'previewCreatureBasicAttackDamage';
      },
    });
  };

  const equip = useMutation({
    ...equipCreatureItemMutation(),
    onSuccess: () => {
      invalidateCreatureData();
      setSelectedItemId(null);
    },
  });
  const unequip = useMutation({
    ...unequipCreatureItemMutation(),
    onSuccess: invalidateCreatureData,
  });

  const items = inventory.data?.items ?? [];
  const equippedSlots = new Set(
    items.map((item) => item.equippedSlot).filter((slot): slot is EquipmentSlot => slot != null),
  );
  const visible = sortItems(items, search, categories, equippedOnly, sort);
  const busy = equip.isPending || unequip.isPending;

  const selectedItem = selectedItemId ? items.find((i) => i.itemId === selectedItemId) : null;
  const selectedSlot =
    selectedItem && selectedItem.equippedSlot == null
      ? equipSlotFor(selectedItem, equippedSlots)
      : null;
  const previewItem: EquipItemPreview | null =
    selectedItem && selectedSlot ? { itemId: selectedItem.itemId, slot: selectedSlot } : null;

  const toggleSort = (key: SortKey) => {
    if (sort.key === key) {
      setSort({ key, dir: sort.dir === 'asc' ? 'desc' : 'asc' });
    } else {
      setSort({ key, dir: key === 'name' ? 'asc' : 'desc' });
    }
  };

  return (
    <>
      <DialogHeader>
        <DialogTitle>Inventory</DialogTitle>
      </DialogHeader>

      <div className="flex min-h-0 flex-1 gap-4">
        <div className="flex min-w-0 flex-1 flex-col gap-2">
          <InventoryFilters
            search={search}
            onSearchChange={setSearch}
            categories={categories}
            onCategoriesChange={handleCategoriesChange}
            equippedOnly={equippedOnly}
            onEquippedOnlyChange={setEquippedOnly}
          />

          <div className="min-h-0 flex-1 overflow-x-hidden overflow-y-auto">
            {!inventory.data ? (
              <ItemTableSkeleton hasAction />
            ) : visible.length === 0 ? (
              <Empty className="h-full">
                <EmptyMedia variant="icon">
                  <PackageOpen />
                </EmptyMedia>
                <EmptyTitle>
                  {items.length === 0 ? 'Your inventory is empty.' : 'No items match your filters.'}
                </EmptyTitle>
                {items.length > 0 && (search || categories.size > 0 || equippedOnly) && (
                  <EmptyContent>
                    <Button variant="outline" size="sm" onClick={clearFilters}>
                      Clear filters
                    </Button>
                  </EmptyContent>
                )}
              </Empty>
            ) : (
              <ItemTable
                items={visible.map((item) => ({
                  item,
                  quantity: Number(item.quantity),
                  goldValue: Number(item.goldValue),
                  weight: Number(item.weight) * Number(item.quantity),
                }))}
                categories={categories}
                sort={sort}
                onToggleSort={toggleSort}
                onRowClick={({ item }) =>
                  setSelectedItemId((current) => (current === item.itemId ? null : item.itemId))
                }
                rowClassName={({ item }) =>
                  item.itemId === selectedItemId ? 'bg-accent/50' : undefined
                }
                renderName={({ item }) => <EquipmentName item={item} />}
                renderAction={({ item }) => {
                  const targetSlot = equipSlotFor(item, equippedSlots);
                  const equippedSlot = item.equippedSlot ?? null;

                  return equippedSlot ? (
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={busy}
                      onClick={(event) => {
                        event.stopPropagation();
                        unequip.mutate({ path: { creatureId: playerId, slot: equippedSlot } });
                      }}
                    >
                      Unequip
                    </Button>
                  ) : targetSlot ? (
                    <Button
                      size="sm"
                      disabled={busy}
                      onClick={(event) => {
                        event.stopPropagation();
                        equip.mutate({
                          path: { creatureId: playerId },
                          body: { itemId: item.itemId, slot: targetSlot },
                        });
                      }}
                    >
                      Equip
                    </Button>
                  ) : null;
                }}
              />
            )}
          </div>
        </div>

        <CharacterStatsPanel creatureId={playerId} previewItem={previewItem} />
      </div>

      <DialogFooter>
        <Button aria-label="Close inventory" variant="outline" onClick={onClose}>
          Close
        </Button>
      </DialogFooter>
    </>
  );
}

function EquipmentName({ item }: { item: ItemDetail }) {
  const Icon = TYPE_ICON[item.type];
  const rarityColor = item.rarity ? RARITY_COLOR[item.rarity] : undefined;
  const equippedSlot = item.equippedSlot ?? null;

  return (
    <div className="flex h-5 items-center gap-1.5 text-sm">
      <Icon className="text-muted-foreground size-3.5 shrink-0" />
      {rarityColor && (
        <span
          className="size-1.5 shrink-0 rounded-full"
          style={{ backgroundColor: rarityColor }}
          title={item.rarity ?? undefined}
        />
      )}
      <HoverPopover>
        <HoverPopoverTextTrigger
          className="min-w-0 truncate text-left font-medium"
          onClick={(event) => event.stopPropagation()}
        >
          {item.name}
        </HoverPopoverTextTrigger>
        <HoverPopoverContent side="bottom" className="w-auto max-w-64 p-2 text-sm">
          <ItemTooltip item={item} />
        </HoverPopoverContent>
      </HoverPopover>
      {equippedSlot && (
        <span className="bg-muted text-muted-foreground shrink-0 rounded-full px-1.5 py-0.5 text-[10px] font-semibold uppercase">
          {EQUIPMENT_SLOT_LABEL[equippedSlot]}
        </span>
      )}
    </div>
  );
}
