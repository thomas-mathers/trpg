import { screen } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import type { ItemDetail } from '@/api/client';
import { handleGetWorkstationInventory, handleReadBookPage } from '@/api/client/msw.gen';
import { server } from '@/test/server';
import { renderWithProviders } from '@/test/test-utils';

import { BookshelfDialog } from './bookshelf-dialog';

const book = (name: string, itemId: string): ItemDetail => ({
  $type: 'Book',
  itemId,
  name,
  description: 'A bound volume.',
  weight: 2,
  quantity: 1,
  equippedSlot: null,
  type: 'Book',
  rarity: null,
  goldValue: 12,
  modifiers: [],
  isStackable: false,
});

const lantern = (): ItemDetail => ({
  $type: 'Key',
  itemId: 'key-id',
  name: 'Storeroom Key',
  description: 'A small iron key.',
  weight: 1,
  quantity: 1,
  equippedSlot: null,
  type: 'Key',
  rarity: null,
  goldValue: 1,
  modifiers: [],
  isStackable: false,
});

function stockShelf(items: ItemDetail[]) {
  server.use(
    handleGetWorkstationInventory(() =>
      HttpResponse.json({ items, weight: 0, carryingCapacity: null }),
    ),
  );
}

function renderShelf() {
  return renderWithProviders(
    <BookshelfDialog
      playerId="player-id"
      workstationId="shelf-id"
      name="Bookcase"
      open
      onClose={vi.fn()}
    />,
  );
}

describe('BookshelfDialog', () => {
  it('lists the books on the shelf', async () => {
    stockShelf([
      book('A History of Ravenhollow', 'book-1'),
      book('The Craft of the Smith', 'book-2'),
    ]);

    renderShelf();

    expect(await screen.findByText('A History of Ravenhollow')).toBeInTheDocument();
    expect(screen.getByText('The Craft of the Smith')).toBeInTheDocument();
  });

  it('leaves anything that is not a book on the shelf unlisted', async () => {
    stockShelf([book('A History of Ravenhollow', 'book-1'), lantern()]);

    renderShelf();

    await screen.findByText('A History of Ravenhollow');
    expect(screen.queryByText('Storeroom Key')).not.toBeInTheDocument();
  });

  it('opens the reader on the book that was chosen', async () => {
    stockShelf([book('A History of Ravenhollow', 'book-1')]);
    server.use(
      handleReadBookPage(() =>
        HttpResponse.json({
          title: 'A History of Ravenhollow',
          pageNumber: 1,
          pageCount: 2,
          text: 'The city was founded on a bend of the black river.',
          revealedSecret: false,
        }),
      ),
    );

    const { user } = renderShelf();
    await screen.findByText('A History of Ravenhollow');

    await user.click(screen.getByRole('button', { name: 'Read A History of Ravenhollow' }));

    expect(
      await screen.findByText('The city was founded on a bend of the black river.'),
    ).toBeInTheDocument();
  });

  it('says so when the shelf holds nothing readable', async () => {
    stockShelf([lantern()]);

    renderShelf();

    expect(await screen.findByText('Nothing worth reading here.')).toBeInTheDocument();
  });
});
