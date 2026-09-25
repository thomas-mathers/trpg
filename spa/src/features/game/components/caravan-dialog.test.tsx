import { HubConnectionState } from '@microsoft/signalr';
import { screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { NearbyCaravanSnapshot } from '@/api/client';
import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import { GameChatContext, type GameChat } from '@/features/game/hooks/use-game-chat';
import {
  GameHubConnectionContext,
  type GameHubConnection,
} from '@/features/game/hooks/use-game-hub-connection';
import { renderWithProviders } from '@/test/test-utils';

import { CaravanDialog } from './caravan-dialog';

const caravan: NearbyCaravanSnapshot = {
  caravanId: 'caravan-id',
  routeName: 'The Capital Circuit',
  ticketFeeGold: 10,
  minutesUntilDeparture: 15,
  passengerServiceAvailable: true,
  destinations: [
    {
      locationId: 'without-ticket-id',
      locationName: 'Stonebridge',
      travelTimeHours: 4,
      hasTicket: false,
    },
    {
      locationId: 'with-ticket-id',
      locationName: 'Ravenhollow',
      travelTimeHours: 6,
      hasTicket: true,
    },
  ],
};

function buildChatHub(overrides: Partial<IChatHub> = {}): IChatHub {
  return {
    endSession: vi.fn(),
    receiveOpening: vi.fn(),
    sendChat: vi.fn(),
    sendPurchaseCaravanTicket: vi.fn(),
    sendBoardCaravan: vi.fn(),
    sendDeclineCaravanTicket: vi.fn(),
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
  onClose: () => void,
  chatHubOverrides: Partial<IChatHub> = {},
  gameChatOverrides: Partial<GameChat> = {},
  selectedCaravan: NearbyCaravanSnapshot = caravan,
) {
  const chatHub = buildChatHub(chatHubOverrides);
  const gameChat = buildGameChat(gameChatOverrides);
  const hubConnection: GameHubConnection = {
    connectionStatus: HubConnectionState.Connected,
    connectionError: false,
    chatHub,
  };

  const result = renderWithProviders(
    <GameHubConnectionContext.Provider value={hubConnection}>
      <GameChatContext.Provider value={gameChat}>
        <CaravanDialog caravan={selectedCaravan} onClose={onClose} />
      </GameChatContext.Provider>
    </GameHubConnectionContext.Provider>,
  );

  return { ...result, chatHub, gameChat };
}

describe('CaravanDialog', () => {
  it('starts a narrated purchase turn and leaves the dialog open', async () => {
    const onClose = vi.fn();
    const fakeStream = {} as ReturnType<IChatHub['sendPurchaseCaravanTicket']>;
    const sendPurchaseCaravanTicket = vi.fn().mockReturnValue(fakeStream);
    const { user, chatHub, gameChat } = renderDialog(onClose, { sendPurchaseCaravanTicket });

    await user.click(screen.getByRole('button', { name: 'Buy ticket' }));

    expect(chatHub.sendPurchaseCaravanTicket).toHaveBeenCalledWith(
      'caravan-id',
      'without-ticket-id',
    );
    expect(gameChat.submitNarratedTurn).toHaveBeenCalledWith(
      'Buy a caravan ticket to Stonebridge',
      fakeStream,
    );
    expect(onClose).not.toHaveBeenCalled();
  });

  it('starts a narrated board turn and closes', async () => {
    const onClose = vi.fn();
    const fakeStream = {} as ReturnType<IChatHub['sendBoardCaravan']>;
    const sendBoardCaravan = vi.fn().mockReturnValue(fakeStream);
    const { user, chatHub, gameChat } = renderDialog(onClose, { sendBoardCaravan });

    await user.click(screen.getByRole('button', { name: 'Board' }));

    expect(chatHub.sendBoardCaravan).toHaveBeenCalledWith('caravan-id');
    expect(gameChat.submitNarratedTurn).toHaveBeenCalledWith(
      'Board the caravan to Ravenhollow',
      fakeStream,
    );
    expect(onClose).toHaveBeenCalled();
  });

  it('starts a narrated decline turn and closes', async () => {
    const onClose = vi.fn();
    const fakeStream = {} as ReturnType<IChatHub['sendDeclineCaravanTicket']>;
    const sendDeclineCaravanTicket = vi.fn().mockReturnValue(fakeStream);
    const { user, chatHub, gameChat } = renderDialog(onClose, { sendDeclineCaravanTicket });

    await user.click(screen.getByRole('button', { name: 'No thanks' }));

    expect(chatHub.sendDeclineCaravanTicket).toHaveBeenCalledWith();
    expect(gameChat.submitNarratedTurn).toHaveBeenCalledWith(
      'Decline the caravan ticket',
      fakeStream,
    );
    expect(onClose).toHaveBeenCalled();
  });

  it('disables every action button while a turn is streaming', () => {
    renderDialog(vi.fn(), {}, { isStreaming: true });

    expect(screen.getByRole('button', { name: 'Buy ticket' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Board' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'No thanks' })).toBeDisabled();
  });

  it('disables purchase and boarding while passenger service is suspended', () => {
    renderDialog(vi.fn(), {}, {}, { ...caravan, passengerServiceAvailable: false });

    expect(
      screen.getByText('Passenger service is suspended until the weather improves.'),
    ).toBeVisible();
    expect(screen.getByRole('button', { name: 'Buy ticket' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Board' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'No thanks' })).toBeEnabled();
  });
});
