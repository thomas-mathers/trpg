import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Circle, CircleCheck, MapPin, ScrollText } from 'lucide-react';
import { useState } from 'react';

import {
  getQuestJournalOptions,
  getQuestJournalQueryKey,
  setQuestTrackingMutation,
  type QuestJournalEntrySnapshot,
} from '@/api/client';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Empty, EmptyMedia, EmptyTitle } from '@/components/ui/empty';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { NarrationText } from '@/features/game/components/narration-text';
import { parseNarrationMarkup } from '@/features/game/narration-markup';

interface QuestJournalDialogProps {
  playerId: string;
  worldId: string;
  open: boolean;
  onClose: () => void;
}

export function QuestJournalDialog({ playerId, worldId, open, onClose }: QuestJournalDialogProps) {
  const [tab, setTab] = useState('active');
  const [selectedQuestId, setSelectedQuestId] = useState<string>();
  const journal = useQuery({
    ...getQuestJournalOptions({ path: { playerId }, query: { worldId } }),
    enabled: open,
  });
  const activeQuests = journal.data?.filter((quest) => quest.status !== 'Completed') ?? [];
  const completedQuests = journal.data?.filter((quest) => quest.status === 'Completed') ?? [];
  const quests = tab === 'active' ? activeQuests : completedQuests;
  const selectedQuest = quests.find((quest) => quest.id === selectedQuestId) ?? quests[0];

  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="flex h-[min(90dvh,44rem)] w-[calc(100vw-2rem)] max-w-none flex-col gap-4 sm:h-[min(88dvh,56rem)] sm:w-[min(92vw,72rem)] sm:max-w-none">
        <DialogHeader>
          <DialogTitle>Quest Journal</DialogTitle>
        </DialogHeader>
        <DialogDescription>Your current work and its progress.</DialogDescription>

        <Tabs value={tab} onValueChange={setTab} className="min-h-0 flex-1">
          <TabsList>
            <TabsTrigger value="active">Active ({activeQuests.length})</TabsTrigger>
            <TabsTrigger value="completed">Completed ({completedQuests.length})</TabsTrigger>
          </TabsList>
          <TabsContent value="active" className="mt-4 min-h-0 overflow-hidden">
            <QuestJournalContent
              emptyMessage="You have no active quests."
              loading={journal.isLoading}
              playerId={playerId}
              worldId={worldId}
              quests={quests}
              selectedQuest={selectedQuest}
              onSelectQuest={setSelectedQuestId}
            />
          </TabsContent>
          <TabsContent value="completed" className="mt-4 min-h-0 overflow-hidden">
            <QuestJournalContent
              emptyMessage="You have not completed any quests."
              loading={journal.isLoading}
              playerId={playerId}
              worldId={worldId}
              quests={quests}
              selectedQuest={selectedQuest}
              onSelectQuest={setSelectedQuestId}
            />
          </TabsContent>
        </Tabs>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Close
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function QuestJournalContent({
  emptyMessage,
  loading,
  playerId,
  worldId,
  quests,
  selectedQuest,
  onSelectQuest,
}: {
  emptyMessage: string;
  loading: boolean;
  playerId: string;
  worldId: string;
  quests: readonly QuestJournalEntrySnapshot[];
  selectedQuest: QuestJournalEntrySnapshot | undefined;
  onSelectQuest: (questId: string) => void;
}) {
  if (loading) {
    return (
      <div className="bg-muted/40 grid h-full min-h-0 overflow-hidden rounded-lg border md:grid-cols-[18rem_minmax(0,1fr)]">
        <div className="max-h-40 space-y-2 overflow-hidden border-b p-2 md:max-h-none md:border-r md:border-b-0">
          {Array.from({ length: 4 }, (_, index) => (
            <Skeleton key={index} className="h-11 w-full rounded-md" />
          ))}
        </div>
        <div className="bg-card min-h-0 p-5 shadow-sm">
          <Skeleton className="h-6 w-2/3" />
          <Skeleton className="mt-4 h-4 w-full" />
          <Skeleton className="mt-2 h-4 w-5/6" />
        </div>
      </div>
    );
  }

  if (quests.length === 0) {
    return <QuestJournalEmptyState message={emptyMessage} />;
  }

  return (
    <div className="bg-muted/40 grid h-full min-h-0 overflow-hidden rounded-lg border md:grid-cols-[18rem_minmax(0,1fr)]">
      <nav className="max-h-40 space-y-1 overflow-y-auto border-b p-2 md:max-h-none md:border-r md:border-b-0">
        {quests.map((quest) => (
          <QuestJournalListItem
            key={quest.id}
            quest={quest}
            selected={quest.id === selectedQuest?.id}
            onSelect={() => onSelectQuest(quest.id)}
          />
        ))}
      </nav>
      <div className="bg-card min-h-0 overflow-y-auto p-5 shadow-sm">
        {selectedQuest && (
          <QuestJournalEntry playerId={playerId} worldId={worldId} quest={selectedQuest} />
        )}
      </div>
    </div>
  );
}

function QuestJournalListItem({
  quest,
  selected,
  onSelect,
}: {
  quest: QuestJournalEntrySnapshot;
  selected: boolean;
  onSelect: () => void;
}) {
  const locationName = getQuestLocationName(quest);

  return (
    <button
      type="button"
      className={`w-full rounded-md px-3 py-2.5 text-left transition-colors ${
        selected
          ? 'bg-primary text-primary-foreground shadow-sm'
          : 'hover:bg-background/80 hover:shadow-sm'
      }`}
      onClick={onSelect}
    >
      <span className="font-heading block truncate tracking-wide">{quest.name}</span>
      {locationName && (
        <span
          className={`mt-1 flex items-center gap-1 text-xs ${
            selected ? 'text-primary-foreground/70' : 'text-muted-foreground'
          }`}
        >
          <MapPin className="size-3 shrink-0" />
          <span className="truncate">{locationName}</span>
        </span>
      )}
    </button>
  );
}

function QuestJournalEmptyState({ message }: { message: string }) {
  return (
    <Empty className="h-full border">
      <EmptyMedia variant="icon">
        <ScrollText />
      </EmptyMedia>
      <EmptyTitle>{message}</EmptyTitle>
    </Empty>
  );
}

function QuestJournalEntry({
  playerId,
  worldId,
  quest,
}: {
  playerId: string;
  worldId: string;
  quest: QuestJournalEntrySnapshot;
}) {
  const queryClient = useQueryClient();
  const setTracking = useMutation(setQuestTrackingMutation());
  const toggleTracking = async () => {
    await setTracking.mutateAsync({
      path: { playerId, questId: quest.id },
      query: { worldId },
      body: { isTracked: !quest.isTracked },
    });
    await queryClient.invalidateQueries({
      queryKey: getQuestJournalQueryKey({ path: { playerId }, query: { worldId } }),
    });
  };

  return (
    <section className="space-y-5">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h3 className="font-heading text-xl tracking-wide">{quest.name}</h3>
          <p className="text-muted-foreground mt-1 text-xs">{formatQuestStatus(quest.status)}</p>
        </div>
        {(quest.status === 'Accepted' || quest.status === 'ReadyToComplete') && (
          <Button
            variant="outline"
            size="sm"
            onClick={toggleTracking}
            disabled={setTracking.isPending}
          >
            {quest.isTracked ? 'Untrack' : 'Track'}
          </Button>
        )}
      </div>
      <p className="text-muted-foreground border-y py-4 text-sm leading-6">
        <NarrationText segments={parseNarrationMarkup(quest.description)} />
      </p>
      <div className="space-y-2">
        <h4 className="text-muted-foreground text-xs font-medium tracking-wider uppercase">
          Objectives
        </h4>
        <ul className="space-y-2 text-sm">
          {quest.objectives.map((objective) => (
            <li
              key={objective.name}
              className="bg-muted/30 flex items-start gap-2 rounded-md border px-3 py-2"
            >
              <span className="mt-0.5 flex size-4 shrink-0 items-center justify-center" aria-hidden>
                {objective.amount >= objective.requiredAmount ? (
                  <CircleCheck className="text-heal size-4" />
                ) : (
                  <Circle className="text-muted-foreground size-3" />
                )}
              </span>
              <span>
                <NarrationText segments={parseNarrationMarkup(objective.description)} /> (
                {objective.amount}/{objective.requiredAmount})
                {objective.locationName && (
                  <span className="text-muted-foreground mt-1 flex items-center gap-1 text-xs">
                    <MapPin className="size-3" />
                    {objective.locationName}
                  </span>
                )}
              </span>
            </li>
          ))}
        </ul>
      </div>
      <p className="border-t pt-4 text-sm font-medium">Reward: {quest.goldReward} gold</p>
    </section>
  );
}

function formatQuestStatus(status: string) {
  return status === 'ReadyToComplete' ? 'Ready to turn in' : status;
}

function getQuestLocationName(quest: QuestJournalEntrySnapshot) {
  return quest.objectives.find((objective) => objective.locationName)?.locationName;
}
