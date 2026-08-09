import { Coins, Weight } from 'lucide-react';
import type { ReactNode } from 'react';

import type { ItemDetail } from '@/api/client';
import { Skeleton } from '@/components/ui/skeleton';
import { SortableHeader, type SortState } from '@/features/inventory/components/sortable-header';
import type { ItemCategory } from '@/features/inventory/item-visuals';
import { cn } from '@/lib/utils';

export type ItemTableSortKey = 'name' | 'damage' | 'defense' | 'quantity' | 'value' | 'weight';

export interface ItemTableItem {
  item: ItemDetail;
  quantity: number;
  goldValue: number;
  weight: number;
}

export function isItemTableSortKeyVisible(
  key: ItemTableSortKey,
  categories: ReadonlySet<ItemCategory>,
) {
  if (key === 'damage') {
    return categories.size === 0 || categories.has('Weapon');
  }
  if (key === 'defense') {
    return categories.size === 0 || categories.has('Armor') || categories.has('Shield');
  }
  return true;
}

interface ItemTableProps<T extends ItemTableItem> {
  items: readonly T[];
  categories: ReadonlySet<ItemCategory>;
  sort: SortState<ItemTableSortKey>;
  onToggleSort: (key: ItemTableSortKey) => void;
  renderName: (item: T) => ReactNode;
  renderLeadingHeader?: ReactNode;
  renderLeadingCell?: (item: T) => ReactNode;
  renderAction?: (item: T) => ReactNode;
  onRowClick?: (item: T) => void;
  rowClassName?: (item: T) => string | undefined;
}

export function ItemTable<T extends ItemTableItem>({
  items,
  categories,
  sort,
  onToggleSort,
  renderName,
  renderLeadingHeader,
  renderLeadingCell,
  renderAction,
  onRowClick,
  rowClassName,
}: ItemTableProps<T>) {
  const showAllStats = categories.size === 0;
  const showDamage = showAllStats || categories.has('Weapon');
  const showDefense = showAllStats || categories.has('Armor') || categories.has('Shield');

  return (
    <table className="w-full table-fixed">
      <colgroup>
        {renderLeadingCell && <col className="w-7" />}
        <col />
        {showDamage && <col className="w-20" />}
        {showDefense && <col className="w-16" />}
        <col className="w-16" />
        <col className="w-16" />
        <col className="w-16" />
        {renderAction && <col className="w-24" />}
      </colgroup>
      <thead>
        <tr className="bg-muted/50 text-muted-foreground text-[11px] font-semibold tracking-wider uppercase">
          {renderLeadingCell && <th className="px-2 py-2">{renderLeadingHeader}</th>}
          <SortableHeader label="Item" sortKey="name" sort={sort} onToggle={onToggleSort} />
          {showDamage && (
            <SortableHeader
              label="Damage"
              sortKey="damage"
              sort={sort}
              onToggle={onToggleSort}
              align="right"
            />
          )}
          {showDefense && (
            <SortableHeader
              label="Defense"
              sortKey="defense"
              sort={sort}
              onToggle={onToggleSort}
              align="right"
            />
          )}
          <SortableHeader
            label="Qty"
            sortKey="quantity"
            sort={sort}
            onToggle={onToggleSort}
            align="right"
          />
          <SortableHeader
            label="Value"
            sortKey="value"
            sort={sort}
            onToggle={onToggleSort}
            align="right"
          />
          <SortableHeader
            label="Weight"
            sortKey="weight"
            sort={sort}
            onToggle={onToggleSort}
            align="right"
          />
          {renderAction && <th className="px-2 py-2" />}
        </tr>
      </thead>
      <tbody className="divide-border divide-y">
        {items.map((tableItem) => {
          const item = tableItem.item;
          const damage = item.$type === 'Weapon' ? `${item.minDamage}–${item.maxDamage}` : null;
          const defense = item.$type === 'Armor' || item.$type === 'Shield' ? item.defense : null;

          return (
            <tr
              key={item.itemId}
              className={cn(onRowClick && 'cursor-pointer', rowClassName?.(tableItem))}
              onClick={onRowClick ? () => onRowClick(tableItem) : undefined}
            >
              {renderLeadingCell && (
                <td className="px-2 py-1.5 align-top">{renderLeadingCell(tableItem)}</td>
              )}
              <td className="px-2 py-1.5 align-top">{renderName(tableItem)}</td>
              {showDamage && <ItemStatCell value={damage} />}
              {showDefense && <ItemStatCell value={defense} />}
              <ItemStatCell value={tableItem.quantity} />
              <td className="px-2 py-1.5 text-right align-top font-mono text-sm tabular-nums">
                <div className="flex h-5 items-center justify-end gap-1">
                  {tableItem.goldValue * tableItem.quantity}
                  <Coins className="text-muted-foreground size-3 shrink-0" />
                </div>
              </td>
              <td className="px-2 py-1.5 text-right align-top font-mono text-sm tabular-nums">
                <div className="flex h-5 items-center justify-end gap-1">
                  {tableItem.weight}
                  <Weight className="text-muted-foreground size-3 shrink-0" />
                </div>
              </td>
              {renderAction && (
                <td className="px-2 py-1.5 text-right align-top">{renderAction(tableItem)}</td>
              )}
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}

interface ItemTableSkeletonProps {
  hasLeadingCell?: boolean;
  hasAction?: boolean;
}

export function ItemTableSkeleton({
  hasLeadingCell = false,
  hasAction = false,
}: ItemTableSkeletonProps) {
  return (
    <table className="w-full table-fixed" aria-label="Loading items">
      <colgroup>
        {hasLeadingCell && <col className="w-7" />}
        <col />
        <col className="w-20" />
        <col className="w-16" />
        <col className="w-16" />
        <col className="w-16" />
        <col className="w-16" />
        {hasAction && <col className="w-24" />}
      </colgroup>
      <thead>
        <tr className="bg-muted/50 text-muted-foreground text-[11px] font-semibold tracking-wider uppercase">
          {hasLeadingCell && <th className="px-2 py-2" />}
          <th className="px-2 py-2 text-left">Item</th>
          <th className="px-2 py-2 text-right">Damage</th>
          <th className="px-2 py-2 text-right">Defense</th>
          <th className="px-2 py-2 text-right">Qty</th>
          <th className="px-2 py-2 text-right">Value</th>
          <th className="px-2 py-2 text-right">Weight</th>
          {hasAction && <th className="px-2 py-2" />}
        </tr>
      </thead>
      <tbody className="divide-border divide-y">
        {Array.from({ length: 5 }, (_, index) => (
          <tr key={index}>
            {hasLeadingCell && (
              <td className="px-2 py-1.5">
                <Skeleton className="size-4" />
              </td>
            )}
            <td className="px-2 py-1.5">
              <Skeleton className="h-5 w-3/4" />
            </td>
            <td className="px-2 py-1.5">
              <Skeleton className="ml-auto h-5 w-12" />
            </td>
            <td className="px-2 py-1.5">
              <Skeleton className="ml-auto h-5 w-10" />
            </td>
            <td className="px-2 py-1.5">
              <Skeleton className="ml-auto h-5 w-6" />
            </td>
            <td className="px-2 py-1.5">
              <Skeleton className="ml-auto h-5 w-10" />
            </td>
            <td className="px-2 py-1.5">
              <Skeleton className="ml-auto h-5 w-10" />
            </td>
            {hasAction && (
              <td className="px-2 py-1.5">
                <Skeleton className="ml-auto h-8 w-16" />
              </td>
            )}
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function ItemStatCell({ value }: { value: number | string | null }) {
  return (
    <td className="px-2 py-1.5 text-right align-top font-mono text-sm tabular-nums">
      <div className="flex h-5 items-center justify-end">{value ?? '—'}</div>
    </td>
  );
}
