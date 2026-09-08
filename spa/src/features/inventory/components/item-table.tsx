import type { ReactNode } from 'react';

import type { ItemDetail } from '@/api/client';
import { Skeleton } from '@/components/ui/skeleton';
import { InventoryEmptyState } from '@/features/inventory/components/inventory-empty-state';
import { InventoryFilters } from '@/features/inventory/components/inventory-filters';
import {
  ItemDamageCell,
  ItemDefenseCell,
  ItemStatisticCell,
} from '@/features/inventory/components/item-table-cells';
import { GoldIcon, WeightIcon } from '@/features/inventory/components/item-unit-icon';
import { SortableHeader } from '@/features/inventory/components/sortable-header';
import type { ItemTableState } from '@/features/inventory/hooks/use-item-table';
import { cn } from '@/lib/utils';

const numberFormatter = new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 });

export type ItemTableStatistic = 'damage' | 'defense' | 'quantity' | 'value' | 'weight';

interface ItemTableProps {
  table: ItemTableState;
  renderItemName: (item: ItemDetail) => ReactNode;
  loading?: boolean;
  emptyMessage?: string;
  renderLeadingHeader?: ReactNode;
  renderLeadingCell?: (item: ItemDetail) => ReactNode;
  renderAction?: (item: ItemDetail) => ReactNode;
  onRowClick?: (item: ItemDetail) => void;
  isSelected?: (item: ItemDetail) => boolean;
  // Omit to derive damage and defense from the active filter and show every other statistic.
  statistics?: readonly ItemTableStatistic[];
}

export function ItemTable({
  table,
  renderItemName,
  loading = false,
  emptyMessage = 'Nothing here.',
  renderLeadingHeader,
  renderLeadingCell,
  renderAction,
  onRowClick,
  isSelected,
  statistics,
}: ItemTableProps) {
  return (
    <div className="flex min-h-0 flex-1 flex-col gap-2">
      <InventoryFilters
        search={table.search}
        onSearchChange={table.setSearch}
        categories={table.categories}
        onCategoriesChange={table.onCategoriesChange}
        equippedOnly={table.equippedOnly}
        onEquippedOnlyChange={table.setEquippedOnly}
      />
      <div className="min-h-0 flex-1 scrollbar-gutter-stable overflow-auto">
        <ItemTableContent
          table={table}
          renderItemName={renderItemName}
          loading={loading}
          emptyMessage={emptyMessage}
          renderLeadingHeader={renderLeadingHeader}
          renderLeadingCell={renderLeadingCell}
          renderAction={renderAction}
          onRowClick={onRowClick}
          isSelected={isSelected}
          statistics={statistics}
        />
      </div>
    </div>
  );
}

interface ItemTableContentProps {
  table: ItemTableState;
  renderItemName: (item: ItemDetail) => ReactNode;
  loading: boolean;
  emptyMessage: string;
  renderLeadingHeader?: ReactNode;
  renderLeadingCell?: (item: ItemDetail) => ReactNode;
  renderAction?: (item: ItemDetail) => ReactNode;
  onRowClick?: (item: ItemDetail) => void;
  isSelected?: (item: ItemDetail) => boolean;
  statistics?: readonly ItemTableStatistic[];
}

function ItemTableContent({
  table,
  renderItemName,
  loading,
  emptyMessage,
  renderLeadingHeader,
  renderLeadingCell,
  renderAction,
  onRowClick,
  isSelected,
  statistics,
}: ItemTableContentProps) {
  const showAllStats = table.categories.size === 0;
  const showDamage = statistics
    ? statistics.includes('damage')
    : showAllStats || table.categories.has('Weapon');
  const showDefense = statistics
    ? statistics.includes('defense')
    : showAllStats || table.categories.has('Armor') || table.categories.has('Shield');
  const showQuantity = statistics ? statistics.includes('quantity') : true;
  const showValue = statistics ? statistics.includes('value') : true;
  const showWeight = statistics ? statistics.includes('weight') : true;
  const hasActiveFilters = Boolean(table.search) || table.categories.size > 0 || table.equippedOnly;

  if (loading) {
    return (
      <ItemTableSkeleton
        hasLeadingCell={renderLeadingCell !== undefined}
        hasAction={renderAction !== undefined}
        showDamage={showDamage}
        showDefense={showDefense}
        showQuantity={showQuantity}
        showValue={showValue}
        showWeight={showWeight}
      />
    );
  }

  if (table.visibleItems.length === 0) {
    return (
      <InventoryEmptyState
        itemCount={table.itemCount}
        emptyMessage={emptyMessage}
        onClearFilters={hasActiveFilters ? table.clearFilters : undefined}
      />
    );
  }

  return (
    <table className="w-full table-fixed">
      <colgroup>
        {renderLeadingCell && <col className="w-7" />}
        <col />
        {showDamage && <col className="w-20" />}
        {showDefense && <col className="w-16" />}
        {showQuantity && <col className="w-20" />}
        {showValue && <col className="w-20" />}
        {showWeight && <col className="w-20" />}
        {renderAction && <col className="w-32" />}
      </colgroup>
      <thead>
        <tr className="text-muted-foreground text-[11px] font-semibold tracking-wider uppercase">
          {renderLeadingCell && <th className="px-2 py-2">{renderLeadingHeader}</th>}
          <SortableHeader
            label="Item"
            sortKey="name"
            sort={table.sort}
            onToggle={table.onToggleSort}
          />
          {showDamage && (
            <SortableHeader
              label="Damage"
              sortKey="damage"
              sort={table.sort}
              onToggle={table.onToggleSort}
              align="right"
            />
          )}
          {showDefense && (
            <SortableHeader
              label="Defense"
              sortKey="defense"
              sort={table.sort}
              onToggle={table.onToggleSort}
              align="right"
            />
          )}
          {showQuantity && (
            <SortableHeader
              label="Qty"
              sortKey="quantity"
              sort={table.sort}
              onToggle={table.onToggleSort}
              align="right"
            />
          )}
          {showValue && (
            <SortableHeader
              label="Value"
              sortKey="value"
              sort={table.sort}
              onToggle={table.onToggleSort}
              align="right"
            />
          )}
          {showWeight && (
            <SortableHeader
              label="Weight"
              sortKey="weight"
              sort={table.sort}
              onToggle={table.onToggleSort}
              align="right"
            />
          )}
          {renderAction && <th className="px-2 py-2" />}
        </tr>
      </thead>
      <tbody className="divide-border divide-y">
        {table.visibleItems.map((item) => {
          return (
            <tr
              key={item.itemId}
              className={cn(onRowClick && 'cursor-pointer', isSelected?.(item) && 'bg-primary/10')}
              onClick={onRowClick ? () => onRowClick(item) : undefined}
            >
              {renderLeadingCell && (
                <td className="px-2 py-1.5 align-middle">{renderLeadingCell(item)}</td>
              )}
              <td className="px-2 py-1.5 align-middle">{renderItemName(item)}</td>
              {showDamage && <ItemDamageCell item={item} />}
              {showDefense && <ItemDefenseCell item={item} />}
              {showQuantity && <ItemStatisticCell value={Number(item.quantity)} />}
              {showValue && (
                <ItemGoldValueCell value={Number(item.goldValue) * Number(item.quantity)} />
              )}
              {showWeight && <ItemWeightCell value={Number(item.weight) * Number(item.quantity)} />}
              {renderAction && (
                <td className="px-2 py-1.5 text-right align-middle">{renderAction(item)}</td>
              )}
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}

interface ItemTableSkeletonProps {
  hasLeadingCell: boolean;
  hasAction: boolean;
  showDamage: boolean;
  showDefense: boolean;
  showQuantity: boolean;
  showValue: boolean;
  showWeight: boolean;
}

function ItemTableSkeleton({
  hasLeadingCell,
  hasAction,
  showDamage,
  showDefense,
  showQuantity,
  showValue,
  showWeight,
}: ItemTableSkeletonProps) {
  return (
    <table className="w-full table-fixed" aria-label="Loading items">
      <colgroup>
        {hasLeadingCell && <col className="w-7" />}
        <col />
        {showDamage && <col className="w-20" />}
        {showDefense && <col className="w-16" />}
        {showQuantity && <col className="w-20" />}
        {showValue && <col className="w-20" />}
        {showWeight && <col className="w-16" />}
        {hasAction && <col className="w-32" />}
      </colgroup>
      <thead>
        <tr className="bg-primary/10 text-muted-foreground text-[11px] font-semibold tracking-wider uppercase">
          {hasLeadingCell && <th className="px-2 py-2" />}
          <th className="px-2 py-2 text-left">Item</th>
          {showDamage && <th className="px-2 py-2 text-right">Damage</th>}
          {showDefense && <th className="px-2 py-2 text-right">Defense</th>}
          {showQuantity && <th className="px-2 py-2 text-right">Qty</th>}
          {showValue && <th className="px-2 py-2 text-right">Value</th>}
          {showWeight && <th className="px-2 py-2 text-right">Weight</th>}
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
              <Skeleton className="h-7 w-3/4" />
            </td>
            {showDamage && (
              <td className="px-2 py-1.5">
                <Skeleton className="ml-auto h-5 w-12" />
              </td>
            )}
            {showDefense && (
              <td className="px-2 py-1.5">
                <Skeleton className="ml-auto h-5 w-10" />
              </td>
            )}
            {showQuantity && (
              <td className="px-2 py-1.5">
                <Skeleton className="ml-auto h-5 w-6" />
              </td>
            )}
            {showValue && (
              <td className="px-2 py-1.5">
                <Skeleton className="ml-auto h-7 w-10" />
              </td>
            )}
            {showWeight && (
              <td className="px-2 py-1.5">
                <Skeleton className="ml-auto h-7 w-10" />
              </td>
            )}
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

function ItemGoldValueCell({ value }: { value: number }) {
  const displayValue = numberFormatter.format(value);

  return (
    <td className="overflow-hidden px-2 py-1.5 text-right align-middle font-mono text-sm tabular-nums">
      <div className="flex h-7 w-full min-w-0 items-center justify-end gap-1" title={displayValue}>
        <span className="truncate">{displayValue}</span>
        <GoldIcon className="text-muted-foreground size-5" />
      </div>
    </td>
  );
}

function ItemWeightCell({ value }: { value: number }) {
  const displayValue = numberFormatter.format(value);

  return (
    <td className="overflow-hidden px-2 py-1.5 text-right align-middle font-mono text-sm tabular-nums">
      <div className="flex h-7 w-full min-w-0 items-center justify-end gap-1" title={displayValue}>
        <span className="truncate">{displayValue}</span>
        <WeightIcon className="text-muted-foreground size-5" />
      </div>
    </td>
  );
}
