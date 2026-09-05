import { Eye, Footprints, Handshake } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import type { SuspicionCause, SuspicionEncounterActionName } from '@/features/encounters/encounter';
import { useSuspicionEncounterState } from '@/features/encounters/hooks/use-suspicion-encounter-state';
import { useGameChat } from '@/features/game/hooks/use-game-chat';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';
import { useDelayedReveal } from '@/hooks/use-delayed-reveal';

const ACTION_NAMES: readonly SuspicionEncounterActionName[] = ['Comply', 'Flee'];

function isSuspicionEncounterActionName(name: string): name is SuspicionEncounterActionName {
  return (ACTION_NAMES as readonly string[]).includes(name);
}

const CAUSE_DESCRIPTIONS: Record<SuspicionCause, string> = {
  Sneaking: 'noticing you moving furtively, as if trying not to be seen',
  CastingMagicInPublic: 'catching you casting magic openly in public',
};

export function SuspicionEncounterDialog() {
  const encounter = useSuspicionEncounterState();
  const { isStreaming, submitNarratedTurn } = useGameChat();
  const chatHub = useChatHub();
  const isRevealed = useDelayedReveal(!!encounter && !isStreaming);

  const actionDetails: Record<
    SuspicionEncounterActionName,
    { label: string; description: string; icon: typeof Handshake; submit: () => void }
  > = {
    Comply: {
      label: 'Comply',
      description: 'Answer their questions and let them move on.',
      icon: Handshake,
      submit: () => submitNarratedTurn('Comply', chatHub.resolveComplySuspicionAction()),
    },
    Flee: {
      label: 'Flee',
      description: 'Bolt before they can question you further.',
      icon: Footprints,
      submit: () => submitNarratedTurn('Flee', chatHub.resolveFleeSuspicionAction()),
    },
  };

  if (!encounter || !isRevealed) {
    return null;
  }

  return (
    <Dialog open onOpenChange={() => undefined}>
      <DialogContent
        showCloseButton={false}
        className="ring-stamina/40 top-4 w-[min(100vw-2rem,42rem)] max-w-[calc(100%-2rem)] translate-y-0 gap-0 overflow-hidden p-0 shadow-2xl ring-2 sm:max-w-[42rem]"
      >
        <DialogHeader>
          <DialogTitle className="mx-0 mt-0 flex items-center gap-2 rounded-none px-5 py-4 text-base">
            <Eye className="text-stamina h-5 w-5" />
            Suspicion
          </DialogTitle>
        </DialogHeader>
        <DialogDescription className="px-5 pt-3">
          {encounter.guardName} stops you at {encounter.locationName},{' '}
          {CAUSE_DESCRIPTIONS[encounter.cause]}.
        </DialogDescription>

        <div className="space-y-5 p-5">
          <div className="grid gap-2 sm:grid-cols-2">
            {encounter.allowedActions.map((actionName) => {
              if (!isSuspicionEncounterActionName(actionName)) {
                return null;
              }
              const details = actionDetails[actionName];
              const Icon = details.icon;

              return (
                <button
                  key={actionName}
                  type="button"
                  disabled={isStreaming}
                  onClick={() => details.submit()}
                  className="border-border bg-card hover:bg-accent focus-visible:ring-ring flex min-h-24 flex-col items-start gap-2 rounded-lg border p-3 text-left shadow-sm transition-colors focus-visible:ring-2 focus-visible:outline-none disabled:pointer-events-none disabled:opacity-50"
                >
                  <Icon className="text-stamina h-5 w-5" />
                  <span className="text-sm font-semibold">{details.label}</span>
                  <span className="text-muted-foreground text-xs">{details.description}</span>
                </button>
              );
            })}
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
