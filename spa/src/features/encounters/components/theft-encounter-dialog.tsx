import { Handshake, ShieldAlert, Swords } from 'lucide-react';

import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/components/ui/dialog';
import type { TheftEncounterActionName } from '@/features/encounters/encounter';
import { useTheftEncounterState } from '@/features/encounters/hooks/use-theft-encounter-state';
import { useGameChat } from '@/features/game/hooks/use-game-chat';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';
import { useDelayedReveal } from '@/hooks/use-delayed-reveal';

const ACTION_NAMES: readonly TheftEncounterActionName[] = ['Apologize', 'Fight'];

function isTheftEncounterActionName(name: string): name is TheftEncounterActionName {
  return (ACTION_NAMES as readonly string[]).includes(name);
}

export function TheftEncounterDialog() {
  const encounter = useTheftEncounterState();
  const { isStreaming, submitNarratedTurn } = useGameChat();
  const chatHub = useChatHub();
  const isRevealed = useDelayedReveal(!!encounter && !isStreaming);

  const actionDetails: Record<
    TheftEncounterActionName,
    { label: string; description: string; icon: typeof Swords; submit: () => void }
  > = {
    Apologize: {
      label: 'Apologize',
      description: 'Accept responsibility and try to make amends.',
      icon: Handshake,
      submit: () => submitNarratedTurn('Apologize', chatHub.resolveApologizeTheftEncounterAction()),
    },
    Fight: {
      label: 'Fight',
      description: 'Refuse to yield and fight your way out.',
      icon: Swords,
      submit: () => submitNarratedTurn('Fight', chatHub.resolveFightTheftEncounterAction()),
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
        <header className="chrome-surface text-chrome-foreground chrome-scope rounded-t-xl px-5 py-4">
          <DialogTitle className="flex items-center gap-2 text-base">
            <ShieldAlert className="text-stamina h-5 w-5" />
            Theft encounter
          </DialogTitle>
          <DialogDescription className="mt-1.5">
            {encounter.confrontingName} catches you trying to steal.
          </DialogDescription>
        </header>

        <div className="space-y-5 p-5">
          {encounter.itemNames.length > 0 && (
            <section aria-labelledby="theft-encounter-items">
              <h2
                id="theft-encounter-items"
                className="text-muted-foreground text-xs font-semibold tracking-wide uppercase"
              >
                Items involved
              </h2>
              <ul className="mt-2 divide-y rounded-lg border">
                {encounter.itemNames.map((itemName, index) => (
                  <li key={`${itemName}-${index}`} className="px-3 py-2.5 text-sm">
                    {itemName}
                  </li>
                ))}
              </ul>
            </section>
          )}
          <div className="grid gap-2 sm:grid-cols-2">
            {encounter.allowedActions.map((actionName) => {
              if (!isTheftEncounterActionName(actionName)) {
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
