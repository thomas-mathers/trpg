import { Coins, Lock, Shield, Swords } from 'lucide-react';

import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/components/ui/dialog';
import type { GuardEncounterActionName } from '@/features/encounters/encounter';
import { useGuardEncounterState } from '@/features/encounters/hooks/use-guard-encounter-state';
import { useScene } from '@/features/game/contexts/scene-context';
import { useGameChat } from '@/features/game/hooks/use-game-chat';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';
import { useDelayedReveal } from '@/hooks/use-delayed-reveal';

export function GuardEncounterDialog() {
  const encounter = useGuardEncounterState();
  const scene = useScene();
  const { isStreaming, submitNarratedTurn } = useGameChat();
  const chatHub = useChatHub();
  const isRevealed = useDelayedReveal(!!encounter && !isStreaming);

  if (!encounter || !isRevealed) {
    return null;
  }

  const canAffordFine = (scene?.playerStatus.gold ?? 0) >= encounter.fineAmount;

  const actionDetails: Record<
    GuardEncounterActionName,
    {
      description: string;
      icon: typeof Shield;
      disabled?: boolean;
      submit: (displayText: string) => void;
    }
  > = {
    PayFine: {
      description: `Pay ${encounter.fineAmount} gold and part ways.`,
      icon: Coins,
      disabled: !canAffordFine,
      submit: (displayText) =>
        submitNarratedTurn(displayText, chatHub.resolvePayFineGuardEncounterAction()),
    },
    GoToJail: {
      description: `Submit to arrest for ${encounter.jailHours} hour(s).`,
      icon: Lock,
      submit: (displayText) =>
        submitNarratedTurn(displayText, chatHub.resolveGoToJailGuardEncounterAction()),
    },
    ResistArrest: {
      description: 'Fight your way free.',
      icon: Swords,
      submit: (displayText) =>
        submitNarratedTurn(displayText, chatHub.resolveResistArrestGuardEncounterAction()),
    },
  };

  return (
    <Dialog open onOpenChange={() => undefined}>
      <DialogContent
        showCloseButton={false}
        className="w-[min(100vw-2rem,42rem)] max-w-[calc(100%-2rem)] gap-0 overflow-hidden p-0 sm:max-w-[42rem]"
      >
        <header className="bg-card border-b px-5 py-4">
          <DialogTitle className="flex items-center gap-2 text-base">
            <Shield className="h-5 w-5 text-amber-500" />
            Guard encounter
          </DialogTitle>
          <DialogDescription className="mt-1.5">
            {encounter.guardName} stops you at {encounter.locationName}.
          </DialogDescription>
        </header>

        <div className="space-y-5 p-5">
          <RecentOffenses offenses={encounter.recentOffenses} />
          <div className="grid gap-2 sm:grid-cols-3">
            {(Object.keys(actionDetails) as GuardEncounterActionName[]).map((actionName) => {
              const details = actionDetails[actionName];
              const Icon = details.icon;
              return (
                <button
                  key={actionName}
                  type="button"
                  disabled={isStreaming || details.disabled}
                  onClick={() => details.submit(actionName)}
                  className="border-border bg-card hover:bg-accent focus-visible:ring-ring flex min-h-24 flex-col items-start gap-2 rounded-lg border p-3 text-left shadow-sm transition-colors focus-visible:ring-2 focus-visible:outline-none disabled:pointer-events-none disabled:opacity-50"
                >
                  <Icon className="h-5 w-5 text-amber-500" />
                  <span className="text-sm font-semibold">{formatActionName(actionName)}</span>
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

function formatActionName(actionName: GuardEncounterActionName): string {
  switch (actionName) {
    case 'PayFine':
      return 'Pay fine';
    case 'GoToJail':
      return 'Go to jail';
    case 'ResistArrest':
      return 'Resist arrest';
  }
}

function RecentOffenses({ offenses }: { offenses: readonly string[] }) {
  if (offenses.length === 0) {
    return null;
  }

  return (
    <section aria-labelledby="guard-encounter-offenses">
      <h2
        id="guard-encounter-offenses"
        className="text-muted-foreground text-xs font-semibold tracking-wide uppercase"
      >
        Recent offenses
      </h2>
      <ul className="mt-2 divide-y rounded-lg border">
        {offenses.map((offense, index) => (
          <li key={`${offense}-${index}`} className="px-3 py-2.5 text-sm">
            {offense}
          </li>
        ))}
      </ul>
    </section>
  );
}
