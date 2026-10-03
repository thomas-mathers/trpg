import { describe, expect, it } from 'vitest';

import type {
  NearbyCaravanSnapshot,
  SceneSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { createSceneSnapshot } from '@/features/game/contexts/scene-context';

import { initialSceneState, reduceSceneState, type SceneAction } from './scene-state';

const scope = { worldId: 'world', locationId: 'location' };

function snapshot(overrides: Partial<SceneSnapshot> = {}): SceneSnapshot {
  const empty = createSceneSnapshot();
  return {
    ...empty,
    ...scope,
    version: 1,
    playerStatus: { ...empty.playerStatus, id: 'player' },
    ...overrides,
  };
}

function apply(...actions: SceneAction[]) {
  return actions.reduce(reduceSceneState, initialSceneState());
}

describe('scene state', () => {
  it('requires a snapshot and applies equal-version sibling deltas', () => {
    const weather: SceneAction = {
      type: 'WeatherChanged',
      payload: { ...scope, version: 2, weather: 'Rain' },
    };
    const clock: SceneAction = {
      type: 'ClockReanchored',
      payload: {
        ...scope,
        version: 2,
        gameTimeMilliseconds: 500,
        anchoredAtUnixMilliseconds: 1000,
        timeScale: 2,
      },
    };

    expect(apply(weather).hasSnapshot).toBe(false);
    const state = apply({ type: 'SceneSnapshot', payload: snapshot() }, weather, clock);
    expect(state.scene).toMatchObject({
      weather: 'Rain',
      version: 2,
      gameTimeMilliseconds: 500,
      anchoredAtUnixMilliseconds: 1000,
      timeScale: 2,
    });
  });

  it('rejects old versions and deltas for another world or location', () => {
    const initial = { type: 'SceneSnapshot', payload: snapshot() } as const;
    const newer = {
      type: 'WeatherChanged',
      payload: { ...scope, version: 4, weather: 'Snow' },
    } as const;
    const state = apply(
      initial,
      newer,
      { type: 'WeatherChanged', payload: { ...scope, version: 3, weather: 'Rain' } },
      {
        type: 'WeatherChanged',
        payload: { ...scope, worldId: 'other', version: 5, weather: 'Fog' },
      },
      {
        type: 'WeatherChanged',
        payload: { ...scope, locationId: 'other', version: 5, weather: 'Fog' },
      },
      { type: 'SceneSnapshot', payload: snapshot({ version: 3 }) },
    );
    expect(state.scene.weather).toBe('Snow');
    expect(state.scene.version).toBe(4);

    const next = reduceSceneState(state, {
      type: 'SceneSnapshot',
      payload: snapshot({ version: 5, locationId: 'other', weather: 'Clear' }),
    });
    expect(next.scene.weather).toBe('Clear');
    expect(reduceSceneState(next, newer)).toBe(next);
  });

  it('updates creature membership, status, and placement without duplicating the player', () => {
    const player = snapshot().playerStatus;
    const npc = { ...player, id: 'npc', name: 'Nia' };
    const state = apply(
      { type: 'SceneSnapshot', payload: snapshot() },
      { type: 'CreaturesArrived', payload: { ...scope, version: 2, creatures: [npc] } },
      {
        type: 'CreaturesUpdated',
        payload: {
          ...scope,
          version: 3,
          creatures: [
            { ...player, currentHp: 8 },
            { ...npc, currentHp: 5 },
          ],
        },
      },
      {
        type: 'CreaturesMoved',
        payload: {
          ...scope,
          version: 3,
          placements: [
            { creatureId: 'player', placement: { x: 1, y: 2, angle: 3 } },
            { creatureId: 'npc', placement: { x: 4, y: 5, angle: 6 } },
          ],
        },
      },
    );
    expect(state.scene.playerStatus).toMatchObject({ currentHp: 8, placement: { x: 1 } });
    expect(state.scene.nearbyCreatures).toMatchObject([
      { id: 'npc', currentHp: 5, placement: { x: 4 } },
    ]);

    const left = reduceSceneState(state, {
      type: 'CreaturesLeft',
      payload: { ...scope, version: 4, creatureIds: ['npc'] },
    });
    expect(left.scene.nearbyCreatures).toEqual([]);
  });

  it('upserts and removes caravans', () => {
    const caravan: NearbyCaravanSnapshot = {
      caravanId: 'caravan',
      routeName: 'North road',
      ticketFeeGold: 5,
      minutesUntilDeparture: 10,
      passengerServiceAvailable: true,
      destinations: [],
    };
    const state = apply(
      { type: 'SceneSnapshot', payload: snapshot() },
      { type: 'CaravansArrived', payload: { ...scope, version: 2, caravans: [caravan] } },
      {
        type: 'CaravansUpdated',
        payload: { ...scope, version: 3, caravans: [{ ...caravan, minutesUntilDeparture: 5 }] },
      },
    );
    expect(state.scene.nearbyCaravans).toMatchObject([{ minutesUntilDeparture: 5 }]);
    const left = reduceSceneState(state, {
      type: 'CaravansLeft',
      payload: { ...scope, version: 4, caravanIds: ['caravan'] },
    });
    expect(left.scene.nearbyCaravans).toEqual([]);
  });
});
