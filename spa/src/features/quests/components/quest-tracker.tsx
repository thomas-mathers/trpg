import { useQuery } from '@tanstack/react-query';
import { CircleCheck } from 'lucide-react';

import { getQuestJournalOptions } from '@/api/client';
import { Button } from '@/components/ui/button';

interface QuestTrackerProps {
  playerId: string;
  worldId: string;
  onOpenJournal: () => void;
}

export function QuestTracker({ playerId, worldId, onOpenJournal }: QuestTrackerProps) {
  const journal = useQuery(getQuestJournalOptions({ path: { playerId }, query: { worldId } }));
  const tracked = journal.data?.filter((quest) => quest.isTracked) ?? [];
  const quest = tracked.find((entry) => entry.status === 'ReadyToComplete') ?? tracked[0];

  if (!quest) {
    return null;
  }

  return (
    <section>
      <p className="text-muted-foreground text-[11px] font-semibold tracking-wider uppercase">
        Tracked Quest
      </p>
      <button type="button" className="mt-1 text-left font-medium" onClick={onOpenJournal}>
        {quest.name}
      </button>
      <ul className="text-muted-foreground mt-1 space-y-1 text-xs">
        {quest.objectives.map((objective) => (
          <li key={objective.name} className="flex items-center gap-1.5">
            <span className="flex size-3 shrink-0 items-center justify-center" aria-hidden>
              {objective.amount >= objective.requiredAmount ? (
                <CircleCheck className="size-3 text-emerald-600 dark:text-emerald-400" />
              ) : (
                <span>•</span>
              )}
            </span>
            <span>
              {objective.name} ({objective.amount}/{objective.requiredAmount})
            </span>
          </li>
        ))}
      </ul>
      <Button variant="link" size="xs" className="mt-1 px-0" onClick={onOpenJournal}>
        {tracked.length > 1
          ? `${tracked.length - 1} more tracked quest${tracked.length === 2 ? '' : 's'}`
          : 'Open journal'}
      </Button>
    </section>
  );
}
