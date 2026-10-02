import { HubConnectionState } from '@microsoft/signalr';
import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { ActionResult, SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import { SceneContext } from '@/features/game/contexts/scene-context';
import {
  GameHubConnectionContext,
  type GameHubConnection,
} from '@/features/game/hooks/use-game-hub-connection';
import { renderWithProviders } from '@/test/test-utils';

import { WaitDialog } from './wait-dialog';

const MILLISECONDS_PER_MINUTE = 60 * 1000;
const EPOCH_HOUR = 8;

function scene(hour: number, minute = 0): SceneSnapshot {
  return {
    gameTimeMilliseconds: ((hour - EPOCH_HOUR) * 60 + minute) * MILLISECONDS_PER_MINUTE,
    anchoredAtUnixMilliseconds: Date.now(),
    timeScale: 1,
    playerStatus: {
      id: 'player-id',
      level: 1,
      condition: 'Awake',
      posture: 'Sitting',
    },
  } as unknown as SceneSnapshot;
}

function succeeded(): Promise<ActionResult> {
  return Promise.resolve({ succeeded: true });
}

function buildChatHub(overrides: Partial<IChatHub> = {}): IChatHub {
  return {
    endSession: vi.fn(),
    sendChat: vi.fn(),
    sendWait: vi.fn(succeeded),
    sendFlee: vi.fn(succeeded),
    resolveUseAbilityCombatAction: vi.fn().mockResolvedValue(undefined),
    resolveUseItemCombatAction: vi.fn().mockResolvedValue(undefined),
    resolveAttackEncounterAction: vi.fn(succeeded),
    resolveFleeEncounterAction: vi.fn(succeeded),
    ...overrides,
  } as IChatHub;
}

function renderDialog({
  hour = 8,
  minute = 0,
  open = true,
  onClose = vi.fn(),
}: {
  hour?: number;
  minute?: number;
  open?: boolean;
  onClose?: () => void;
} = {}) {
  const chatHub = buildChatHub();
  const hubConnection: GameHubConnection = {
    connectionStatus: HubConnectionState.Connected,
    connectionError: false,
    chatHub,
  };

  const result = renderWithProviders(
    <SceneContext.Provider value={{ scene: scene(hour, minute), setMovementSpeed: () => {} }}>
      <GameHubConnectionContext.Provider value={hubConnection}>
        <WaitDialog open={open} onClose={onClose} />
      </GameHubConnectionContext.Provider>
    </SceneContext.Provider>,
  );

  return { ...result, chatHub, onClose };
}

describe('WaitDialog', () => {
  it('defaults the target time to the current in-game hour', () => {
    renderDialog({ hour: 14 });

    expect(screen.getByLabelText('Wait until')).toHaveValue(14);
    expect(screen.getByLabelText('Minute')).toHaveValue(0);
  });

  it('sends the hour delta to the picked time later the same day', async () => {
    const { user, chatHub, onClose } = renderDialog({ hour: 8 });

    await user.clear(screen.getByLabelText('Wait until'));
    await user.type(screen.getByLabelText('Wait until'), '14');
    await user.clear(screen.getByLabelText('Minute'));
    await user.type(screen.getByLabelText('Minute'), '30');
    await user.click(screen.getByRole('button', { name: 'Wait' }));

    expect(chatHub.sendWait).toHaveBeenCalledWith(6, 30);
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('measures the wait from the current minute rather than the start of the hour', async () => {
    const { user, chatHub } = renderDialog({ hour: 8, minute: 45 });

    await user.clear(screen.getByLabelText('Wait until'));
    await user.type(screen.getByLabelText('Wait until'), '14');
    await user.clear(screen.getByLabelText('Minute'));
    await user.type(screen.getByLabelText('Minute'), '30');
    await user.click(screen.getByRole('button', { name: 'Wait' }));

    expect(chatHub.sendWait).toHaveBeenCalledWith(5, 45);
  });

  it('wraps to the next day when the picked time is earlier than the current hour', async () => {
    const { user, chatHub } = renderDialog({ hour: 14 });

    await user.clear(screen.getByLabelText('Wait until'));
    await user.type(screen.getByLabelText('Wait until'), '08');
    await user.click(screen.getByRole('button', { name: 'Wait' }));

    expect(chatHub.sendWait).toHaveBeenCalledWith(18, 0);
  });

  it('waits a full day when the picked time matches the current hour', async () => {
    const { chatHub } = renderDialog({ hour: 8 });

    await waitFor(() => expect(screen.getByLabelText('Wait until')).toHaveValue(8));
    (await screen.findByRole('button', { name: 'Wait' })).click();

    expect(chatHub.sendWait).toHaveBeenCalledWith(24, 0);
  });

  it('closes and does not render when the player is not sitting', () => {
    const onClose = vi.fn();
    const notSitting = {
      ...scene(8),
      playerStatus: { ...scene(8).playerStatus, posture: 'Standing' as const },
    };

    renderWithProviders(
      <SceneContext.Provider value={{ scene: notSitting, setMovementSpeed: () => {} }}>
        <GameHubConnectionContext.Provider
          value={{
            connectionStatus: HubConnectionState.Connected,
            connectionError: false,
            chatHub: buildChatHub(),
          }}
        >
          <WaitDialog open onClose={onClose} />
        </GameHubConnectionContext.Provider>
      </SceneContext.Provider>,
    );

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(onClose).toHaveBeenCalledOnce();
  });
});
