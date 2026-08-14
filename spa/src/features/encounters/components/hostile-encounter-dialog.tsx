import { Footprints, ShieldAlert, Swords } from 'lucide-react';

import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/components/ui/dialog';
import { encounterActionByName, type HostileEncounterState } from '@/features/encounters/encounter';
import { useEncounterState } from '@/features/encounters/hooks/use-encounter-state';
import { useEncounterActions } from '@/features/game/game-chat-context';
import { useDelayedReveal } from '@/hooks/use-delayed-reveal';

const actionDetails: Record<string, { description: string; icon: typeof Swords }> = {
  Attack: { description: 'Meet the threat head-on.', icon: Swords },
  Evade: { description: 'Try to slip past unnoticed.', icon: Footprints },
  Retreat: { description: 'Fall back the way you came.', icon: ShieldAlert },
};

export function HostileEncounterDialog() {
  const encounter = useEncounterState();
  const { isStreaming, submitEncounterAction } = useEncounterActions();
  const isRevealed = useDelayedReveal(!!encounter && !isStreaming);

  if (!encounter || !isRevealed) {
    return null;
  }

  return (
    <Dialog open onOpenChange={() => undefined}>
      <DialogContent
        showCloseButton={false}
        className="w-[min(100vw-2rem,42rem)] max-w-[calc(100%-2rem)] gap-0 overflow-hidden p-0 sm:max-w-[42rem]"
      >
        <header className="bg-card border-b px-5 py-4">
          <DialogTitle className="flex items-center gap-2 text-base">
            <ShieldAlert className="h-5 w-5 text-amber-500" />
            Hostile encounter
          </DialogTitle>
          <DialogDescription className="mt-1.5">
            {encounter.factionName} confront you at {encounter.locationName}.
          </DialogDescription>
        </header>

        <div className="space-y-5 p-5">
          <EncounterMembers encounter={encounter} />
          <div className="grid gap-2 sm:grid-cols-3">
            {encounter.allowedActions.map((actionName) => {
              const action = encounterActionByName[actionName];
              const details = actionDetails[actionName];
              if (!action || !details) {
                return null;
              }

              const Icon = details.icon;
              return (
                <button
                  key={actionName}
                  type="button"
                  disabled={isStreaming}
                  onClick={() => submitEncounterAction(action, actionName)}
                  className="border-border bg-card hover:bg-accent focus-visible:ring-ring flex min-h-24 flex-col items-start gap-2 rounded-lg border p-3 text-left shadow-sm transition-colors focus-visible:ring-2 focus-visible:outline-none disabled:pointer-events-none disabled:opacity-50"
                >
                  <Icon className="h-5 w-5 text-amber-500" />
                  <span className="text-sm font-semibold">{actionName}</span>
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

function EncounterMembers({ encounter }: { encounter: HostileEncounterState }) {
  return (
    <section aria-labelledby="encounter-members">
      <h2
        id="encounter-members"
        className="text-muted-foreground text-xs font-semibold tracking-wide uppercase"
      >
        Threats
      </h2>
      <ul className="mt-2 divide-y rounded-lg border">
        {encounter.members.map((member, index) => (
          <li
            key={`${member.name}-${index}`}
            className="flex items-center justify-between px-3 py-2.5 text-sm"
          >
            <span className="font-medium">{member.name}</span>
            <span className="text-muted-foreground">
              {member.creatureType} · Lv {member.level}
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}
