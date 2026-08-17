import { useEffect, useState, type ReactNode } from 'react';

import type { SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import {
  PlayerIdContext,
  SceneContext,
  SessionContext,
} from '@/features/game/contexts/scene-context';
import { gameEventBus } from '@/lib/game-event-bus';

interface SceneProviderProps {
  sessionId: string;
  children: ReactNode;
}

export function SceneProvider({ sessionId, children }: SceneProviderProps) {
  const [scene, setScene] = useState<SceneSnapshot>();
  const playerId = scene?.playerStatus.id;

  useEffect(() => gameEventBus.on('SceneSnapshot', setScene), []);

  useEffect(
    () =>
      gameEventBus.on('SkillLevelUp', (progress) => {
        setScene((current) =>
          current
            ? {
                ...current,
                playerStatus: {
                  ...current.playerStatus,
                  experienceCurrent: progress.characterExperienceCurrent,
                  experienceToNextLevel: progress.characterExperienceToNextLevel,
                },
              }
            : current,
        );
      }),
    [],
  );

  useEffect(
    () =>
      gameEventBus.on('CharacterLevelUp', ({ level }) => {
        setScene((current) =>
          current ? { ...current, playerStatus: { ...current.playerStatus, level } } : current,
        );
      }),
    [],
  );

  return (
    <SessionContext.Provider value={sessionId}>
      <PlayerIdContext.Provider value={playerId}>
        <SceneContext.Provider value={scene}>{children}</SceneContext.Provider>
      </PlayerIdContext.Provider>
    </SessionContext.Provider>
  );
}
