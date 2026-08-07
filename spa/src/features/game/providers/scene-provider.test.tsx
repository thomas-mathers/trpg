import { render } from '@testing-library/react';
import { byText } from 'testing-library-selector';
import { describe, expect, it } from 'vitest';

import type { SceneSnapshot } from '@/api/client';
import { usePlayerId, useScene, useSessionId } from '@/features/game/contexts/scene-context';
import { gameEventBus } from '@/lib/game-event-bus';

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
});
