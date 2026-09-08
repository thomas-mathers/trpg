import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';

import { getWorkstationInventoryOptions } from '@/api/client';
import type { ItemDetail } from '@/api/client';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { BookReaderDialog } from '@/features/books/components/book-reader-dialog';
import { ItemName } from '@/features/inventory/components/item-name';
import { ItemTable } from '@/features/inventory/components/item-table';
import { useItemTable } from '@/features/inventory/hooks/use-item-table';

export interface BookshelfDialogProps {
  playerId: string;
  workstationId: string | null;
  name: string;
  open: boolean;
  onClose: () => void;
}

export function BookshelfDialog({
  playerId,
  workstationId,
  name,
  open,
  onClose,
}: BookshelfDialogProps) {
  const [readingItem, setReadingItem] = useState<ItemDetail | null>(null);

  const shelf = useQuery({
    ...getWorkstationInventoryOptions({ path: { workstationId: workstationId ?? '' } }),
    enabled: open && workstationId !== null,
  });

  const books = (shelf.data?.items ?? []).filter((item) => item.type === 'Book');
  const itemTable = useItemTable(books);

  return (
    <>
      <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
        <DialogContent className="flex h-[min(90vh,760px)] flex-col gap-4 md:max-w-3xl">
          <DialogHeader>
            <DialogTitle>{name}</DialogTitle>
          </DialogHeader>

          <div className="flex min-h-0 flex-1 flex-col">
            <ItemTable
              table={itemTable}
              renderItemName={(item) => <ItemName item={item} />}
              loading={shelf.isLoading}
              statistics={['value']}
              emptyMessage="Nothing worth reading here."
              renderAction={(item) => (
                <Button
                  size="sm"
                  onClick={(event) => {
                    event.stopPropagation();
                    setReadingItem(item);
                  }}
                >
                  Read
                </Button>
              )}
            />
          </div>

          <DialogFooter>
            <Button aria-label="Close bookshelf" variant="outline" onClick={onClose}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <BookReaderDialog
        playerId={playerId}
        itemId={readingItem?.itemId ?? null}
        title={readingItem?.name ?? ''}
        open={readingItem !== null}
        onClose={() => setReadingItem(null)}
      />
    </>
  );
}
