import { HubConnectionState } from '@microsoft/signalr';
import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { NearbyCaravanSnapshot } from '@/api/client';
import type { ActionResult, SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import { SceneContext } from '@/features/game/contexts/scene-context';
import {
  GameHubConnectionContext,
  type GameHubConnection,
} from '@/features/game/hooks/use-game-hub-connection';
import { recordInteractions } from '@/test/interaction-handlers';
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

function succeeded(): Promise<ActionResult> {
  return Promise.resolve({ succeeded: true });
}

const scene = { worldId: 'world-id', playerStatus: { id: 'player-id' } } as SceneSnapshot;

function buildChatHub(overrides: Partial<IChatHub> = {}): IChatHub {
  return {
    endSession: vi.fn(),
    sendChat: vi.fn(),
    sendPurchaseCaravanTicket: vi.fn(succeeded),
    sendBoardCaravan: vi.fn(succeeded),
    ...overrides,
  } as IChatHub;
}

function renderDialog(
  onClose: () => void,
  chatHubOverrides: Partial<IChatHub> = {},
  selectedCaravan: NearbyCaravanSnapshot = caravan,
  interactionOptions?: { refuseBegin: boolean },
) {
  const interactions = recordInteractions(interactionOptions);
  const chatHub = buildChatHub(chatHubOverrides);
  const hubConnection: GameHubConnection = {
    connectionStatus: HubConnectionState.Connected,
    connectionError: false,
    chatHub,
  };

  const result = renderWithProviders(
    <SceneContext.Provider value={scene}>
      <GameHubConnectionContext.Provider value={hubConnection}>
        <CaravanDialog caravan={selectedCaravan} onClose={onClose} />
      </GameHubConnectionContext.Provider>
    </SceneContext.Provider>,
  );

  return { ...result, chatHub, interactions };
}

describe('CaravanDialog', () => {
  it('engages the caravan while open and releases it when closed', async () => {
    const { interactions, unmount } = renderDialog(vi.fn());

    await waitFor(() => expect(interactions.calls).toEqual(['begin:caravan:caravan-id']));
    unmount();

    await waitFor(() =>
      expect(interactions.calls).toEqual(['begin:caravan:caravan-id', 'end:caravan:caravan-id']),
    );
  });

  it('stays usable and never releases when the caravan cannot be engaged', async () => {
    const { interactions, unmount } = renderDialog(vi.fn(), {}, caravan, {
      refuseBegin: true,
    });

    await waitFor(() => expect(interactions.calls).toEqual(['begin:caravan:caravan-id']));
    expect(screen.getByRole('button', { name: 'Buy ticket' })).toBeEnabled();
    unmount();
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(interactions.calls).toEqual(['begin:caravan:caravan-id']);
  });

  it('buys a ticket and leaves the dialog open', async () => {
    const onClose = vi.fn();
    const { user, chatHub } = renderDialog(onClose);

    await user.click(screen.getByRole('button', { name: 'Buy ticket' }));

    expect(chatHub.sendPurchaseCaravanTicket).toHaveBeenCalledWith(
      'caravan-id',
      'without-ticket-id',
    );
    expect(onClose).not.toHaveBeenCalled();
  });

  it('boards and closes once boarding succeeds', async () => {
    const onClose = vi.fn();
    const { user, chatHub } = renderDialog(onClose);

    await user.click(screen.getByRole('button', { name: 'Board' }));

    expect(chatHub.sendBoardCaravan).toHaveBeenCalledWith('caravan-id');
    await waitFor(() => expect(onClose).toHaveBeenCalled());
  });

  it('stays open when boarding is refused', async () => {
    const onClose = vi.fn();
    const sendBoardCaravan = vi
      .fn()
      .mockResolvedValue({ succeeded: false, reason: 'CaravanNotPresent' });
    const { user } = renderDialog(onClose, { sendBoardCaravan });

    await user.click(screen.getByRole('button', { name: 'Board' }));

    await waitFor(() => expect(sendBoardCaravan).toHaveBeenCalled());
    expect(onClose).not.toHaveBeenCalled();
  });

  it('closes without a server call when declining', async () => {
    const onClose = vi.fn();
    const { user } = renderDialog(onClose);

    await user.click(screen.getByRole('button', { name: 'No thanks' }));

    expect(onClose).toHaveBeenCalled();
  });

  it('disables ticket buttons while a request is in flight', async () => {
    const sendPurchaseCaravanTicket = vi.fn(() => new Promise<ActionResult>(() => undefined));
    const { user } = renderDialog(vi.fn(), { sendPurchaseCaravanTicket });

    await user.click(screen.getByRole('button', { name: 'Buy ticket' }));

    expect(screen.getByRole('button', { name: 'Buy ticket' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Board' })).toBeDisabled();
  });
});
