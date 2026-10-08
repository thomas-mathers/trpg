import type {
  CharacterLevelUp,
  PlayerVitalsUpdated,
  SkillLevelUp,
} from '@/api/signalr-client/TRPG.Creatures.Responses';
import type {
  CaravansArrivedPayload,
  CaravansLeftPayload,
  CaravansUpdatedPayload,
  ClockReanchoredPayload,
  CreaturesArrivedPayload,
  CreaturesLeftPayload,
  CreaturesMovedPayload,
  CreaturesUpdatedPayload,
  CreatureStatusSnapshot,
  SceneSnapshot,
  WeatherChangedPayload,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { createSceneSnapshot } from '@/features/game/contexts/scene-context';

type SceneDeltaPayloads = {
  CreaturesArrived: CreaturesArrivedPayload;
  CreaturesLeft: CreaturesLeftPayload;
  CreaturesMoved: CreaturesMovedPayload;
  CreaturesUpdated: CreaturesUpdatedPayload;
  CaravansArrived: CaravansArrivedPayload;
  CaravansLeft: CaravansLeftPayload;
  CaravansUpdated: CaravansUpdatedPayload;
  WeatherChanged: WeatherChangedPayload;
  ClockReanchored: ClockReanchoredPayload;
};

type SceneDeltaAction = {
  [K in keyof SceneDeltaPayloads]: { type: K; payload: SceneDeltaPayloads[K] };
}[keyof SceneDeltaPayloads];

export type SceneAction =
  | SceneDeltaAction
  | { type: 'SceneSnapshot'; payload: SceneSnapshot }
  | { type: 'PlayerVitalsUpdated'; payload: PlayerVitalsUpdated }
  | { type: 'SkillLevelUp'; payload: SkillLevelUp }
  | { type: 'CharacterLevelUp'; payload: CharacterLevelUp }
  | { type: 'MovementSpeedChanged'; movementSpeed: number; walkMetersPerSecond: number }
  | { type: 'Reset' };

export interface SceneState {
  scene: SceneSnapshot;
  hasSnapshot: boolean;
}

export function initialSceneState(): SceneState {
  return { scene: createSceneSnapshot(), hasSnapshot: false };
}

export function reduceSceneState(state: SceneState, action: SceneAction): SceneState {
  if (action.type === 'Reset') return initialSceneState();
  if (action.type === 'SceneSnapshot') return applySnapshot(state, action.payload);
  if (action.type === 'PlayerVitalsUpdated') return applyVitals(state, action.payload);
  if (action.type === 'SkillLevelUp') return applySkillLevel(state, action.payload);
  if (action.type === 'CharacterLevelUp') return applyCharacterLevel(state, action.payload);
  if (action.type === 'MovementSpeedChanged')
    return applyMovementSpeed(state, action.movementSpeed, action.walkMetersPerSecond);

  const { scene } = state;
  const { payload } = action;
  if (
    !state.hasSnapshot ||
    payload.worldId !== scene.worldId ||
    payload.locationId !== scene.locationId ||
    payload.version < scene.version
  )
    return state;

  return {
    ...state,
    scene: { ...applySceneDelta(scene, action), version: payload.version },
  };
}

function applySnapshot(state: SceneState, snapshot: SceneSnapshot): SceneState {
  if (state.hasSnapshot && snapshot.version <= state.scene.version) return state;
  return { scene: snapshot, hasSnapshot: true };
}

function applyVitals(state: SceneState, vitals: PlayerVitalsUpdated): SceneState {
  const { scene } = state;
  if (
    !state.hasSnapshot ||
    vitals.playerId !== scene.playerStatus.id ||
    vitals.version <= scene.version
  )
    return state;

  return {
    ...state,
    scene: {
      ...scene,
      version: vitals.version,
      playerStatus: {
        ...scene.playerStatus,
        currentHp: vitals.currentHp,
        maximumHp: vitals.maximumHp,
        currentAp: vitals.currentAp,
        maximumAp: vitals.maximumAp,
        currentMp: vitals.currentMp,
        maximumMp: vitals.maximumMp,
      },
    },
  };
}

function applySkillLevel(state: SceneState, progress: SkillLevelUp): SceneState {
  const { scene } = state;
  return {
    ...state,
    scene: {
      ...scene,
      playerStatus: {
        ...scene.playerStatus,
        experienceCurrent: progress.characterExperienceCurrent,
        experienceToNextLevel: progress.characterExperienceToNextLevel,
      },
    },
  };
}

function applyCharacterLevel(state: SceneState, progress: CharacterLevelUp): SceneState {
  const { scene } = state;
  return {
    ...state,
    scene: { ...scene, playerStatus: { ...scene.playerStatus, level: progress.level } },
  };
}

function applyMovementSpeed(
  state: SceneState,
  movementSpeed: number,
  walkMetersPerSecond: number,
): SceneState {
  const { scene } = state;
  return {
    ...state,
    scene: {
      ...scene,
      playerStatus: { ...scene.playerStatus, movementSpeed, walkMetersPerSecond },
    },
  };
}

function applySceneDelta(scene: SceneSnapshot, action: SceneDeltaAction): SceneSnapshot {
  switch (action.type) {
    case 'CreaturesArrived':
    case 'CreaturesUpdated':
      return applyCreatureStatuses(scene, action.payload.creatures);
    case 'CreaturesLeft':
      return {
        ...scene,
        nearbyCreatures: scene.nearbyCreatures.filter(
          (creature) => !action.payload.creatureIds.includes(creature.id),
        ),
      };
    case 'CreaturesMoved':
      return applyCreatureMoves(scene, action.payload);
    case 'CaravansArrived':
    case 'CaravansUpdated':
      return {
        ...scene,
        nearbyCaravans: upsertById(
          scene.nearbyCaravans,
          action.payload.caravans,
          (caravan) => caravan.caravanId,
        ),
      };
    case 'CaravansLeft':
      return {
        ...scene,
        nearbyCaravans: scene.nearbyCaravans.filter(
          (caravan) => !action.payload.caravanIds.includes(caravan.caravanId),
        ),
      };
    case 'WeatherChanged':
      return { ...scene, weather: action.payload.weather };
    case 'ClockReanchored':
      return {
        ...scene,
        gameTimeMilliseconds: action.payload.gameTimeMilliseconds,
        anchoredAtUnixMilliseconds: action.payload.anchoredAtUnixMilliseconds,
        timeScale: action.payload.timeScale,
      };
  }
}

function applyCreatureStatuses(
  scene: SceneSnapshot,
  updates: CreatureStatusSnapshot[],
): SceneSnapshot {
  const player = updates.find((creature) => creature.id === scene.playerStatus.id);
  const nearby = updates.filter((creature) => creature.id !== scene.playerStatus.id);
  return {
    ...scene,
    playerStatus: player ?? scene.playerStatus,
    nearbyCreatures: upsertById(scene.nearbyCreatures, nearby, (creature) => creature.id),
  };
}

function applyCreatureMoves(scene: SceneSnapshot, payload: CreaturesMovedPayload): SceneSnapshot {
  const placements = new Map(
    payload.placements.map(({ creatureId, placement }) => [creatureId, placement]),
  );
  const playerPlacement = placements.get(scene.playerStatus.id);
  return {
    ...scene,
    playerStatus: playerPlacement
      ? { ...scene.playerStatus, placement: playerPlacement }
      : scene.playerStatus,
    nearbyCreatures: scene.nearbyCreatures.map((creature) => {
      const placement = placements.get(creature.id);
      return placement ? { ...creature, placement } : creature;
    }),
  };
}

function upsertById<T>(current: T[], updates: T[], idOf: (item: T) => string): T[] {
  const byId = new Map(current.map((item) => [idOf(item), item]));
  updates.forEach((item) => byId.set(idOf(item), item));
  return [...byId.values()];
}
