import { useMutation } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, Loader2 } from 'lucide-react';
import { useCallback, useEffect, useRef, useState } from 'react';

import { prefetchBookPage, readBookPageMutation } from '@/api/client';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

const PAGES_WARMED_AHEAD = 2;

export interface BookReaderDialogProps {
  playerId: string;
  itemId: string | null;
  title: string;
  open: boolean;
  onClose: () => void;
}

export function BookReaderDialog({
  playerId,
  itemId,
  title,
  open,
  onClose,
}: BookReaderDialogProps) {
  const [pageNumber, setPageNumber] = useState(1);
  const [pageCount, setPageCount] = useState<number | null>(null);
  const [text, setText] = useState<string | null>(null);
  // Covers waiting on a warm-up as well as the request itself, which the mutation cannot see.
  const [isReading, setIsReading] = useState(false);
  const warming = useRef(new Map<number, Promise<unknown>>());

  const { mutate, isError } = useMutation({
    ...readBookPageMutation(),
    onSuccess: (data) => {
      setText(data.text ?? null);
      setPageCount(data.pageCount ?? null);
      if (itemId && data.pageCount != null && data.pageNumber != null) {
        warmAhead(warming.current, itemId, data.pageNumber + 1, data.pageCount);
      }
    },
    onSettled: () => setIsReading(false),
  });

  const readPage = useCallback(
    async (page: number) => {
      if (!itemId) return;
      setText(null);
      setIsReading(true);
      // Turning forward before the warm-up lands would compose the page twice, and one of the two
      // writes would then lose to the other on the way into the database.
      await warming.current.get(page);
      mutate({ path: { playerId, itemId, pageNumber: page } });
    },
    [itemId, mutate, playerId],
  );

  // A fresh open starts at page one; the first read is what reveals how long the book is.
  useEffect(() => {
    if (!open || !itemId) return;
    setPageNumber(1);
    setPageCount(null);
    warming.current.clear();
    void readPage(1);
  }, [open, itemId, readPage]);

  const turnTo = (page: number) => {
    setPageNumber(page);
    void readPage(page);
  };

  const atStart = pageNumber <= 1;
  const atEnd = pageCount !== null && pageNumber >= pageCount;

  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="flex h-[min(90vh,760px)] flex-col gap-4 md:max-w-3xl">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
        </DialogHeader>

        <div className="bg-card min-h-0 flex-1 overflow-y-auto rounded-md border px-6 py-5">
          {isReading && (
            <div
              className="text-muted-foreground flex h-full items-center justify-center"
              aria-live="polite"
            >
              <Loader2 className="mr-2 size-4 animate-spin" aria-hidden />
              Reading...
            </div>
          )}
          {!isReading && isError && (
            <p className="text-destructive">This page could not be read.</p>
          )}
          {!isReading && !isError && text !== null && (
            <div className="space-y-4 leading-relaxed text-pretty whitespace-pre-wrap">{text}</div>
          )}
        </div>

        <DialogFooter className="items-center justify-between sm:justify-between">
          <span className="text-muted-foreground text-sm tabular-nums">
            {pageCount === null ? `Page ${pageNumber}` : `Page ${pageNumber} of ${pageCount}`}
          </span>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              aria-label="Previous page"
              disabled={atStart || isReading}
              onClick={() => turnTo(pageNumber - 1)}
            >
              <ChevronLeft className="size-4" aria-hidden />
              Back
            </Button>
            <Button
              variant="outline"
              size="sm"
              aria-label="Next page"
              disabled={atEnd || isReading}
              onClick={() => turnTo(pageNumber + 1)}
            >
              Next
              <ChevronRight className="size-4" aria-hidden />
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// Composed while the reader is still on an earlier page, so turning forward is instant. This
// deliberately does not go through the read endpoint, which would teach a secret unread.
function warmAhead(
  warming: Map<number, Promise<unknown>>,
  itemId: string,
  pageNumber: number,
  pageCount: number,
  remaining = PAGES_WARMED_AHEAD,
) {
  if (remaining <= 0 || pageNumber > pageCount || warming.has(pageNumber)) return;

  const request = prefetchBookPage({ path: { itemId, pageNumber } })
    .catch(() => {
      // A failed warm-up costs nothing; the page composes when the reader turns to it.
    })
    .finally(() => warming.delete(pageNumber));

  warming.set(pageNumber, request);

  // Chained rather than fired together, because a page is written from the ones before it and has
  // nothing to continue from until they exist.
  void request.then(() => warmAhead(warming, itemId, pageNumber + 1, pageCount, remaining - 1));
}
