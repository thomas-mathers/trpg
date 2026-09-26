import { render, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { byText } from 'testing-library-selector';
import { describe, expect, it } from 'vitest';

import { handlePrefetchDungeonPremises } from '@/api/client/msw.gen';
import type { PlayerVitalsUpdated } from '@/api/signalr-client/TRPG.Creatures.Responses';
import type { SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { usePlayerId, useScene, useSessionId } from '@/features/game/contexts/scene-context';
import { gameEventBus } from '@/lib/game-event-bus';
import { server } from '@/test/server';

import { SceneProvider } from './scene-provider';

const ui = {
  initialState: byText('session-id|no-player|no-scene'),
  loadedState: byText('session-id|player-id|scene-loaded'),
};

function Consumer() {
  const sessionId = useSessionId();
  const playerId = usePlayerId();
  const scene = useScene();

  return (
    <output>
      {sessionId}|{playerId ?? 'no-player'}|{scene ? 'scene-loaded' : 'no-scene'}
    </output>
  );
}

function VitalsConsumer() {
  const scene = useScene();

  return (
    <output>
      {scene ? `hp:${scene.playerStatus.currentHp}/${scene.playerStatus.maximumHp}` : 'no-scene'}
    </output>
  );
}

function makeVitals(overrides: Partial<PlayerVitalsUpdated>): PlayerVitalsUpdated {
  return {
    playerId: 'player-id',
    currentHp: 10,
    maximumHp: 40,
    currentAp: 4,
    maximumAp: 10,
    currentMp: 2,
    maximumMp: 8,
    version: 5,
    ...overrides,
  };
}

describe('SceneProvider', () => {
  it('provides the session and updates scene/player state from snapshots', async () => {
    render(
      <SceneProvider sessionId="session-id">
        <Consumer />
      </SceneProvider>,
    );

    expect(ui.initialState.get()).toBeVisible();

    const snapshot = { playerStatus: { id: 'player-id' } } as SceneSnapshot;
    gameEventBus.emit('SceneSnapshot', snapshot);

    expect(await ui.loadedState.find()).toBeVisible();
  });

  it('ignores a snapshot that is older than the one already shown', async () => {
    render(
      <SceneProvider sessionId="session-id">
        <VitalsConsumer />
      </SceneProvider>,
    );
    gameEventBus.emit('SceneSnapshot', {
      playerStatus: { id: 'player-id', currentHp: 9, maximumHp: 40 },
      version: 4,
    } as SceneSnapshot);
    expect(await byText('hp:9/40').find()).toBeVisible();

    gameEventBus.emit('SceneSnapshot', {
      playerStatus: { id: 'player-id', currentHp: 2, maximumHp: 40 },
      version: 3,
    } as SceneSnapshot);

    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(byText('hp:9/40').get()).toBeVisible();
  });

  it('accepts snapshots from the start of a new session even after a higher version was seen', async () => {
    const { rerender } = render(
      <SceneProvider sessionId="session-id">
        <VitalsConsumer />
      </SceneProvider>,
    );
    gameEventBus.emit('SceneSnapshot', {
      playerStatus: { id: 'player-id', currentHp: 9, maximumHp: 40 },
      version: 50,
    } as SceneSnapshot);
    expect(await byText('hp:9/40').find()).toBeVisible();

    rerender(
      <SceneProvider sessionId="other-session-id">
        <VitalsConsumer />
      </SceneProvider>,
    );
    gameEventBus.emit('SceneSnapshot', {
      playerStatus: { id: 'player-id', currentHp: 4, maximumHp: 40 },
      version: 1,
    } as SceneSnapshot);

    expect(await byText('hp:4/40').find()).toBeVisible();
  });

  it('warms the premise of every nearby dungeon in one batch, but not an ordinary building', async () => {
    const prefetchedBatches: string[][] = [];
    server.use(
      handlePrefetchDungeonPremises(async ({ request }) => {
        const { buildingIds } = await request.json();
        prefetchedBatches.push(buildingIds);
        return new HttpResponse(null, { status: 204 });
      }),
    );

    render(
      <SceneProvider sessionId="session-id">
        <Consumer />
      </SceneProvider>,
    );

    gameEventBus.emit('SceneSnapshot', {
      playerStatus: { id: 'player-id' },
      nearbyBuildings: [
        { id: 'crypt-id', name: 'The Silent Tomb', type: 'Crypt', typeDescription: 'Crypt' },
        { id: 'mine-id', name: 'The Old Shaft', type: 'Mine', typeDescription: 'Mine' },
        { id: 'inn-id', name: 'The Wandering Boar', type: 'Inn', typeDescription: 'Inn' },
      ],
    } as SceneSnapshot);

    await waitFor(() => expect(prefetchedBatches).toEqual([['crypt-id', 'mine-id']]));
  });

  it('does not warm the same dungeon twice, even if the scene refreshes again', async () => {
    const prefetchedBatches: string[][] = [];
    server.use(
      handlePrefetchDungeonPremises(async ({ request }) => {
        const { buildingIds } = await request.json();
        prefetchedBatches.push(buildingIds);
        return new HttpResponse(null, { status: 204 });
      }),
    );

    render(
      <SceneProvider sessionId="session-id">
        <Consumer />
      </SceneProvider>,
    );

    const dungeon = {
      id: 'crypt-id',
      name: 'The Silent Tomb',
      type: 'Crypt',
      typeDescription: 'Crypt',
    };
    gameEventBus.emit('SceneSnapshot', {
      playerStatus: { id: 'player-id' },
      nearbyBuildings: [dungeon],
    } as SceneSnapshot);
    await waitFor(() => expect(prefetchedBatches).toEqual([['crypt-id']]));

    // A freshly-built array, as a real scene refresh would carry, so the effect's dependency
    // actually changes and re-runs — proving the dedupe is by building id, not array identity.
    gameEventBus.emit('SceneSnapshot', {
      playerStatus: { id: 'player-id' },
      nearbyBuildings: [{ ...dungeon }],
    } as SceneSnapshot);

    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(prefetchedBatches).toEqual([['crypt-id']]);
  });

  describe('player vitals updates', () => {
    const vitalsSnapshot = {
      playerStatus: { id: 'player-id', currentHp: 1, maximumHp: 40 },
    } as SceneSnapshot;

    it('applies a vitals update to the player status of the current scene', async () => {
      render(
        <SceneProvider sessionId="session-id">
          <VitalsConsumer />
        </SceneProvider>,
      );
      gameEventBus.emit('SceneSnapshot', vitalsSnapshot);

      gameEventBus.emit('PlayerVitalsUpdated', makeVitals({ currentHp: 12 }));

      expect(await byText('hp:12/40').find()).toBeVisible();
    });

    it('ignores a vitals update whose version is not newer than one already applied', async () => {
      render(
        <SceneProvider sessionId="session-id">
          <VitalsConsumer />
        </SceneProvider>,
      );
      gameEventBus.emit('SceneSnapshot', vitalsSnapshot);
      gameEventBus.emit('PlayerVitalsUpdated', makeVitals({ currentHp: 20, version: 10 }));
      expect(await byText('hp:20/40').find()).toBeVisible();

      gameEventBus.emit('PlayerVitalsUpdated', makeVitals({ currentHp: 15, version: 5 }));

      await new Promise((resolve) => setTimeout(resolve, 0));
      expect(byText('hp:20/40').get()).toBeVisible();
    });

    it('ignores a vitals update older than the snapshot that already carries newer vitals', async () => {
      render(
        <SceneProvider sessionId="session-id">
          <VitalsConsumer />
        </SceneProvider>,
      );
      gameEventBus.emit('SceneSnapshot', { ...vitalsSnapshot, version: 8 });

      gameEventBus.emit('PlayerVitalsUpdated', makeVitals({ currentHp: 30, version: 7 }));

      await new Promise((resolve) => setTimeout(resolve, 0));
      expect(byText('hp:1/40').get()).toBeVisible();
    });

    it('applies a snapshot that is newer than the vitals already applied', async () => {
      render(
        <SceneProvider sessionId="session-id">
          <VitalsConsumer />
        </SceneProvider>,
      );
      gameEventBus.emit('SceneSnapshot', { ...vitalsSnapshot, version: 1 });
      gameEventBus.emit('PlayerVitalsUpdated', makeVitals({ currentHp: 12, version: 2 }));
      expect(await byText('hp:12/40').find()).toBeVisible();

      gameEventBus.emit('SceneSnapshot', {
        playerStatus: { id: 'player-id', currentHp: 3, maximumHp: 40 },
        version: 3,
      } as SceneSnapshot);

      expect(await byText('hp:3/40').find()).toBeVisible();
    });

    it('ignores a vitals update for a different creature', async () => {
      render(
        <SceneProvider sessionId="session-id">
          <VitalsConsumer />
        </SceneProvider>,
      );
      gameEventBus.emit('SceneSnapshot', vitalsSnapshot);

      gameEventBus.emit('PlayerVitalsUpdated', makeVitals({ playerId: 'someone-else' }));

      await new Promise((resolve) => setTimeout(resolve, 0));
      expect(byText('hp:1/40').get()).toBeVisible();
    });
  });
});
