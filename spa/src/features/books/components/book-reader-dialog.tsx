import { useMutation } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, Loader2 } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';

import { readBookPageMutation } from '@/api/client';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

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

  const { mutate, isPending, isError } = useMutation({
    ...readBookPageMutation(),
    onSuccess: (data) => {
      setText(data.text ?? null);
      setPageCount(data.pageCount ?? null);
    },
  });

  const readPage = useCallback(
    (page: number) => {
      if (!itemId) return;
      setText(null);
      mutate({ path: { playerId, itemId, pageNumber: page } });
    },
    [itemId, mutate, playerId],
  );

  // A fresh open starts at page one; the first read is what reveals how long the book is.
  useEffect(() => {
    if (!open || !itemId) return;
    setPageNumber(1);
    setPageCount(null);
    readPage(1);
  }, [open, itemId, readPage]);

  const turnTo = (page: number) => {
    setPageNumber(page);
    readPage(page);
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
          {isPending && (
            <div
              className="text-muted-foreground flex h-full items-center justify-center"
              aria-live="polite"
            >
              <Loader2 className="mr-2 size-4 animate-spin" aria-hidden />
              Reading...
            </div>
          )}
          {!isPending && isError && (
            <p className="text-destructive">This page could not be read.</p>
          )}
          {!isPending && !isError && text !== null && (
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
              disabled={atStart || isPending}
              onClick={() => turnTo(pageNumber - 1)}
            >
              <ChevronLeft className="size-4" aria-hidden />
              Back
            </Button>
            <Button
              variant="outline"
              size="sm"
              aria-label="Next page"
              disabled={atEnd || isPending}
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
