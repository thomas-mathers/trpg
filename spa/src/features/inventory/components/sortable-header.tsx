import { ChevronDown, ChevronUp } from 'lucide-react';

import type {
  ItemTableSortKey,
  ItemTableSortState,
} from '@/features/inventory/hooks/use-item-table';
import { cn } from '@/lib/utils';

interface SortableHeaderProps {
  label: string;
  sortKey: ItemTableSortKey;
  sort: ItemTableSortState;
  onToggle: (key: ItemTableSortKey) => void;
  align?: 'left' | 'right';
}

export function SortableHeader({
  label,
  sortKey,
  sort,
  onToggle,
  align = 'left',
}: SortableHeaderProps) {
  const active = sort.key === sortKey;
  const Icon = sort.dir === 'desc' ? ChevronDown : ChevronUp;
  return (
    <th
      className={cn(
        'px-2 py-2',
        align === 'right' ? 'text-right' : 'text-left',
        active ? 'text-foreground' : 'hover:text-foreground',
      )}
    >
      <button
        type="button"
        onClick={() => onToggle(sortKey)}
        className={cn(
          'inline-flex items-center gap-0.5 whitespace-nowrap',
          align === 'right' && 'flex-row-reverse',
        )}
      >
        {label}
        <Icon className={cn('size-2.5', !active && 'opacity-0 group-hover:opacity-100')} />
      </button>
    </th>
  );
}
