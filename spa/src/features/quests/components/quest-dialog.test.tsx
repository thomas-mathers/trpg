import { HubConnectionState } from '@microsoft/signalr';
import { screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import { handleCompleteQuest, handleGetQuestJournal } from '@/api/client/msw.gen';
import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import { GameChatContext, type GameChat } from '@/features/game/hooks/use-game-chat';
import {
  GameHubConnectionContext,
  type GameHubConnection,
} from '@/features/game/hooks/use-game-hub-connection';
import { server } from '@/test/server';
import { renderWithProviders } from '@/test/test-utils';

import { QuestDialog, type QuestDialogState } from './quest-dialog';

const offerQuest: QuestDialogState = {
  worldId: 'world-id',
  questId: 'quest-id',
  name: 'A Dangerous Delivery',
  description: 'Help with a dangerous errand.',
  goldReward: 100,
  objectives: [
    {
      name: 'Defeat Hulking Thunderfoot',
      description: 'Defeat Hulking Thunderfoot.',
      requiredAmount: 1,
    },
  ],
  mode: 'Offer',
};

const turnInQuest: QuestDialogState = { ...offerQuest, mode: 'TurnIn' };

function buildChatHub(overrides: Partial<IChatHub> = {}): IChatHub {
  return {
    endSession: vi.fn(),
    receiveOpening: vi.fn(),
    sendChat: vi.fn(),
    sendAcceptQuest: vi.fn(),
    sendDeclineQuest: vi.fn(),
    ...overrides,
  } as IChatHub;
}

function buildGameChat(overrides: Partial<GameChat> = {}): GameChat {
  return {
    messages: [],
    isStreaming: false,
    submitNarratedTurn: vi.fn(),
    ...overrides,
  };
}

function renderDialog(
  quest: QuestDialogState | null,
  onClose: () => void,
  chatHubOverrides: Partial<IChatHub> = {},
) {
  const chatHub = buildChatHub(chatHubOverrides);
  const gameChat = buildGameChat();
  const hubConnection: GameHubConnection = {
    connectionStatus: HubConnectionState.Connected,
    connectionError: false,
    chatHub,
  };

  const result = renderWithProviders(
    <GameHubConnectionContext.Provider value={hubConnection}>
      <GameChatContext.Provider value={gameChat}>
        <QuestDialog playerId="player-id" quest={quest} onClose={onClose} />
      </GameChatContext.Provider>
    </GameHubConnectionContext.Provider>,
  );

  return { ...result, chatHub, gameChat };
}

describe('QuestDialog', () => {
  it('starts a narrated accept turn and closes', async () => {
    const onClose = vi.fn();
    const fakeStream = {} as ReturnType<IChatHub['sendAcceptQuest']>;
    const sendAcceptQuest = vi.fn().mockReturnValue(fakeStream);
    const { user, chatHub, gameChat } = renderDialog(offerQuest, onClose, { sendAcceptQuest });

    await user.click(screen.getByRole('button', { name: 'Accept quest' }));

    expect(chatHub.sendAcceptQuest).toHaveBeenCalledWith('quest-id');
    expect(gameChat.submitNarratedTurn).toHaveBeenCalledWith(
      'Accept "A Dangerous Delivery"',
      fakeStream,
      undefined,
      expect.any(Function),
    );
    expect(onClose).toHaveBeenCalled();
  });

  it('starts a narrated decline turn and closes', async () => {
    const onClose = vi.fn();
    const fakeStream = {} as ReturnType<IChatHub['sendDeclineQuest']>;
    const sendDeclineQuest = vi.fn().mockReturnValue(fakeStream);
    const { user, chatHub, gameChat } = renderDialog(offerQuest, onClose, { sendDeclineQuest });

    await user.click(screen.getByRole('button', { name: 'Not now' }));

    expect(chatHub.sendDeclineQuest).toHaveBeenCalledWith('quest-id');
    expect(gameChat.submitNarratedTurn).toHaveBeenCalledWith(
      'Decline "A Dangerous Delivery"',
      fakeStream,
    );
    expect(onClose).toHaveBeenCalled();
  });

  it('completes a ready-to-turn-in quest through the REST endpoint and closes', async () => {
    const onClose = vi.fn();
    let requestUrl: URL | undefined;
    server.use(
      handleCompleteQuest(({ request }) => {
        requestUrl = new URL(request.url);
        return new HttpResponse(null, { status: 204 });
      }),
      handleGetQuestJournal({ body: [] }),
    );

    const { user } = renderDialog(turnInQuest, onClose);

    await user.click(screen.getByRole('button', { name: 'Complete quest' }));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(requestUrl?.searchParams.get('worldId')).toBe('world-id');
  });
});
