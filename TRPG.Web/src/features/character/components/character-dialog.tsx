import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

interface CharacterDialogProps {
  open: boolean;
  onClose: () => void;
}

export function CharacterDialog({ open, onClose }: CharacterDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent
        className="flex h-[min(94vh,880px)] flex-col gap-4 md:max-w-7xl"
        onPointerDownOutside={(event) => event.preventDefault()}
      >
        <CharacterDialogBody />
      </DialogContent>
    </Dialog>
  );
}

function CharacterDialogBody() {
  return (
    <>
      <DialogHeader>
        <DialogTitle>Character</DialogTitle>
      </DialogHeader>

      <DialogFooter className="flex-col items-stretch gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex gap-2">
          <Button variant="outline">Cancel</Button>
          <Button>Confirm</Button>
        </div>
      </DialogFooter>
    </>
  );
}
