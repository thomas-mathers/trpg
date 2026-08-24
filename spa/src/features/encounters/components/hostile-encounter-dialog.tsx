import { Footprints, ShieldAlert, Swords } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import type { EncounterActionName, HostileEncounterState } from '@/features/encounters/encounter';
import { useHostileEncounterState } from '@/features/encounters/hooks/use-hostile-encounter-state';
import { useGameChat } from '@/features/game/hooks/use-game-chat';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';
import { useDelayedReveal } from '@/hooks/use-delayed-reveal';

const ACTION_NAMES: readonly EncounterActionName[] = ['Attack', 'Evade', 'Retreat'];

function isEncounterActionName(name: string): name is EncounterActionName {
  return (ACTION_NAMES as readonly string[]).includes(name);
}

export function HostileEncounterDialog() {
  const encounter = useHostileEncounterState();
  const { isStreaming, submitNarratedTurn } = useGameChat();
  const chatHub = useChatHub();
  const isRevealed = useDelayedReveal(!!encounter && !isStreaming);

  const actionDetails: Record<
    EncounterActionName,
    { description: string; icon: typeof Swords; submit: (displayText: string) => void }
  > = {
    Attack: {
      description: 'Meet the threat head-on.',
      icon: Swords,
      submit: (displayText) =>
        submitNarratedTurn(displayText, chatHub.resolveAttackEncounterAction()),
    },
    Evade: {
      description: 'Try to slip past unnoticed.',
      icon: Footprints,
      submit: (displayText) =>
        submitNarratedTurn(displayText, chatHub.resolveEvadeEncounterAction()),
    },
    Retreat: {
      description: 'Fall back the way you came.',
      icon: ShieldAlert,
      submit: (displayText) =>
        submitNarratedTurn(displayText, chatHub.resolveRetreatEncounterAction()),
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
            <ShieldAlert className="text-stamina h-5 w-5" />
            Hostile encounter
          </DialogTitle>
        </DialogHeader>
        <DialogDescription className="px-5 pt-3">
          {encounter.factionName} confront you at {encounter.locationName}.
        </DialogDescription>

        <div className="space-y-5 p-5">
          <EncounterMembers encounter={encounter} />
          <div className="grid gap-2 sm:grid-cols-3">
            {encounter.allowedActions.map((actionName) => {
              if (!isEncounterActionName(actionName)) {
                return null;
              }
              const details = actionDetails[actionName];

              const Icon = details.icon;
              return (
                <button
                  key={actionName}
                  type="button"
                  disabled={isStreaming}
                  onClick={() => details.submit(actionName)}
                  className="border-border bg-card hover:bg-accent focus-visible:ring-ring flex min-h-24 flex-col items-start gap-2 rounded-lg border p-3 text-left shadow-sm transition-colors focus-visible:ring-2 focus-visible:outline-none disabled:pointer-events-none disabled:opacity-50"
                >
                  <Icon className="text-stamina h-5 w-5" />
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
