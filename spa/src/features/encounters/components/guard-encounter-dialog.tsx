import { Coins, Lock, Shield, Swords } from 'lucide-react';

import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/components/ui/dialog';
import type { GuardEncounterActionName } from '@/features/encounters/encounter';
import { useGuardEncounterState } from '@/features/encounters/hooks/use-guard-encounter-state';
import { useGameChat } from '@/features/game/hooks/use-game-chat';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';
import { useDelayedReveal } from '@/hooks/use-delayed-reveal';

const ACTION_NAMES: readonly GuardEncounterActionName[] = ['PayFine', 'GoToJail', 'ResistArrest'];

function isGuardEncounterActionName(name: string): name is GuardEncounterActionName {
  return (ACTION_NAMES as readonly string[]).includes(name);
}

export function GuardEncounterDialog() {
  const encounter = useGuardEncounterState();
  const { isStreaming, submitNarratedTurn } = useGameChat();
  const chatHub = useChatHub();
  const isRevealed = useDelayedReveal(!!encounter && !isStreaming);

  const actionDetails: Record<
    GuardEncounterActionName,
    {
      label: string;
      description: string;
      icon: typeof Swords;
      submit: () => void;
    }
  > = {
    PayFine: {
      label: `Pay ${encounter?.fineAmount ?? 0} gold`,
      description: 'Settle the matter and clear your name.',
      icon: Coins,
      submit: () => submitNarratedTurn('Pay the fine', chatHub.resolvePayFineEncounterAction()),
    },
    GoToJail: {
      label: `Serve ${encounter?.jailHours ?? 0} hours`,
      description: 'Surrender and serve your sentence.',
      icon: Lock,
      submit: () => submitNarratedTurn('Go to jail', chatHub.resolveGoToJailEncounterAction()),
    },
    ResistArrest: {
      label: 'Resist arrest',
      description: 'Fight your way free.',
      icon: Swords,
      submit: () =>
        submitNarratedTurn('Resist arrest', chatHub.resolveResistArrestEncounterAction()),
    },
  };

  if (!encounter || !isRevealed) {
    return null;
  }

  return (
    <Dialog open onOpenChange={() => undefined}>
      <DialogContent
        showCloseButton={false}
        className="top-4 w-[min(100vw-2rem,42rem)] max-w-[calc(100%-2rem)] translate-y-0 gap-0 overflow-hidden p-0 shadow-2xl ring-2 ring-amber-500/40 sm:max-w-[42rem]"
      >
        <header className="bg-card border-b px-5 py-4">
          <DialogTitle className="flex items-center gap-2 text-base">
            <Shield className="text-stamina h-5 w-5" />
            Guard encounter
          </DialogTitle>
          <DialogDescription className="mt-1.5">
            {encounter.guardName} confronts you at {encounter.locationName}.
          </DialogDescription>
        </header>

        <div className="space-y-5 p-5">
          {encounter.recentOffenses.length > 0 && (
            <section aria-labelledby="guard-encounter-offenses">
              <h2
                id="guard-encounter-offenses"
                className="text-muted-foreground text-xs font-semibold tracking-wide uppercase"
              >
                Recent offenses
              </h2>
              <ul className="mt-2 divide-y rounded-lg border">
                {encounter.recentOffenses.map((offense, index) => (
                  <li key={`${offense}-${index}`} className="px-3 py-2.5 text-sm">
                    {offense}
                  </li>
                ))}
              </ul>
            </section>
          )}
          <div className="grid gap-2 sm:grid-cols-3">
            {encounter.allowedActions.map((actionName) => {
              if (!isGuardEncounterActionName(actionName)) {
                return null;
              }
              const details = actionDetails[actionName];
              const isDisabled =
                isStreaming || (actionName === 'PayFine' && !encounter.canAffordFine);

              const Icon = details.icon;
              return (
                <button
                  key={actionName}
                  type="button"
                  disabled={isDisabled}
                  onClick={() => details.submit()}
                  title={
                    actionName === 'PayFine' && !encounter.canAffordFine
                      ? "You don't have enough gold to pay this fine."
                      : undefined
                  }
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
