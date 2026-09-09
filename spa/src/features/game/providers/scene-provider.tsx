import { useEffect, useRef, useState, type ReactNode } from 'react';

import { prefetchDungeonPremises, type BuildingType } from '@/api/client';
import type { SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import {
  PlayerIdContext,
  SceneContext,
  SessionContext,
} from '@/features/game/contexts/scene-context';
import { gameEventBus } from '@/lib/game-event-bus';

// Mirrors TRPG.Domain.Models.BuildingTypes.Dungeon — the server already no-ops a prefetch against
// anything else, this just keeps the client from asking for buildings that can never need one.
const DUNGEON_BUILDING_TYPES: ReadonlySet<BuildingType> = new Set([
  'Cave',
  'Crypt',
  'Mine',
  'Ruins',
  'Tower',
]);

interface SceneProviderProps {
  sessionId: string;
  children: ReactNode;
}

export function SceneProvider({ sessionId, children }: SceneProviderProps) {
  const [scene, setScene] = useState<SceneSnapshot>();
  const playerId = scene?.playerStatus.id;
  const prefetchedBuildingIds = useRef(new Set<string>());

  useEffect(() => gameEventBus.on('SceneSnapshot', setScene), []);

  useEffect(() => {
    const buildingIds = (scene?.nearbyBuildings ?? [])
      .filter((building) => DUNGEON_BUILDING_TYPES.has(building.type))
      .map((dungeon) => dungeon.id)
      .filter((buildingId) => !prefetchedBuildingIds.current.has(buildingId));
    if (buildingIds.length === 0) return;

    buildingIds.forEach((buildingId) => prefetchedBuildingIds.current.add(buildingId));

    // Warms every unentered nearby dungeon's history in one request, ahead of the player actually
    // walking into any of them; a failed warm-up costs nothing, since walking in writes it the
    // same way if it is still missing.
    void prefetchDungeonPremises({ body: { buildingIds } }).catch(() => {});
  }, [scene?.nearbyBuildings]);

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
