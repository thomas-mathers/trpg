import { useQueryClient } from '@tanstack/react-query';

import { getQuestJournalQueryKey } from '@/api/client';
import type { DeliverItemDialogResponse, ItemDetail } from '@/api/client';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useGameChat } from '@/features/game/hooks/use-game-chat';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';
import { ITEM_TYPE_LABEL } from '@/features/inventory/display-names';
import { RARITY_COLOR, TYPE_ICON } from '@/features/inventory/item-visuals';

export type DeliverItemDialogState = DeliverItemDialogResponse & {
  recipientId: string;
  worldId: string;
};

interface DeliverItemDialogProps {
  playerId: string;
  deliverable: DeliverItemDialogState | null;
  onClose: () => void;
}

export function DeliverItemDialog({ playerId, deliverable, onClose }: DeliverItemDialogProps) {
  const queryClient = useQueryClient();
  const chatHub = useChatHub();
  const { submitNarratedTurn, isStreaming } = useGameChat();

  if (!deliverable) {
    return null;
  }

  const invalidateJournal = () =>
    queryClient.invalidateQueries({
      queryKey: getQuestJournalQueryKey({
        path: { playerId },
        query: { worldId: deliverable.worldId },
      }),
    });

  const handleGive = () => {
    submitNarratedTurn(
      `Give the ${deliverable.item.name}`,
      chatHub.sendDeliverItem(deliverable.recipientId),
      undefined,
      invalidateJournal,
    );
    onClose();
  };

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Give Item</DialogTitle>
        </DialogHeader>
        <DialogDescription>
          You have something to hand over as part of &ldquo;{deliverable.questName}&rdquo;.
        </DialogDescription>

        <div className="bg-muted/40 rounded-md border p-3">
          <DeliverableItemPreview item={deliverable.item} />
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={isStreaming}>
            Not now
          </Button>
          <Button onClick={handleGive} disabled={isStreaming}>
            Give
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function DeliverableItemPreview({ item }: { item: ItemDetail }) {
  const rarityColor = item.rarity ? RARITY_COLOR[item.rarity] : undefined;
  const Icon = TYPE_ICON[item.type];

  return (
    <div className="flex items-center gap-1.5">
      <Icon className="text-muted-foreground mr-1 size-8 shrink-0" />
      <div className="min-w-0 flex-1">
        <p
          className="truncate font-semibold"
          style={rarityColor ? { color: rarityColor } : undefined}
          title={item.name}
        >
          {item.name}
        </p>
        <p className="text-muted-foreground text-xs">{ITEM_TYPE_LABEL[item.type]}</p>
        {item.description && (
          <p className="text-muted-foreground mt-1 text-xs italic">{item.description}</p>
        )}
      </div>
    </div>
  );
}
