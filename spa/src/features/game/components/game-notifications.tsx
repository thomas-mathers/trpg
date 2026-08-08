import { Award, BookOpen, Trophy } from 'lucide-react';
import { useEffect } from 'react';
import { toast } from 'sonner';

import { Toaster } from '@/components/ui/sonner';
import { gameEventBus } from '@/lib/game-event-bus';

import { GameToast } from './game-toast';

export function GameNotifications() {
  useEffect(() => {
    const unsubscribeSkill = gameEventBus.on('SkillLevelUp', ({ skill, level }) => {
      toast.custom(
        (toastId) => (
          <GameToast
            toastId={toastId}
            icon={BookOpen}
            title="Skill level up"
            description={`${skill} reached level ${level}.`}
          />
        ),
        { duration: 3200 },
      );
    });
    const unsubscribeCharacter = gameEventBus.on('CharacterLevelUp', ({ level }) => {
      toast.custom(
        (toastId) => (
          <GameToast
            toastId={toastId}
            icon={Award}
            title="Character level up"
            description={`You reached level ${level}.`}
          />
        ),
        { duration: 3800 },
      );
    });
    const unsubscribeCombatEnded = gameEventBus.on('CombatEnded', (outcome) => {
      if (outcome !== 'Victory') {
        return;
      }

      toast.custom(
        (toastId) => (
          <GameToast
            toastId={toastId}
            icon={Trophy}
            title="Victory"
            description="Your enemies have been defeated."
          />
        ),
        { duration: 3800 },
      );
    });
    return () => {
      unsubscribeSkill();
      unsubscribeCharacter();
      unsubscribeCombatEnded();
    };
  }, []);

  return <Toaster position="top-center" visibleToasts={3} />;
}
