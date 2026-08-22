import { GiOpenTreasureChest } from 'react-icons/gi';

import { Button } from '@/components/ui/button';
import { Empty, EmptyContent, EmptyMedia, EmptyTitle } from '@/components/ui/empty';

interface InventoryEmptyStateProps {
  itemCount: number;
  emptyMessage: string;
  onClearFilters?: () => void;
}

export function InventoryEmptyState({
  itemCount,
  emptyMessage,
  onClearFilters,
}: InventoryEmptyStateProps) {
  return (
    <Empty className="h-full">
      <EmptyMedia variant="icon">
        <GiOpenTreasureChest />
      </EmptyMedia>
      <EmptyTitle>{itemCount === 0 ? emptyMessage : 'No items match your filters.'}</EmptyTitle>
      {itemCount > 0 && onClearFilters && (
        <EmptyContent>
          <Button variant="outline" size="sm" onClick={onClearFilters}>
            Clear filters
          </Button>
        </EmptyContent>
      )}
    </Empty>
  );
}
