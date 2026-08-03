import { DoorOpen, Skull, Trophy } from 'lucide-react';

import type { CombatOutcome } from '@/lib/combat-outcome';

interface CombatOutcomeScreenProps {
  outcome: CombatOutcome;
  onContinue: () => void;
  onExitToMenu: () => void;
}

// Defeat means the player's Creature is now permanently dead (see ChatHub.IsPlayerDead) - there's
// nothing to "continue" back into the way there is for Victory/Fled, so it gets its own terminal
// treatment instead of just different copy on the same continue-and-resume flow.
export function CombatOutcomeScreen({
  outcome,
  onContinue,
  onExitToMenu,
}: CombatOutcomeScreenProps) {
  if (outcome === 'Defeat') {
    return (
      <div className="flex flex-col items-center gap-2.5 px-4 py-6 text-center">
        <Skull className="text-destructive h-8 w-8" />
        <span className="text-[15px] font-bold">You have died</span>
        <span className="text-muted-foreground max-w-[46ch] text-xs">
          This adventure has come to an end.
        </span>
        <button
          type="button"
          onClick={onExitToMenu}
          className="bg-primary text-primary-foreground mt-1 rounded-md px-4.5 py-1.5 text-[13px] font-semibold hover:opacity-90"
        >
          Return to Main Menu
        </button>
      </div>
    );
  }

  const { Icon, iconClass, title, subtitle } =
    outcome === 'Victory'
      ? {
          Icon: Trophy,
          iconClass: 'text-green-500',
          title: 'Victory!',
          subtitle: 'The battle is won.',
        }
      : {
          Icon: DoorOpen,
          iconClass: 'text-muted-foreground',
          title: 'Escaped',
          subtitle: 'You break away from the fight.',
        };

  return (
    <div className="flex flex-col items-center gap-2.5 px-4 py-6 text-center">
      <Icon className={`h-8 w-8 ${iconClass}`} />
      <span className="text-[15px] font-bold">{title}</span>
      <span className="text-muted-foreground max-w-[46ch] text-xs">{subtitle}</span>
      <button
        type="button"
        onClick={onContinue}
        className="bg-primary text-primary-foreground mt-1 rounded-md px-4.5 py-1.5 text-[13px] font-semibold hover:opacity-90"
      >
        Continue
      </button>
    </div>
  );
}
