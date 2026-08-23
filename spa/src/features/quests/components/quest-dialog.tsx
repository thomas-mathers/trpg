import { useMutation, useQueryClient } from '@tanstack/react-query';

import { acceptQuestMutation, completeQuestMutation, getQuestJournalQueryKey } from '@/api/client';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { NarrationText } from '@/features/game/components/narration-text';
import { parseNarrationMarkup } from '@/features/game/narration-markup';
import type { QuestDialogRequested } from '@/lib/game-event-bus';

interface QuestDialogProps {
  playerId: string;
  quest: QuestDialogRequested | null;
  onClose: () => void;
}

export function QuestDialog({ playerId, quest, onClose }: QuestDialogProps) {
  const queryClient = useQueryClient();
  const acceptQuest = useMutation(acceptQuestMutation());
  const completeQuest = useMutation(completeQuestMutation());

  if (!quest) {
    return null;
  }

  const isOffer = quest.mode === 'Offer';
  const mutation = isOffer ? acceptQuest : completeQuest;
  const handleConfirm = async () => {
    await mutation.mutateAsync({
      path: { playerId, questId: quest.questId },
      query: { worldId: quest.worldId },
    });
    await queryClient.invalidateQueries({
      queryKey: getQuestJournalQueryKey({
        path: { playerId },
        query: { worldId: quest.worldId },
      }),
    });
    onClose();
  };

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{quest.name}</DialogTitle>
        </DialogHeader>
        <DialogDescription>
          <NarrationText segments={parseNarrationMarkup(quest.description)} />
        </DialogDescription>

        <div className="space-y-3">
          <div>
            <h3 className="text-sm font-semibold">Objectives</h3>
            <ul className="mt-1 list-disc space-y-1 pl-5 text-sm">
              {quest.objectives.map((objective) => (
                <li key={objective.name}>
                  <NarrationText segments={parseNarrationMarkup(objective.description)} />{' '}
                  {objective.requiredAmount > 1 && `(${objective.requiredAmount})`}
                </li>
              ))}
            </ul>
          </div>

          <p className="text-sm font-semibold">Reward: {quest.goldReward} gold</p>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={mutation.isPending}>
            Not now
          </Button>
          <Button onClick={handleConfirm} disabled={mutation.isPending}>
            {isOffer ? 'Accept quest' : 'Complete quest'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
