import { useQuery } from '@tanstack/react-query';

import { getSignTextOptions } from '@/api/client';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';

export interface SignDialogProps {
  worldId: string;
  signId: string | null;
  name: string;
  open: boolean;
  onClose: () => void;
}

export function SignDialog({ worldId, signId, name, open, onClose }: SignDialogProps) {
  const sign = useQuery({
    ...getSignTextOptions({ path: { signId: signId ?? '' }, query: { worldId } }),
    enabled: open && signId !== null,
  });

  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="flex max-h-[90vh] flex-col gap-4 md:max-w-lg">
        <DialogHeader>
          <DialogTitle>{name}</DialogTitle>
        </DialogHeader>

        <div className="min-h-0 flex-1 overflow-y-auto text-sm whitespace-pre-line">
          {sign.isLoading ? (
            <div className="flex flex-col gap-2">
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-4 w-3/4" />
            </div>
          ) : (
            sign.data?.text
          )}
        </div>

        <DialogFooter>
          <Button aria-label="Close sign" variant="outline" onClick={onClose}>
            Close
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
