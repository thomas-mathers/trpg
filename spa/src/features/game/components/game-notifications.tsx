import { Award, BookOpen, CircleCheck, Eye, ShieldOff, Skull, Trophy, XCircle } from 'lucide-react';
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
    const unsubscribeCombatResolved = gameEventBus.on('CombatResolved', (outcome) => {
      if (outcome === 'Victory') {
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
        return;
      }

      if (outcome === 'Defeat') {
        toast.custom(
          (toastId) => (
            <GameToast
              toastId={toastId}
              icon={Skull}
              title="You have fallen"
              description="Your consciousness fades..."
            />
          ),
          { duration: 3800 },
        );
      }
    });
    const unsubscribeQuestObjective = gameEventBus.on(
      'QuestObjectiveCompleted',
      ({ objectiveName }) => {
        toast.custom(
          (toastId) => (
            <GameToast
              toastId={toastId}
              icon={CircleCheck}
              title="Objective complete"
              description={objectiveName}
            />
          ),
          { duration: 3200 },
        );
      },
    );
    const unsubscribeQuestJournal = gameEventBus.on(
      'QuestJournalUpdated',
      (notificationMessage) => {
        if (!notificationMessage) {
          return;
        }

        toast.custom(
          (toastId) => (
            <GameToast
              toastId={toastId}
              icon={XCircle}
              title="Quest failed"
              description={notificationMessage}
            />
          ),
          { duration: 3800 },
        );
      },
    );
    const unsubscribeCrimeWitnessed = gameEventBus.on('CrimeWitnessed', ({ crimeName }) => {
      toast.custom(
        (toastId) => (
          <GameToast
            toastId={toastId}
            icon={Eye}
            title="Crime witnessed"
            description={`Someone saw your ${crimeName}.`}
          />
        ),
        { duration: 3800 },
      );
    });
    const unsubscribeCrimeWitnessesRemoved = gameEventBus.on(
      'CrimeWitnessesRemoved',
      ({ crimeName }) => {
        toast.custom(
          (toastId) => (
            <GameToast
              toastId={toastId}
              icon={ShieldOff}
              title="No living witnesses"
              description={`A ${crimeName} will go unreported.`}
            />
          ),
          { duration: 3800 },
        );
      },
    );
    return () => {
      unsubscribeSkill();
      unsubscribeCharacter();
      unsubscribeCombatResolved();
      unsubscribeQuestObjective();
      unsubscribeQuestJournal();
      unsubscribeCrimeWitnessed();
      unsubscribeCrimeWitnessesRemoved();
    };
  }, []);

  return <Toaster position="top-center" visibleToasts={3} />;
}
