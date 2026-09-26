import { HubConnectionState } from '@microsoft/signalr';
import { act, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { SceneContext } from '@/features/game/contexts/scene-context';
import { renderWithProviders } from '@/test/test-utils';

import { StatusBar } from './status-bar';

const ANCHORED_AT = Date.UTC(2030, 0, 1);
const HOUR = 60 * 60 * 1000;

function scene(gameTimeMilliseconds: number): SceneSnapshot {
  return {
    stateName: 'Aldmark',
    gameTimeMilliseconds,
    anchoredAtUnixMilliseconds: ANCHORED_AT,
    playerStatus: {
      id: 'player-id',
      name: 'Aria',
      level: 3,
      currentHp: 10,
      maximumHp: 20,
      currentAp: 5,
      maximumAp: 10,
      currentMp: 2,
      maximumMp: 4,
      experienceCurrent: 0,
      experienceToNextLevel: 100,
    },
  } as SceneSnapshot;
}

function renderStatusBar(current: SceneSnapshot | undefined) {
  return renderWithProviders(
    <SceneContext.Provider value={current}>
      <StatusBar connectionStatus={HubConnectionState.Connected} controls={null} />
    </SceneContext.Provider>,
  );
}

describe('StatusBar clock', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date', 'setInterval', 'clearInterval'] });
    vi.setSystemTime(ANCHORED_AT);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows the custom calendar date and the time with seconds', () => {
    renderStatusBar(scene(0));

    expect(screen.getByText('Emberday, Frostwane 1 - 08:00:00')).toBeVisible();
  });

  it('ticks the visible time once per second', () => {
    renderStatusBar(scene(0));

    act(() => {
      vi.advanceTimersByTime(3000);
    });

    expect(screen.getByText('Emberday, Frostwane 1 - 08:00:03')).toBeVisible();
  });

  it('rolls the date over at midnight', () => {
    renderStatusBar(scene(16 * HOUR - 1000));

    expect(screen.getByText('Emberday, Frostwane 1 - 23:59:59')).toBeVisible();

    act(() => {
      vi.advanceTimersByTime(1000);
    });

    expect(screen.getByText('Ashday, Frostwane 2 - 00:00:00')).toBeVisible();
  });

  it('shows no time before the first scene', () => {
    renderStatusBar(undefined);

    expect(screen.queryByText(/Frostwane/)).not.toBeInTheDocument();
  });
});
