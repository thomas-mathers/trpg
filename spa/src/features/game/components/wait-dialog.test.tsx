import { HubConnectionState } from '@microsoft/signalr';
import { screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import { SceneContext } from '@/features/game/contexts/scene-context';
import { GameChatContext, type GameChat } from '@/features/game/hooks/use-game-chat';
import {
  GameHubConnectionContext,
  type GameHubConnection,
} from '@/features/game/hooks/use-game-hub-connection';
import { renderWithProviders } from '@/test/test-utils';

import { WaitDialog } from './wait-dialog';

function scene(hour: number): SceneSnapshot {
  return {
    hour,
    playerStatus: { id: 'player-id', level: 1 },
  } as unknown as SceneSnapshot;
}

function buildGameChat(overrides: Partial<GameChat> = {}): GameChat {
  return {
    messages: [],
    isStreaming: false,
    submitNarratedTurn: vi.fn(),
    ...overrides,
  };
}

function buildChatHub(overrides: Partial<IChatHub> = {}): IChatHub {
  return {
    endSession: vi.fn(),
    receiveOpening: vi.fn(),
    sendChat: vi.fn(),
    sendWait: vi.fn(),
    sendFlee: vi.fn(),
    resolveUseAbilityCombatAction: vi.fn().mockResolvedValue(undefined),
    resolveUseItemCombatAction: vi.fn().mockResolvedValue(undefined),
    resolveAttackEncounterAction: vi.fn(),
    resolveEvadeEncounterAction: vi.fn(),
    resolveRetreatEncounterAction: vi.fn(),
    ...overrides,
  } as IChatHub;
}

function renderDialog({
  hour = 8,
  open = true,
  onClose = vi.fn(),
}: { hour?: number; open?: boolean; onClose?: () => void } = {}) {
  const gameChat = buildGameChat();
  const chatHub = buildChatHub();
  const hubConnection: GameHubConnection = {
    connectionStatus: HubConnectionState.Connected,
    connectionError: false,
    chatHub,
  };

  const result = renderWithProviders(
    <SceneContext.Provider value={scene(hour)}>
      <GameHubConnectionContext.Provider value={hubConnection}>
        <GameChatContext.Provider value={gameChat}>
          <WaitDialog open={open} onClose={onClose} />
        </GameChatContext.Provider>
      </GameHubConnectionContext.Provider>
    </SceneContext.Provider>,
  );

  return { ...result, gameChat, chatHub, onClose };
}

describe('WaitDialog', () => {
  it('defaults the target time to the current in-game hour', () => {
    renderDialog({ hour: 14 });

    expect(screen.getByLabelText('Wait until')).toHaveValue('14:00');
  });

  it('sends the hour delta to the picked time later the same day', async () => {
    const { user, gameChat, chatHub, onClose } = renderDialog({ hour: 8 });

    await user.clear(screen.getByLabelText('Wait until'));
    await user.type(screen.getByLabelText('Wait until'), '1430');
    await user.click(screen.getByRole('button', { name: 'Wait' }));

    expect(chatHub.sendWait).toHaveBeenCalledWith(6, 30);
    expect(gameChat.submitNarratedTurn).toHaveBeenCalledWith(
      'Wait until 14:30',
      vi.mocked(chatHub.sendWait).mock.results[0]?.value,
    );
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('wraps to the next day when the picked time is earlier than the current hour', async () => {
    const { user, chatHub } = renderDialog({ hour: 14 });

    await user.clear(screen.getByLabelText('Wait until'));
    await user.type(screen.getByLabelText('Wait until'), '0800');
    await user.click(screen.getByRole('button', { name: 'Wait' }));

    expect(chatHub.sendWait).toHaveBeenCalledWith(18, 0);
  });

  it('waits a full day when the picked time matches the current hour', async () => {
    const { chatHub } = renderDialog({ hour: 8 });

    await screen.findByDisplayValue('08:00');
    (await screen.findByRole('button', { name: 'Wait' })).click();

    expect(chatHub.sendWait).toHaveBeenCalledWith(24, 0);
  });

  it('does not render when there is no scene yet', () => {
    renderWithProviders(
      <SceneContext.Provider value={undefined}>
        <GameHubConnectionContext.Provider
          value={{
            connectionStatus: HubConnectionState.Connected,
            connectionError: false,
            chatHub: buildChatHub(),
          }}
        >
          <GameChatContext.Provider value={buildGameChat()}>
            <WaitDialog open onClose={() => {}} />
          </GameChatContext.Provider>
        </GameHubConnectionContext.Provider>
      </SceneContext.Provider>,
    );

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
