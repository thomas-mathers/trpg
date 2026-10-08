import { useCallback, useEffect, useReducer, useRef, type ReactNode } from 'react';

import { prefetchDungeonPremises, type BuildingType } from '@/api/client';
import {
  PlayerIdContext,
  SceneContext,
  SessionContext,
} from '@/features/game/contexts/scene-context';
import { gameEventBus } from '@/lib/game-event-bus';

import { initialSceneState, reduceSceneState } from './scene-state';

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
  const [state, dispatch] = useReducer(reduceSceneState, undefined, initialSceneState);
  const { scene } = state;
  const prefetchedBuildingIds = useRef(new Set<string>());

  useEffect(() => {
    dispatch({ type: 'Reset' });
    prefetchedBuildingIds.current.clear();
  }, [sessionId]);

  useEffect(() => {
    const unsubscribers = [
      gameEventBus.on('SceneSnapshot', (payload) => dispatch({ type: 'SceneSnapshot', payload })),
      gameEventBus.on('CreaturesArrived', (payload) =>
        dispatch({ type: 'CreaturesArrived', payload }),
      ),
      gameEventBus.on('CreaturesLeft', (payload) => dispatch({ type: 'CreaturesLeft', payload })),
      gameEventBus.on('CreaturesMoved', (payload) => dispatch({ type: 'CreaturesMoved', payload })),
      gameEventBus.on('CreaturesUpdated', (payload) =>
        dispatch({ type: 'CreaturesUpdated', payload }),
      ),
      gameEventBus.on('CaravansArrived', (payload) =>
        dispatch({ type: 'CaravansArrived', payload }),
      ),
      gameEventBus.on('CaravansLeft', (payload) => dispatch({ type: 'CaravansLeft', payload })),
      gameEventBus.on('CaravansUpdated', (payload) =>
        dispatch({ type: 'CaravansUpdated', payload }),
      ),
      gameEventBus.on('WeatherChanged', (payload) => dispatch({ type: 'WeatherChanged', payload })),
      gameEventBus.on('ClockReanchored', (payload) =>
        dispatch({ type: 'ClockReanchored', payload }),
      ),
      gameEventBus.on('PlayerVitalsUpdated', (payload) =>
        dispatch({ type: 'PlayerVitalsUpdated', payload }),
      ),
      gameEventBus.on('SkillLevelUp', (payload) => dispatch({ type: 'SkillLevelUp', payload })),
      gameEventBus.on('CharacterLevelUp', (payload) =>
        dispatch({ type: 'CharacterLevelUp', payload }),
      ),
    ];
    return () => unsubscribers.forEach((unsubscribe) => unsubscribe());
  }, []);

  useEffect(() => {
    const buildingIds = (scene.nearbyBuildings ?? [])
      .filter((building) => DUNGEON_BUILDING_TYPES.has(building.type))
      .map((dungeon) => dungeon.id)
      .filter((buildingId) => !prefetchedBuildingIds.current.has(buildingId));

    if (buildingIds.length === 0) return;

    buildingIds.forEach((buildingId) => prefetchedBuildingIds.current.add(buildingId));
    void prefetchDungeonPremises({ body: { buildingIds } }).catch(() => {});
  }, [scene.nearbyBuildings]);

  const setMovementSpeed = useCallback(
    (movementSpeed: number, walkMetersPerSecond: number) =>
      dispatch({ type: 'MovementSpeedChanged', movementSpeed, walkMetersPerSecond }),
    [],
  );

  return (
    <SessionContext.Provider value={sessionId}>
      <PlayerIdContext.Provider value={scene.playerStatus.id}>
        <SceneContext.Provider value={{ scene, setMovementSpeed }}>
          {children}
        </SceneContext.Provider>
      </PlayerIdContext.Provider>
    </SessionContext.Provider>
  );
}
