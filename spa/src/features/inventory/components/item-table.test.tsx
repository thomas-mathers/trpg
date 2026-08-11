import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { ItemDetail } from '@/api/client';
import { useItemTable } from '@/features/inventory/hooks/use-item-table';
import { renderWithProviders } from '@/test/test-utils';

import { ItemName } from './item-name';
import { ItemTable } from './item-table';

const items: ItemDetail[] = [];

function TestItemTable({ loading = false }: { loading?: boolean }) {
  const table = useItemTable(items);

  return (
    <ItemTable
      table={table}
      renderItemName={(item) => <ItemName item={item} />}
      loading={loading}
      emptyMessage="No items match your filters."
    />
  );
}

describe('ItemTable', () => {
  it('renders its empty state when no items are visible', () => {
    // Arrange
    renderWithProviders(<TestItemTable />);

    // Act
    const emptyState = screen.getByText('No items match your filters.');

    // Assert
    expect(emptyState).toBeVisible();
    expect(screen.getByPlaceholderText('Search...')).toBeVisible();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('renders its loading skeleton before the empty state', () => {
    // Arrange
    renderWithProviders(<TestItemTable loading />);

    // Act
    const skeleton = screen.getByRole('table', { name: 'Loading items' });

    // Assert
    expect(skeleton).toBeVisible();
    expect(screen.queryByText('No items match your filters.')).not.toBeInTheDocument();
  });
});
