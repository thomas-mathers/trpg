import { Coins, Footprints, MessageSquareWarning, ShieldAlert, Swords } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import type {
  ShakedownEncounterActionName,
  ShakedownEncounterState,
} from '@/features/encounters/encounter';
import { useShakedownEncounterState } from '@/features/encounters/hooks/use-shakedown-encounter-state';
import { useGameChat } from '@/features/game/hooks/use-game-chat';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';
import { useDelayedReveal } from '@/hooks/use-delayed-reveal';

const ACTION_NAMES: readonly ShakedownEncounterActionName[] = [
  'Intimidate',
  'PayToll',
  'Fight',
  'Flee',
];

function isShakedownEncounterActionName(name: string): name is ShakedownEncounterActionName {
  return (ACTION_NAMES as readonly string[]).includes(name);
}

export function ShakedownEncounterDialog() {
  const encounter = useShakedownEncounterState();
  const { isStreaming, submitNarratedTurn } = useGameChat();
  const chatHub = useChatHub();
  const isRevealed = useDelayedReveal(!!encounter && !isStreaming);

  const actionDetails: Record<
    ShakedownEncounterActionName,
    { label: string; description: string; icon: typeof Swords; submit: () => void }
  > = {
    Intimidate: {
      label: 'Intimidate',
      description: 'Try to make the bandits back down.',
      icon: MessageSquareWarning,
      submit: () =>
        submitNarratedTurn('Intimidate the bandits', chatHub.resolveIntimidateEncounterAction()),
    },
    PayToll: {
      label: `Pay ${encounter?.tollAmount ?? 0} gold`,
      description: 'Pay their demand and continue on your way.',
      icon: Coins,
      submit: () => submitNarratedTurn('Pay the toll', chatHub.resolvePayTollEncounterAction()),
    },
    Fight: {
      label: 'Fight',
      description: 'Refuse their demand with steel.',
      icon: Swords,
      submit: () => submitNarratedTurn('Fight the bandits', chatHub.resolveFightEncounterAction()),
    },
    Flee: {
      label: 'Flee',
      description: 'Try to escape before they surround you.',
      icon: Footprints,
      submit: () =>
        submitNarratedTurn('Flee from the bandits', chatHub.resolveFleeShakedownEncounterAction()),
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
            Shakedown
          </DialogTitle>
        </DialogHeader>
        <DialogDescription className="px-5 pt-3">
          {encounter.factionName} block your path at {encounter.locationName} and demand{' '}
          {encounter.tollAmount} gold.
        </DialogDescription>

        <div className="space-y-5 p-5">
          <EncounterMembers encounter={encounter} />
          <div className="grid gap-2 sm:grid-cols-2">
            {encounter.allowedActions.map((actionName) => {
              if (!isShakedownEncounterActionName(actionName)) {
                return null;
              }
              const details = actionDetails[actionName];
              const cannotAffordToll = actionName === 'PayToll' && !encounter.canAffordToll;
              const Icon = details.icon;

              return (
                <button
                  key={actionName}
                  type="button"
                  disabled={isStreaming || cannotAffordToll}
                  onClick={details.submit}
                  title={
                    cannotAffordToll ? "You don't have enough gold to pay this toll." : undefined
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

function EncounterMembers({ encounter }: { encounter: ShakedownEncounterState }) {
  return (
    <section aria-labelledby="shakedown-encounter-members">
      <h2
        id="shakedown-encounter-members"
        className="text-muted-foreground text-xs font-semibold tracking-wide uppercase"
      >
        Bandits
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
