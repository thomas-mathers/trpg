import { render, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { byText } from 'testing-library-selector';
import { describe, expect, it } from 'vitest';

import { handlePrefetchDungeonPremises } from '@/api/client/msw.gen';
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
});
