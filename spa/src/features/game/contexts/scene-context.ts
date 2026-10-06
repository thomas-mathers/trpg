import { createContext, useContext } from 'react';

import type { SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

export const SessionContext = createContext<string | null>(null);
export const PlayerIdContext = createContext<string | undefined>(undefined);

export interface SceneContextType {
  scene: SceneSnapshot;
  setMovementSpeed: (movementSpeed: number) => void;
}

export const createSceneSnapshot = (): SceneSnapshot => ({
  worldId: '',
  locationId: '',
  stateName: '',
  playerStatus: {
    id: '',
    name: '',
    creatureType: 'Human',
    gender: 'Male',
    level: 1,
    age: 1,
    condition: 'Awake',
    posture: 'Standing',
    movement: 'Stationary',
    isSneaking: false,
    isAlerted: false,
    isRestrained: false,
    gold: 0,
    currentHp: 0,
    maximumHp: 0,
    currentAp: 0,
    maximumAp: 0,
    currentMp: 0,
    maximumMp: 0,
    experienceCurrent: 0,
    experienceToNextLevel: 0,
    strength: 0,
    dexterity: 0,
    intelligence: 0,
    endurance: 0,
    stamina: 0,
    mana: 0,
    defense: 0,
    movementSpeed: 0,
    physicalResistance: 0,
    fireResistance: 0,
    iceResistance: 0,
    lightningResistance: 0,
    poisonResistance: 0,
    magicResistance: 0,
    questMarkers: [],
    readyToDeliver: false,
    activeConditions: {},
    activeDots: [],
    activeHots: [],
    activeBuffs: [],
    equipment: [],
    placement: { x: 0, y: 0, angle: 0 },
  },
  nearbyCreatures: [],
  nearbyBuildings: [],
  nearbyProps: [],
  exits: [],
  nearbyCaravans: [],
  size: { width: 0, depth: 0 },
  version: 0,
  gameTimeMilliseconds: 0,
  anchoredAtUnixMilliseconds: 0,
  timeScale: 0,
});

export const SceneContext = createContext<SceneContextType>({
  scene: createSceneSnapshot(),
  setMovementSpeed: () => {},
});

export function useSessionId() {
  const sessionId = useContext(SessionContext);
  if (!sessionId) {
    throw new Error('useSessionId must be used within a SceneProvider');
  }
  return sessionId;
}

export function useScene(): SceneContextType {
  return useContext(SceneContext);
}

export function usePlayerId() {
  return useContext(PlayerIdContext);
}
