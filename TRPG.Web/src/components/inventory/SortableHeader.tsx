import { ChevronDown, ChevronUp } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface SortState<K extends string> {
  key: K;
  dir: 'asc' | 'desc';
}

interface SortableHeaderProps<K extends string> {
  label: string;
  sortKey: K;
  sort: SortState<K>;
  onToggle: (key: K) => void;
  align?: 'left' | 'right';
}

export function SortableHeader<K extends string>({
  label,
  sortKey,
  sort,
  onToggle,
  align = 'left',
}: SortableHeaderProps<K>) {
  const active = sort.key === sortKey;
  const Icon = sort.dir === 'desc' ? ChevronDown : ChevronUp;
  return (
    <th
      className={cn(
        'cursor-pointer px-2 py-2',
        align === 'right' ? 'text-right' : 'text-left',
        active ? 'text-foreground' : 'hover:text-foreground',
      )}
      onClick={() => onToggle(sortKey)}
    >
      <span
        className={cn('inline-flex items-center gap-0.5', align === 'right' && 'flex-row-reverse')}
      >
        {label}
        <Icon className={cn('size-2.5', !active && 'opacity-0 group-hover:opacity-100')} />
      </span>
    </th>
  );
}
