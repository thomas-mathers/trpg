import { PackageOpen } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Empty, EmptyContent, EmptyMedia, EmptyTitle } from '@/components/ui/empty';

interface InventoryEmptyStateProps {
  itemCount: number;
  emptyMessage: string;
  hasActiveFilters: boolean;
  onClearFilters: () => void;
}

export function InventoryEmptyState({
  itemCount,
  emptyMessage,
  hasActiveFilters,
  onClearFilters,
}: InventoryEmptyStateProps) {
  return (
    <Empty className="h-full">
      <EmptyMedia variant="icon">
        <PackageOpen />
      </EmptyMedia>
      <EmptyTitle>{itemCount === 0 ? emptyMessage : 'No items match your filters.'}</EmptyTitle>
      {itemCount > 0 && hasActiveFilters && (
        <EmptyContent>
          <Button variant="outline" size="sm" onClick={onClearFilters}>
            Clear filters
          </Button>
        </EmptyContent>
      )}
    </Empty>
  );
}
