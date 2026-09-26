import { HubConnectionState } from '@microsoft/signalr';
import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { ItemDetail } from '@/api/client';
import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import { GameChatContext, type GameChat } from '@/features/game/hooks/use-game-chat';
import {
  GameHubConnectionContext,
  type GameHubConnection,
} from '@/features/game/hooks/use-game-hub-connection';
import { recordInteractions } from '@/test/interaction-handlers';
import { renderWithProviders } from '@/test/test-utils';

import { DeliverItemDialog, type DeliverItemDialogState } from './deliver-item-dialog';

const deliverable = {
  recipientId: 'recipient-id',
  worldId: 'world-id',
  questName: 'A Dangerous Delivery',
  item: {
    $type: 'Misc',
    itemId: 'letter-id',
    name: 'Sealed Letter',
    description: 'A letter with a wax seal.',
    weight: 0,
    quantity: 1,
    equippedSlot: null,
    type: 'Misc',
    rarity: null,
    goldValue: 0,
    modifiers: [],
    isStackable: false,
  } as unknown as ItemDetail,
} as DeliverItemDialogState;

function renderDialog(onClose: () => void) {
  const interactions = recordInteractions();
  const chatHub = { sendDeliverItem: vi.fn() } as unknown as IChatHub;
  const gameChat: GameChat = {
    messages: [],
    isStreaming: false,
    submitNarratedTurn: vi.fn(() => {
      interactions.calls.push('turn');
    }),
  };
  const hubConnection: GameHubConnection = {
    connectionStatus: HubConnectionState.Connected,
    connectionError: false,
    chatHub,
  };

  const result = renderWithProviders(
    <GameHubConnectionContext.Provider value={hubConnection}>
      <GameChatContext.Provider value={gameChat}>
        <DeliverItemDialog playerId="player-id" deliverable={deliverable} onClose={onClose} />
      </GameChatContext.Provider>
    </GameHubConnectionContext.Provider>,
  );

  return { ...result, chatHub, gameChat, interactions };
}

describe('DeliverItemDialog', () => {
  it('engages the recipient while open', async () => {
    const { interactions } = renderDialog(vi.fn());

    await waitFor(() => expect(interactions.calls).toEqual(['begin:creature:recipient-id']));
  });

  it('releases the recipient before the delivery turn starts', async () => {
    const onClose = vi.fn();
    const { user, chatHub, interactions } = renderDialog(onClose);
    await waitFor(() => expect(interactions.calls).toEqual(['begin:creature:recipient-id']));

    await user.click(screen.getByRole('button', { name: 'Give' }));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(chatHub.sendDeliverItem).toHaveBeenCalledWith('recipient-id');
    expect(interactions.calls).toEqual([
      'begin:creature:recipient-id',
      'end:creature:recipient-id',
      'turn',
    ]);
  });

  it('releases the recipient when the dialog unmounts', async () => {
    const { interactions, unmount } = renderDialog(vi.fn());
    await waitFor(() => expect(interactions.calls).toEqual(['begin:creature:recipient-id']));

    unmount();

    await waitFor(() =>
      expect(interactions.calls).toEqual([
        'begin:creature:recipient-id',
        'end:creature:recipient-id',
      ]),
    );
  });
});
