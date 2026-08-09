import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowDown,
  ArrowLeft,
  ArrowRight,
  ArrowUp,
  PackageOpen,
  Skull,
  User,
  Weight,
} from 'lucide-react';
import { useEffect, useState } from 'react';

import { getCreatureInventoryOptions, transferInventoryMutation } from '@/api/client';
import type { ItemDetail, ItemRarity, ItemType } from '@/api/client';
import { NumericStepper } from '@/components/numeric-stepper';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogTitle } from '@/components/ui/dialog';
import { Empty, EmptyContent, EmptyMedia, EmptyTitle } from '@/components/ui/empty';
import {
  HoverPopover,
  HoverPopoverContent,
  HoverPopoverTextTrigger,
} from '@/components/ui/hover-popover';
import { InventoryFilters } from '@/features/inventory/components/inventory-filters';
import {
  isItemTableSortKeyVisible,
  ItemTable,
  type ItemTableItem,
  type ItemTableSortKey,
} from '@/features/inventory/components/item-table';
import { ItemTooltip } from '@/features/inventory/components/item-tooltip';
import type { SortState } from '@/features/inventory/components/sortable-header';
import { type ItemCategory, RARITY_COLOR, TYPE_ICON } from '@/features/inventory/item-visuals';

interface WorkingItem extends ItemTableItem {
  rowKey: string;
  itemId: string;
  name: string;
  type: ItemType;
  category: ItemCategory;
  rarity: ItemRarity | null;
  equippedSlot: string | null;
  weight: number;
  quantity: number;
  goldValue: number;
}

type Direction = 'toOther' | 'toPlayer';

interface PendingMove {
  itemId: string;
  quantity: number;
  direction: Direction;
}

type SortKey = ItemTableSortKey;

const toWorkingItems = (items: ItemDetail[]): WorkingItem[] =>
  items.map((item) => ({
    rowKey: item.itemId,
    itemId: item.itemId,
    name: item.name,
    type: item.type,
    category: item.$type,
    rarity: item.rarity ?? null,
    equippedSlot: item.equippedSlot ?? null,
    weight: Number(item.weight),
    quantity: Number(item.quantity),
    goldValue: Number(item.goldValue),
    item,
  }));

const sortItems = (
  items: WorkingItem[],
  search: string,
  categories: ReadonlySet<ItemCategory>,
  equippedOnly: boolean,
  sort: SortState<SortKey>,
): WorkingItem[] => {
  const query = search.trim().toLowerCase();
  const filtered = items.filter((item) => {
    if (query && !item.name.toLowerCase().includes(query)) {
      return false;
    }
    if (equippedOnly && item.equippedSlot === null) {
      return false;
    }
    return categories.size === 0 || categories.has(item.category);
  });
  const dir = sort.dir === 'asc' ? 1 : -1;
  return [...filtered].sort((a, b) => {
    if (sort.key === 'name') {
      return a.name.localeCompare(b.name) * dir;
    }
    if (sort.key === 'quantity') {
      return (a.quantity - b.quantity) * dir;
    }
    if (sort.key === 'value') {
      return (a.goldValue * a.quantity - b.goldValue * b.quantity) * dir;
    }
    if (sort.key === 'damage') {
      const averageDamage = (item: WorkingItem) =>
        item.item.$type === 'Weapon' ? (item.item.minDamage + item.item.maxDamage) / 2 : 0;
      const averageDifference = averageDamage(a) - averageDamage(b);
      if (averageDifference !== 0) {
        return averageDifference * dir;
      }
      return (
        ((a.item.$type === 'Weapon' ? a.item.maxDamage : 0) -
          (b.item.$type === 'Weapon' ? b.item.maxDamage : 0)) *
        dir
      );
    }
    if (sort.key === 'defense') {
      return (
        ((a.item.$type === 'Armor' || a.item.$type === 'Shield' ? a.item.defense : 0) -
          (b.item.$type === 'Armor' || b.item.$type === 'Shield' ? b.item.defense : 0)) *
        dir
      );
    }
    return (a.weight * a.quantity - b.weight * b.quantity) * dir;
  });
};

function moveSelected(
  fromItems: WorkingItem[],
  setFromItems: (items: WorkingItem[]) => void,
  toItems: WorkingItem[],
  setToItems: (items: WorkingItem[]) => void,
  selected: Map<string, number>,
  setSelected: (selected: Map<string, number>) => void,
  direction: Direction,
  recordMoves: (moves: PendingMove[]) => void,
) {
  const moves: PendingMove[] = [];
  let nextFrom = fromItems;
  const nextTo = [...toItems];

  for (const [rowKey, selectedQty] of selected) {
    const row = fromItems.find((r) => r.rowKey === rowKey);
    if (!row) {
      continue;
    }
    const amount = Math.min(selectedQty, row.quantity);
    if (amount <= 0) {
      continue;
    }

    moves.push({ itemId: row.itemId, quantity: amount, direction });
    nextFrom = nextFrom
      .map((r) => (r.rowKey === rowKey ? { ...r, quantity: r.quantity - amount } : r))
      .filter((r) => r.quantity > 0);

    const existingGoldIndex = row.type === 'Gold' ? nextTo.findIndex((r) => r.type === 'Gold') : -1;
    if (existingGoldIndex >= 0) {
      nextTo[existingGoldIndex] = {
        ...nextTo[existingGoldIndex],
        quantity: nextTo[existingGoldIndex].quantity + amount,
      };
    } else {
      nextTo.push({ ...row, rowKey: crypto.randomUUID(), quantity: amount, equippedSlot: null });
    }
  }

  setFromItems(nextFrom);
  setToItems(nextTo);
  setSelected(new Map());
  recordMoves(moves);
}

interface TransferTarget {
  id: string;
  name: string;
}

export interface TransferItemDialogProps {
  playerId: string;
  target: TransferTarget | null;
  open: boolean;
  transfersEnabled?: boolean;
  onClose: () => void;
}

export function TransferItemDialog({
  playerId,
  target,
  open,
  transfersEnabled = true,
  onClose,
}: TransferItemDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent
        className="flex h-[min(94vh,880px)] flex-col gap-4 md:max-w-[min(96vw,96rem)]"
        onPointerDownOutside={(event) => event.preventDefault()}
      >
        {target && (
          <DialogTitle>{transfersEnabled ? 'Transfer Items' : 'Inspect Inventory'}</DialogTitle>
        )}
        {target && (
          <TransferDialogBody
            key={target.id}
            playerId={playerId}
            target={target}
            transfersEnabled={transfersEnabled}
            onClose={onClose}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}

function TransferDialogBody({
  playerId,
  target,
  transfersEnabled,
  onClose,
}: {
  playerId: string;
  target: TransferTarget;
  transfersEnabled: boolean;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const playerInventory = useQuery(getCreatureInventoryOptions({ path: { creatureId: playerId } }));
  const targetInventory = useQuery(
    getCreatureInventoryOptions({ path: { creatureId: target.id } }),
  );
  const transfer = useMutation(transferInventoryMutation());

  const [leftItems, setLeftItems] = useState<WorkingItem[] | null>(null);
  const [rightItems, setRightItems] = useState<WorkingItem[] | null>(null);
  const [pendingMoves, setPendingMoves] = useState<PendingMove[]>([]);
  const [leftSelected, setLeftSelected] = useState(new Map<string, number>());
  const [rightSelected, setRightSelected] = useState(new Map<string, number>());
  const [leftSearch, setLeftSearch] = useState('');
  const [rightSearch, setRightSearch] = useState('');
  const [leftCategories, setLeftCategories] = useState<ReadonlySet<ItemCategory>>(new Set());
  const [rightCategories, setRightCategories] = useState<ReadonlySet<ItemCategory>>(new Set());
  const [leftEquippedOnly, setLeftEquippedOnly] = useState(false);
  const [rightEquippedOnly, setRightEquippedOnly] = useState(false);
  const [leftSort, setLeftSort] = useState<SortState<SortKey>>({ key: 'name', dir: 'asc' });
  const [rightSort, setRightSort] = useState<SortState<SortKey>>({ key: 'name', dir: 'asc' });

  useEffect(() => {
    if (leftItems === null && playerInventory.data) {
      setLeftItems(toWorkingItems(playerInventory.data.items));
    }
  }, [leftItems, playerInventory.data]);

  useEffect(() => {
    if (rightItems === null && targetInventory.data) {
      setRightItems(toWorkingItems(targetInventory.data.items));
    }
  }, [rightItems, targetInventory.data]);

  if (leftItems === null || rightItems === null) {
    return (
      <div className="flex flex-1 items-center justify-center py-12">
        <p className="text-muted-foreground text-sm">Loading inventories...</p>
      </div>
    );
  }

  const recordMoves = (moves: PendingMove[]) =>
    setPendingMoves((current) => [...current, ...moves]);

  const handleTake = () =>
    moveSelected(
      rightItems,
      setRightItems,
      leftItems,
      setLeftItems,
      rightSelected,
      setRightSelected,
      'toPlayer',
      recordMoves,
    );

  const handleGive = () =>
    moveSelected(
      leftItems,
      setLeftItems,
      rightItems,
      setRightItems,
      leftSelected,
      setLeftSelected,
      'toOther',
      recordMoves,
    );

  const handleConfirm = async () => {
    const toOther = netMoves(pendingMoves, 'toOther', 'toPlayer');
    const toPlayer = netMoves(pendingMoves, 'toPlayer', 'toOther');

    if (toOther.size > 0) {
      await transfer.mutateAsync({
        body: {
          fromId: playerId,
          toId: target.id,
          items: [...toOther].map(([itemId, quantity]) => ({ itemId, quantity })),
        },
      });
    }
    if (toPlayer.size > 0) {
      await transfer.mutateAsync({
        body: {
          fromId: target.id,
          toId: playerId,
          items: [...toPlayer].map(([itemId, quantity]) => ({ itemId, quantity })),
        },
      });
    }

    await queryClient.invalidateQueries({
      queryKey: getCreatureInventoryOptions({ path: { creatureId: playerId } }).queryKey,
    });
    await queryClient.invalidateQueries({
      queryKey: getCreatureInventoryOptions({ path: { creatureId: target.id } }).queryKey,
    });
    onClose();
  };

  const changedCount =
    netMoves(pendingMoves, 'toOther', 'toPlayer').size +
    netMoves(pendingMoves, 'toPlayer', 'toOther').size;

  return (
    <>
      <div className="flex min-h-0 flex-1 flex-col items-stretch gap-3 overflow-hidden md:flex-row">
        <InventorySidePanel
          title={
            <>
              <User className="text-muted-foreground size-4" /> You
            </>
          }
          ariaLabel="Your inventory"
          items={leftItems}
          selected={leftSelected}
          selectionEnabled={transfersEnabled}
          onSelectedChange={setLeftSelected}
          search={leftSearch}
          onSearchChange={setLeftSearch}
          categories={leftCategories}
          onCategoriesChange={setLeftCategories}
          equippedOnly={leftEquippedOnly}
          onEquippedOnlyChange={setLeftEquippedOnly}
          sort={leftSort}
          onSortChange={setLeftSort}
        />

        <div className="flex flex-shrink-0 flex-row items-center justify-center gap-2 md:flex-col">
          <Button
            variant="outline"
            size="icon"
            className="relative rounded-full"
            onClick={handleTake}
            disabled={!transfersEnabled || rightSelected.size === 0}
            title={`Move selected items to your inventory`}
          >
            <ArrowUp className="md:hidden" />
            <ArrowLeft className="hidden md:inline" />
            {rightSelected.size > 0 && <ArrowBadge count={rightSelected.size} />}
          </Button>
          <Button
            variant="outline"
            size="icon"
            className="relative rounded-full"
            onClick={handleGive}
            disabled={!transfersEnabled || leftSelected.size === 0}
            title={`Move selected items to ${target.name}`}
          >
            <ArrowDown className="md:hidden" />
            <ArrowRight className="hidden md:inline" />
            {leftSelected.size > 0 && <ArrowBadge count={leftSelected.size} />}
          </Button>
        </div>

        <InventorySidePanel
          title={
            <>
              <Skull className="text-muted-foreground size-4" /> {target.name}
            </>
          }
          ariaLabel={`${target.name}'s inventory`}
          items={rightItems}
          selected={rightSelected}
          selectionEnabled={transfersEnabled}
          onSelectedChange={setRightSelected}
          search={rightSearch}
          onSearchChange={setRightSearch}
          categories={rightCategories}
          onCategoriesChange={setRightCategories}
          equippedOnly={rightEquippedOnly}
          onEquippedOnlyChange={setRightEquippedOnly}
          sort={rightSort}
          onSortChange={setRightSort}
        />
      </div>

      <DialogFooter className="flex-col items-stretch gap-2 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-muted-foreground text-sm">
          {changedCount === 0
            ? 'No changes yet.'
            : `${changedCount} stack${changedCount === 1 ? '' : 's'} to transfer.`}
        </p>
        <div className="flex gap-2">
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button
            onClick={handleConfirm}
            disabled={!transfersEnabled || changedCount === 0 || transfer.isPending}
          >
            Confirm Transfer
          </Button>
        </div>
      </DialogFooter>
    </>
  );
}

function netMoves(
  moves: PendingMove[],
  direction: Direction,
  opposite: Direction,
): Map<string, number> {
  const forward = new Map<string, number>();
  const backward = new Map<string, number>();
  for (const move of moves) {
    const map =
      move.direction === direction ? forward : move.direction === opposite ? backward : null;
    if (map) {
      map.set(move.itemId, (map.get(move.itemId) ?? 0) + move.quantity);
    }
  }
  for (const [itemId, quantity] of forward) {
    const net = quantity - (backward.get(itemId) ?? 0);
    if (net > 0) {
      forward.set(itemId, net);
    } else {
      forward.delete(itemId);
    }
  }
  return forward;
}

function ArrowBadge({ count }: { count: number }) {
  return (
    <span className="bg-primary text-primary-foreground absolute -top-1 -right-1 flex size-4 items-center justify-center rounded-full text-[10px] font-bold">
      {count}
    </span>
  );
}

interface InventorySidePanelProps {
  title: React.ReactNode;
  ariaLabel: string;
  items: WorkingItem[];
  selected: Map<string, number>;
  selectionEnabled: boolean;
  onSelectedChange: (selected: Map<string, number>) => void;
  search: string;
  onSearchChange: (value: string) => void;
  categories: ReadonlySet<ItemCategory>;
  onCategoriesChange: (categories: ReadonlySet<ItemCategory>) => void;
  equippedOnly: boolean;
  onEquippedOnlyChange: (equippedOnly: boolean) => void;
  sort: SortState<SortKey>;
  onSortChange: (sort: SortState<SortKey>) => void;
}

function InventorySidePanel({
  title,
  ariaLabel,
  items,
  selected,
  selectionEnabled,
  onSelectedChange,
  search,
  onSearchChange,
  categories,
  onCategoriesChange,
  equippedOnly,
  onEquippedOnlyChange,
  sort,
  onSortChange,
}: InventorySidePanelProps) {
  const sortedItems = sortItems(items, search, categories, equippedOnly, sort);
  const totalWeight = items.reduce((sum, item) => sum + item.weight * item.quantity, 0);
  const allSelected =
    sortedItems.length > 0 && sortedItems.every((item) => selected.has(item.rowKey));
  const someSelected = sortedItems.some((item) => selected.has(item.rowKey));

  const toggleSort = (key: SortKey) => {
    if (sort.key === key) {
      onSortChange({ key, dir: sort.dir === 'asc' ? 'desc' : 'asc' });
    } else {
      onSortChange({ key, dir: key === 'name' ? 'asc' : 'desc' });
    }
  };

  const handleCategoriesChange = (next: ReadonlySet<ItemCategory>) => {
    onCategoriesChange(next);
    if (!isItemTableSortKeyVisible(sort.key, next)) {
      onSortChange({ key: 'name', dir: 'asc' });
    }
  };

  const clearFilters = () => {
    onSearchChange('');
    onCategoriesChange(new Set());
    onEquippedOnlyChange(false);
    if (!isItemTableSortKeyVisible(sort.key, new Set())) {
      onSortChange({ key: 'name', dir: 'asc' });
    }
  };

  const toggleSelectAll = () => {
    const next = new Map(selected);
    if (allSelected) {
      for (const item of sortedItems) {
        next.delete(item.rowKey);
      }
    } else {
      for (const item of sortedItems) {
        next.set(item.rowKey, item.quantity);
      }
    }
    onSelectedChange(next);
  };

  const toggleRow = (item: WorkingItem) => {
    const next = new Map(selected);
    if (next.has(item.rowKey)) {
      next.delete(item.rowKey);
    } else {
      next.set(item.rowKey, item.quantity);
    }
    onSelectedChange(next);
  };

  const setRowQuantity = (rowKey: string, quantity: number) => {
    const next = new Map(selected);
    next.set(rowKey, quantity);
    onSelectedChange(next);
  };

  return (
    <div className="bg-muted/40 flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden rounded-lg border">
      <div className="border-b px-3 py-2">
        <div className="flex items-center gap-1.5 text-sm font-semibold">{title}</div>
        <p className="text-muted-foreground mt-0.5 flex items-center gap-1 text-xs">
          {items.length} items · {totalWeight} <Weight className="size-3 shrink-0" /> total
        </p>
      </div>

      <div className="flex min-h-0 flex-1 flex-col gap-2 p-3">
        <InventoryFilters
          search={search}
          onSearchChange={onSearchChange}
          categories={categories}
          onCategoriesChange={handleCategoriesChange}
          equippedOnly={equippedOnly}
          onEquippedOnlyChange={onEquippedOnlyChange}
          ariaLabel={ariaLabel}
        />

        <div className="min-h-0 flex-1 overflow-x-hidden overflow-y-auto">
          {sortedItems.length === 0 ? (
            <Empty className="h-full">
              <EmptyMedia variant="icon">
                <PackageOpen />
              </EmptyMedia>
              <EmptyTitle>
                {items.length === 0 ? 'Nothing here.' : 'No items match your filters.'}
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
              items={sortedItems.map((item) => ({ ...item, weight: item.weight * item.quantity }))}
              categories={categories}
              sort={sort}
              onToggleSort={toggleSort}
              rowClassName={(item) => (selected.has(item.rowKey) ? 'bg-accent' : undefined)}
              renderLeadingHeader={
                <div className="flex items-center">
                  <input
                    type="checkbox"
                    className="accent-primary size-4"
                    checked={allSelected}
                    ref={(element) => {
                      if (element) {
                        element.indeterminate = !allSelected && someSelected;
                      }
                    }}
                    disabled={!selectionEnabled || sortedItems.length === 0}
                    onChange={toggleSelectAll}
                    aria-label="Select all"
                  />
                </div>
              }
              renderLeadingCell={(item) => (
                <TransferSelectionCell
                  item={item}
                  selectedQuantity={selected.get(item.rowKey)}
                  selectionEnabled={selectionEnabled}
                  onToggle={() => toggleRow(item)}
                />
              )}
              renderName={(item) => (
                <TransferItemName
                  item={item}
                  selectedQuantity={selected.get(item.rowKey)}
                  selectionEnabled={selectionEnabled}
                  onQuantityChange={(quantity) => setRowQuantity(item.rowKey, quantity)}
                />
              )}
            />
          )}
        </div>
      </div>
    </div>
  );
}

function TransferSelectionCell({
  item,
  selectedQuantity,
  selectionEnabled,
  onToggle,
}: {
  item: WorkingItem;
  selectedQuantity: number | undefined;
  selectionEnabled: boolean;
  onToggle: () => void;
}) {
  const checked = selectedQuantity !== undefined;

  return (
    <div className="flex h-5 items-center">
      <input
        type="checkbox"
        className="accent-primary size-4"
        checked={checked}
        disabled={!selectionEnabled}
        onChange={onToggle}
        aria-label={`Select ${item.name}`}
      />
    </div>
  );
}

function TransferItemName({
  item,
  selectedQuantity,
  selectionEnabled,
  onQuantityChange,
}: {
  item: WorkingItem;
  selectedQuantity: number | undefined;
  selectionEnabled: boolean;
  onQuantityChange: (quantity: number) => void;
}) {
  const checked = selectedQuantity !== undefined;
  const equipped = item.equippedSlot !== null;
  const Icon = TYPE_ICON[item.type];
  const rarityColor = item.rarity ? RARITY_COLOR[item.rarity] : undefined;

  return (
    <>
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
          <HoverPopoverTextTrigger className="min-w-0 truncate text-left font-medium">
            {item.name}
          </HoverPopoverTextTrigger>
          <HoverPopoverContent side="bottom" className="w-auto max-w-64 p-2 text-sm">
            <ItemTooltip item={item.item} />
          </HoverPopoverContent>
        </HoverPopover>
        {equipped && (
          <span className="bg-muted text-muted-foreground shrink-0 rounded-full px-1.5 py-0.5 text-[10px] font-semibold uppercase">
            Equipped
          </span>
        )}
      </div>
      {selectionEnabled && checked && item.quantity > 1 && (
        <div className="mt-1">
          <NumericStepper
            value={selectedQuantity}
            onChange={onQuantityChange}
            min={1}
            max={item.quantity}
          />
        </div>
      )}
    </>
  );
}
