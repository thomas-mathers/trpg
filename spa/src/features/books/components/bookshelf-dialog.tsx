import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { GiBlackBook } from 'react-icons/gi';

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
import { Skeleton } from '@/components/ui/skeleton';
import { BookReaderDialog } from '@/features/books/components/book-reader-dialog';

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

  return (
    <>
      <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
        <DialogContent className="flex h-[min(90vh,720px)] flex-col gap-4 md:max-w-2xl">
          <DialogHeader>
            <DialogTitle>{name}</DialogTitle>
          </DialogHeader>

          <div className="min-h-0 flex-1 overflow-y-auto">
            {shelf.isLoading && <ShelfSkeleton />}
            {!shelf.isLoading && books.length === 0 && (
              <p className="text-muted-foreground py-8 text-center">Nothing worth reading here.</p>
            )}
            {!shelf.isLoading && books.length > 0 && (
              <ul className="divide-border divide-y">
                {books.map((book) => (
                  <li key={book.itemId} className="flex items-center gap-3 py-3 pr-1">
                    <GiBlackBook className="text-muted-foreground size-5 shrink-0" aria-hidden />
                    <span className="min-w-0 flex-1 text-pretty">{book.name}</span>
                    <Button
                      size="sm"
                      variant="outline"
                      aria-label={`Read ${book.name}`}
                      onClick={() => setReadingItem(book)}
                    >
                      Read
                    </Button>
                  </li>
                ))}
              </ul>
            )}
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

function ShelfSkeleton() {
  return (
    <ul className="divide-border divide-y" aria-label="Loading books">
      {Array.from({ length: 5 }, (_, index) => (
        <li key={index} className="flex items-center gap-3 py-3 pr-1">
          <Skeleton className="size-5 shrink-0 rounded" />
          <Skeleton className="h-5 flex-1" />
          <Skeleton className="h-8 w-16" />
        </li>
      ))}
    </ul>
  );
}
