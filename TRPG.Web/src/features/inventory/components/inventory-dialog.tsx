import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Coins, PackageOpen, Weight } from 'lucide-react';
import { useState } from 'react';

import {
  getCreatureBasicAttackDamageOptions,
  getCreatureInventoryOptions,
  getCreatureStatsOptions,
  equipCreatureItemMutation,
  unequipCreatureItemMutation,
} from '@/api/client';
import type { EquipmentSlot, ItemDetail, ItemType } from '@/api/client';
import { SearchInput } from '@/components/search-input';
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
import { Toggle } from '@/components/ui/toggle';
import {
  CharacterStatsPanel,
  type EquipItemPreview,
} from '@/features/inventory/components/character-stats-panel';
import { ItemTooltip } from '@/features/inventory/components/item-tooltip';
import { SortableHeader, type SortState } from '@/features/inventory/components/sortable-header';
import {
  CATEGORY_ORDER,
  type ItemCategory,
  RARITY_COLOR,
  TYPE_ICON,
} from '@/features/inventory/item-visuals';
import { cn } from '@/lib/utils';

import { CATEGORY_LABEL, EQUIPMENT_SLOT_LABEL } from '../display-names';

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

type SortKey = 'name' | 'weight' | 'value';

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
    if (sort.key === 'value') {
      return (
        (Number(a.goldValue ?? 0) * Number(a.quantity) -
          Number(b.goldValue ?? 0) * Number(b.quantity)) *
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

  const toggleCategory = (category: ItemCategory) => {
    const next = new Set(categories);
    if (next.has(category)) {
      next.delete(category);
    } else {
      next.add(category);
    }
    setCategories(next);
  };

  const clearFilters = () => {
    setSearch('');
    setCategories(new Set());
    setEquippedOnly(false);
  };

  const invalidateCreatureData = () => {
    queryClient.invalidateQueries({ queryKey: inventoryOptions.queryKey });
    queryClient.invalidateQueries({
      queryKey: getCreatureStatsOptions({ path: { creatureId: playerId } }).queryKey,
    });
    queryClient.invalidateQueries({
      queryKey: getCreatureBasicAttackDamageOptions({
        path: { creatureId: playerId },
      }).queryKey,
    });
    queryClient.invalidateQueries({
      predicate: (query) => {
        const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;
        return (
          id === 'previewCreatureEquipment' ||
          id === 'previewCreatureBasicAttackDamage'
        );
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

  if (!inventory.data) {
    return (
      <div className="flex flex-1 items-center justify-center py-12">
        <p className="text-muted-foreground text-sm">Loading inventory...</p>
      </div>
    );
  }

  const items = inventory.data.items;
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
        <div className="flex min-w-0 flex-1 flex-col gap-4">
          <SearchInput value={search} onChange={setSearch} />

          <div className="flex w-full min-w-0 gap-1.5 overflow-x-auto py-2">
            <Toggle
              size="sm"
              variant="outline"
              className="shrink-0 rounded-full"
              pressed={equippedOnly}
              onPressedChange={setEquippedOnly}
            >
              Equipped
            </Toggle>
            {CATEGORY_ORDER.map((category) => (
              <Toggle
                key={category}
                size="sm"
                variant="outline"
                className="shrink-0 rounded-full"
                pressed={categories.has(category)}
                onPressedChange={() => toggleCategory(category)}
              >
                {CATEGORY_LABEL[category]}
              </Toggle>
            ))}
          </div>

          <div className="min-h-0 flex-1 overflow-x-hidden overflow-y-auto">
            {visible.length === 0 ? (
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
              <table className="w-full table-fixed">
                <colgroup>
                  <col />
                  <col className="w-16" />
                  <col className="w-16" />
                  <col className="w-24" />
                </colgroup>
                <thead>
                  <tr className="text-muted-foreground text-[11px] font-semibold tracking-wider uppercase">
                    <SortableHeader label="Item" sortKey="name" sort={sort} onToggle={toggleSort} />
                    <SortableHeader
                      label="Weight"
                      sortKey="weight"
                      sort={sort}
                      onToggle={toggleSort}
                      align="right"
                    />
                    <SortableHeader
                      label="Value"
                      sortKey="value"
                      sort={sort}
                      onToggle={toggleSort}
                      align="right"
                    />
                    <th className="px-2 py-2 text-right">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-border divide-y">
                  {visible.map((item) => (
                    <EquipmentRow
                      key={item.itemId}
                      item={item}
                      targetSlot={equipSlotFor(item, equippedSlots)}
                      busy={busy}
                      selected={item.itemId === selectedItemId}
                      onToggleSelect={() =>
                        setSelectedItemId((current) =>
                          current === item.itemId ? null : item.itemId,
                        )
                      }
                      onEquip={(slot) =>
                        equip.mutate({
                          path: { creatureId: playerId },
                          body: { itemId: item.itemId, slot },
                        })
                      }
                      onUnequip={(slot) =>
                        unequip.mutate({ path: { creatureId: playerId }, body: { slot } })
                      }
                    />
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>

        <CharacterStatsPanel creatureId={playerId} previewItem={previewItem} />
      </div>

      <DialogFooter>
        <Button variant="outline" onClick={onClose}>
          Close
        </Button>
      </DialogFooter>
    </>
  );
}

function EquipmentRow({
  item,
  targetSlot,
  busy,
  selected,
  onToggleSelect,
  onEquip,
  onUnequip,
}: {
  item: ItemDetail;
  targetSlot: EquipmentSlot | null;
  busy: boolean;
  selected: boolean;
  onToggleSelect: () => void;
  onEquip: (slot: EquipmentSlot) => void;
  onUnequip: (slot: EquipmentSlot) => void;
}) {
  const Icon = TYPE_ICON[item.type];
  const rarityColor = item.rarity ? RARITY_COLOR[item.rarity] : undefined;
  const equippedSlot = item.equippedSlot ?? null;

  return (
    <tr className={cn('cursor-pointer', selected && 'bg-accent/50')} onClick={onToggleSelect}>
      <td className="px-2 py-1.5">
        <div className="flex items-center gap-1.5 text-sm">
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
      </td>
      <td className="px-2 py-1.5 text-right font-mono text-sm tabular-nums">
        <div className="flex items-center justify-end gap-1">
          {item.weight}
          <Weight className="text-muted-foreground size-3 shrink-0" />
        </div>
      </td>
      <td className="px-2 py-1.5 text-right font-mono text-sm tabular-nums">
        <div className="flex items-center justify-end gap-1">
          {item.goldValue !== null ? (
            <>
              {item.goldValue}
              <Coins className="text-muted-foreground size-3 shrink-0" />
            </>
          ) : (
            '—'
          )}
        </div>
      </td>
      <td className="px-2 py-1.5 text-right">
        {equippedSlot ? (
          <Button
            size="sm"
            variant="outline"
            disabled={busy}
            onClick={(event) => {
              event.stopPropagation();
              onUnequip(equippedSlot);
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
              onEquip(targetSlot);
            }}
          >
            Equip
          </Button>
        ) : null}
      </td>
    </tr>
  );
}
