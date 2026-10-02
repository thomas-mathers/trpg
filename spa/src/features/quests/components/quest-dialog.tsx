import { useQueryClient } from '@tanstack/react-query';

import { getQuestJournalQueryKey } from '@/api/client';
import type { QuestDialogResponse } from '@/api/client';
import type { ActionResult } from '@/api/signalr-client/TRPG.GameSessions.Responses';
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
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';
import { useCreatureInteraction } from '@/features/game/hooks/use-interaction-lifecycle';
import { parseNarrationMarkup } from '@/features/game/narration-markup';
import { useAction } from '@/features/game/run-action';

export type QuestDialogState = QuestDialogResponse & { giverId: string; worldId: string };

interface QuestDialogProps {
  playerId: string;
  quest: QuestDialogState | null;
  onClose: () => void;
}

export function QuestDialog({ playerId, quest, onClose }: QuestDialogProps) {
  const queryClient = useQueryClient();
  const chatHub = useChatHub();
  const { pending, run } = useAction();
  const { release } = useCreatureInteraction({
    playerId,
    worldId: quest?.worldId ?? '',
    creatureId: quest?.giverId,
  });

  if (!quest) {
    return null;
  }

  const isOffer = quest.mode === 'Offer';

  const invalidateJournal = () =>
    queryClient.invalidateQueries({
      queryKey: getQuestJournalQueryKey({
        path: { playerId },
        query: { worldId: quest.worldId },
      }),
    });

  const resolve = async (action: Promise<ActionResult>) => {
    const result = await run(action);
    if (result.succeeded) {
      await invalidateJournal();
    }
    onClose();
  };

  const handleAccept = async () => {
    await release();
    await resolve(chatHub.sendAcceptQuest(quest.questId));
  };

  const handleDecline = async () => {
    await release();
    onClose();
  };

  const handleComplete = async () => {
    await release();
    await resolve(chatHub.sendCompleteQuest(quest.questId));
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
              {quest.objectives.map((objective) => {
                const breakdown =
                  objective.itemNames && objective.itemNames.length > 1
                    ? objective.itemNames
                    : null;

                return (
                  <li key={objective.name}>
                    <NarrationText segments={parseNarrationMarkup(objective.description)} />{' '}
                    {!breakdown && objective.requiredAmount > 1 && `(${objective.requiredAmount})`}
                    {breakdown && (
                      <ul className="text-muted-foreground mt-1 list-disc space-y-0.5 pl-5 text-xs">
                        {breakdown.map((name) => (
                          <li key={name}>{name}</li>
                        ))}
                      </ul>
                    )}
                  </li>
                );
              })}
            </ul>
          </div>

          <p className="text-sm font-semibold">Reward: {quest.goldReward} gold</p>
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={isOffer ? () => void handleDecline() : onClose}
            disabled={pending}
          >
            Not now
          </Button>
          <Button
            onClick={() => void (isOffer ? handleAccept() : handleComplete())}
            disabled={pending}
          >
            {isOffer ? 'Accept quest' : 'Complete quest'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
