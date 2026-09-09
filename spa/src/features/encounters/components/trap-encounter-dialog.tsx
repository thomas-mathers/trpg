import { AlertTriangle, Footprints, Undo2, Wrench } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import type { TrapEncounterActionName, TrapKind } from '@/features/encounters/encounter';
import { useTrapEncounterState } from '@/features/encounters/hooks/use-trap-encounter-state';
import { useGameChat } from '@/features/game/hooks/use-game-chat';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';
import { useDelayedReveal } from '@/hooks/use-delayed-reveal';

const ACTION_NAMES: readonly TrapEncounterActionName[] = ['Attempt', 'Withdraw', 'Disarm'];

function isTrapEncounterActionName(name: string): name is TrapEncounterActionName {
  return (ACTION_NAMES as readonly string[]).includes(name);
}

const TRAP_KIND_DESCRIPTIONS: Record<TrapKind, string> = {
  Mechanical: 'a trapdoor mechanism, waiting for a wrong step',
  Collapse: 'a section of floor that looks ready to give way',
  Slope: 'loose, treacherous footing',
  Water: 'a current strong enough to sweep you off your feet',
};

export function TrapEncounterDialog() {
  const encounter = useTrapEncounterState();
  const { isStreaming, submitNarratedTurn } = useGameChat();
  const chatHub = useChatHub();
  const isRevealed = useDelayedReveal(!!encounter && !isStreaming);

  const actionDetails: Record<
    TrapEncounterActionName,
    { label: string; description: string; icon: typeof Footprints; submit: () => void }
  > = {
    Attempt: {
      label: 'Attempt',
      description: 'Try to get past it carefully.',
      icon: Footprints,
      submit: () => submitNarratedTurn('Attempt', chatHub.resolveAttemptTrapAction()),
    },
    Withdraw: {
      label: 'Withdraw',
      description: 'Step back and leave it alone.',
      icon: Undo2,
      submit: () => submitNarratedTurn('Withdraw', chatHub.resolveWithdrawTrapAction()),
    },
    Disarm: {
      label: 'Disarm',
      description: 'Carefully disable the mechanism.',
      icon: Wrench,
      submit: () => submitNarratedTurn('Disarm', chatHub.resolveDisarmTrapAction()),
    },
  };

  if (!encounter || !isRevealed) {
    return null;
  }

  return (
    <Dialog open onOpenChange={() => undefined}>
      <DialogContent
        showCloseButton={false}
        className="ring-destructive/40 top-4 w-[min(100vw-2rem,42rem)] max-w-[calc(100%-2rem)] translate-y-0 gap-0 overflow-hidden p-0 shadow-2xl ring-2 sm:max-w-[42rem]"
      >
        <DialogHeader>
          <DialogTitle className="mx-0 mt-0 flex items-center gap-2 rounded-none px-5 py-4 text-base">
            <AlertTriangle className="text-destructive h-5 w-5" />
            Trap
          </DialogTitle>
        </DialogHeader>
        <DialogDescription className="px-5 pt-3">
          {encounter.locationName ? `Here in ${encounter.locationName}, y` : 'Y'}ou notice{' '}
          {TRAP_KIND_DESCRIPTIONS[encounter.trapKind]}.
        </DialogDescription>

        <div className="space-y-5 p-5">
          <div className="grid gap-2 sm:grid-cols-2">
            {encounter.allowedActions.map((actionName) => {
              if (!isTrapEncounterActionName(actionName)) {
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
                  <Icon className="text-destructive h-5 w-5" />
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
