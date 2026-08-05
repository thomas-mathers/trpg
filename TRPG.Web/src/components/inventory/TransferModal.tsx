import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowDown,
  ArrowLeft,
  ArrowRight,
  ArrowUp,
  Coins,
  Search,
  Skull,
  User,
  Weight,
} from 'lucide-react';
import { useEffect, useState } from 'react';

import { getCreaturesByCreatureIdInventoryOptions, postTransfersMutation } from '@/api/client';
import type { ItemDetail, ItemRarity, ItemType } from '@/api/client';
import { ItemTooltip } from '@/components/inventory/ItemTooltip';
import { SortableHeader, type SortState } from '@/components/inventory/SortableHeader';
import { NumberStepper } from '@/components/NumberStepper';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  HoverPopover,
  HoverPopoverContent,
  HoverPopoverTextTrigger,
} from '@/components/ui/hover-popover';
import { RARITY_COLOR, TYPE_ICON } from '@/lib/item-visuals';
import { cn } from '@/lib/utils';

interface WorkingItem {
  rowKey: string;
  itemId: string;
  name: string;
  type: ItemType;
  rarity: ItemRarity | null;
  equippedSlot: string | null;
  weight: number;
  quantity: number;
  goldValue: number | null;
  detail: ItemDetail;
}

type Direction = 'toOther' | 'toPlayer';

interface PendingMove {
  itemId: string;
  quantity: number;
  direction: Direction;
}

type SortKey = 'name' | 'quantity' | 'weight' | 'value';

const toWorkingItems = (items: ItemDetail[]): WorkingItem[] =>
  items.map((item) => ({
    rowKey: item.itemId,
    itemId: item.itemId,
    name: item.name,
    type: item.type,
    rarity: item.rarity ?? null,
    equippedSlot: item.equippedSlot ?? null,
    weight: Number(item.weight),
    quantity: Number(item.quantity),
    goldValue: item.goldValue != null ? Number(item.goldValue) : null,
    detail: item,
  }));

const sortItems = (
  items: WorkingItem[],
  search: string,
  sort: SortState<SortKey>,
): WorkingItem[] => {
  const query = search.trim().toLowerCase();
  const filtered = query ? items.filter((item) => item.name.toLowerCase().includes(query)) : items;
  const dir = sort.dir === 'asc' ? 1 : -1;
  return [...filtered].sort((a, b) => {
    if (sort.key === 'name') {
      return a.name.localeCompare(b.name) * dir;
    }
    if (sort.key === 'quantity') {
      return (a.quantity - b.quantity) * dir;
    }
    if (sort.key === 'value') {
      return ((a.goldValue ?? 0) * a.quantity - (b.goldValue ?? 0) * b.quantity) * dir;
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

interface TransferModalProps {
  playerId: string;
  target: TransferTarget | null;
  open: boolean;
  onClose: () => void;
}

export function TransferModal({ playerId, target, open, onClose }: TransferModalProps) {
  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent
        className="flex max-h-[90vh] flex-col gap-4 md:max-w-6xl"
        onPointerDownOutside={(event) => event.preventDefault()}
      >
        {target && (
          <TransferModalBody
            key={target.id}
            playerId={playerId}
            target={target}
            onClose={onClose}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}

function TransferModalBody({
  playerId,
  target,
  onClose,
}: {
  playerId: string;
  target: TransferTarget;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const playerInventory = useQuery(
    getCreaturesByCreatureIdInventoryOptions({ path: { creatureId: playerId } }),
  );
  const targetInventory = useQuery(
    getCreaturesByCreatureIdInventoryOptions({ path: { creatureId: target.id } }),
  );
  const transfer = useMutation(postTransfersMutation());

  const [leftItems, setLeftItems] = useState<WorkingItem[] | null>(null);
  const [rightItems, setRightItems] = useState<WorkingItem[] | null>(null);
  const [pendingMoves, setPendingMoves] = useState<PendingMove[]>([]);
  const [leftSelected, setLeftSelected] = useState(new Map<string, number>());
  const [rightSelected, setRightSelected] = useState(new Map<string, number>());
  const [leftSearch, setLeftSearch] = useState('');
  const [rightSearch, setRightSearch] = useState('');
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
        query: { fromId: playerId, toId: target.id },
        body: { items: [...toOther].map(([itemId, quantity]) => ({ itemId, quantity })) },
      });
    }
    if (toPlayer.size > 0) {
      await transfer.mutateAsync({
        query: { fromId: target.id, toId: playerId },
        body: { items: [...toPlayer].map(([itemId, quantity]) => ({ itemId, quantity })) },
      });
    }

    await queryClient.invalidateQueries({
      queryKey: getCreaturesByCreatureIdInventoryOptions({ path: { creatureId: playerId } })
        .queryKey,
    });
    await queryClient.invalidateQueries({
      queryKey: getCreaturesByCreatureIdInventoryOptions({ path: { creatureId: target.id } })
        .queryKey,
    });
    onClose();
  };

  const changedCount =
    netMoves(pendingMoves, 'toOther', 'toPlayer').size +
    netMoves(pendingMoves, 'toPlayer', 'toOther').size;

  return (
    <>
      <DialogHeader>
        <DialogTitle>Transfer Items</DialogTitle>
      </DialogHeader>

      <div className="flex min-h-0 flex-1 flex-col items-stretch gap-3 overflow-hidden md:flex-row">
        <InventorySidePanel
          title={
            <>
              <User className="text-muted-foreground size-4" /> You
            </>
          }
          items={leftItems}
          selected={leftSelected}
          onSelectedChange={setLeftSelected}
          search={leftSearch}
          onSearchChange={setLeftSearch}
          sort={leftSort}
          onSortChange={setLeftSort}
        />

        <div className="flex flex-shrink-0 flex-row items-center justify-center gap-2 md:flex-col">
          <Button
            variant="outline"
            size="icon"
            className="relative rounded-full"
            onClick={handleTake}
            disabled={rightSelected.size === 0}
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
            disabled={leftSelected.size === 0}
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
          items={rightItems}
          selected={rightSelected}
          onSelectedChange={setRightSelected}
          search={rightSearch}
          onSearchChange={setRightSearch}
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
          <Button onClick={handleConfirm} disabled={changedCount === 0 || transfer.isPending}>
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
  items: WorkingItem[];
  selected: Map<string, number>;
  onSelectedChange: (selected: Map<string, number>) => void;
  search: string;
  onSearchChange: (value: string) => void;
  sort: SortState<SortKey>;
  onSortChange: (sort: SortState<SortKey>) => void;
}

function InventorySidePanel({
  title,
  items,
  selected,
  onSelectedChange,
  search,
  onSearchChange,
  sort,
  onSortChange,
}: InventorySidePanelProps) {
  const visible = sortItems(items, search, sort);
  const selectableVisible = visible.filter((item) => item.equippedSlot === null);
  const totalWeight = items.reduce((sum, item) => sum + item.weight * item.quantity, 0);
  const allSelected =
    selectableVisible.length > 0 && selectableVisible.every((item) => selected.has(item.rowKey));
  const someSelected = selectableVisible.some((item) => selected.has(item.rowKey));

  const toggleSort = (key: SortKey) => {
    if (sort.key === key) {
      onSortChange({ key, dir: sort.dir === 'asc' ? 'desc' : 'asc' });
    } else {
      onSortChange({ key, dir: key === 'name' ? 'asc' : 'desc' });
    }
  };

  const toggleSelectAll = () => {
    const next = new Map(selected);
    if (allSelected) {
      for (const item of selectableVisible) {
        next.delete(item.rowKey);
      }
    } else {
      for (const item of selectableVisible) {
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

      <div className="px-3 pt-2">
        <div className="border-input bg-background flex h-[34px] items-center gap-2 rounded-md border px-2.5 shadow-sm">
          <Search className="text-muted-foreground h-3.5 w-3.5 shrink-0" />
          <input
            type="text"
            value={search}
            onChange={(e) => onSearchChange(e.target.value)}
            placeholder="Search"
            className="placeholder:text-muted-foreground flex-1 bg-transparent text-sm outline-none"
          />
        </div>
      </div>

      <div className="flex-1 overflow-x-hidden overflow-y-auto px-3 pb-2">
        <table className="w-full table-fixed">
          <colgroup>
            <col className="w-7" />
            <col />
            <col className="w-12" />
            <col className="w-16" />
            <col className="w-16" />
          </colgroup>
          <thead>
            <tr className="text-muted-foreground text-[11px] font-semibold tracking-wider uppercase">
              <th className="px-2 py-2 text-left">
                <input
                  type="checkbox"
                  className="accent-primary size-4"
                  checked={allSelected}
                  ref={(el) => {
                    if (el) {
                      el.indeterminate = !allSelected && someSelected;
                    }
                  }}
                  disabled={selectableVisible.length === 0}
                  onChange={toggleSelectAll}
                  aria-label="Select all"
                />
              </th>
              <SortableHeader label="Item" sortKey="name" sort={sort} onToggle={toggleSort} />
              <SortableHeader
                label="Qty"
                sortKey="quantity"
                sort={sort}
                onToggle={toggleSort}
                align="right"
              />
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
            </tr>
          </thead>
          <tbody className="divide-border divide-y">
            {visible.map((item) => (
              <ItemRow
                key={item.rowKey}
                item={item}
                selectedQuantity={selected.get(item.rowKey)}
                onToggle={() => toggleRow(item)}
                onQuantityChange={(quantity) => setRowQuantity(item.rowKey, quantity)}
              />
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function ItemRow({
  item,
  selectedQuantity,
  onToggle,
  onQuantityChange,
}: {
  item: WorkingItem;
  selectedQuantity: number | undefined;
  onToggle: () => void;
  onQuantityChange: (quantity: number) => void;
}) {
  const checked = selectedQuantity !== undefined;
  const locked = item.equippedSlot !== null;
  const Icon = TYPE_ICON[item.type];
  const rarityColor = item.rarity ? RARITY_COLOR[item.rarity] : undefined;

  return (
    <tr className={cn(checked && 'bg-accent', locked && 'opacity-55')}>
      <td className="px-2 py-1.5 align-top">
        <div className="flex h-5 items-center">
          <input
            type="checkbox"
            className="accent-primary size-4"
            checked={checked}
            disabled={locked}
            onChange={onToggle}
            aria-label={`Select ${item.name}`}
            title={locked ? 'Unequip to transfer' : undefined}
          />
        </div>
      </td>
      <td className="px-2 py-1.5 align-top">
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
              <ItemTooltip item={item.detail} />
            </HoverPopoverContent>
          </HoverPopover>
          {locked && (
            <span className="bg-muted text-muted-foreground shrink-0 rounded-full px-1.5 py-0.5 text-[10px] font-semibold uppercase">
              Equipped
            </span>
          )}
        </div>
        {checked && item.quantity > 1 && (
          <div className="mt-1">
            <NumberStepper
              value={selectedQuantity}
              onChange={onQuantityChange}
              min={1}
              max={item.quantity}
            />
          </div>
        )}
      </td>
      <td className="px-2 py-1.5 text-right align-top font-mono text-sm tabular-nums">
        <div className="flex h-5 items-center justify-end">{item.quantity}</div>
      </td>
      <td className="px-2 py-1.5 text-right align-top font-mono text-sm tabular-nums">
        <div className="flex h-5 items-center justify-end gap-1">
          {item.weight * item.quantity}
          <Weight className="text-muted-foreground size-3 shrink-0" />
        </div>
      </td>
      <td className="px-2 py-1.5 text-right align-top font-mono text-sm tabular-nums">
        <div className="flex h-5 items-center justify-end gap-1">
          {item.goldValue !== null ? (
            <>
              {item.goldValue * item.quantity}
              <Coins className="text-muted-foreground size-3 shrink-0" />
            </>
          ) : (
            '—'
          )}
        </div>
      </td>
    </tr>
  );
}
