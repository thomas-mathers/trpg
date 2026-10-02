import { HubConnectionState } from '@microsoft/signalr';
import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { ActionResult } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import {
  GameHubConnectionContext,
  type GameHubConnection,
} from '@/features/game/hooks/use-game-hub-connection';
import { recordInteractions } from '@/test/interaction-handlers';
import { renderWithProviders } from '@/test/test-utils';

import { QuestDialog, type QuestDialogState } from './quest-dialog';

const offerQuest: QuestDialogState = {
  giverId: 'giver-id',
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
      itemNames: null,
    },
  ],
  mode: 'Offer',
};

const turnInQuest: QuestDialogState = { ...offerQuest, mode: 'TurnIn' };

const stealOfferQuest: QuestDialogState = {
  ...offerQuest,
  name: 'A Quiet Job',
  objectives: [
    {
      name: 'Recover 3 items',
      description: 'Quietly recover 3 items from around Stonebridge.',
      requiredAmount: 3,
      itemNames: [
        "Lucan Ashvale's Pocket Watch",
        "The Crooked Chimney's Strongbox",
        "The Striker's Anvil's Ledger",
      ],
    },
  ],
};

function succeeded(): Promise<ActionResult> {
  return Promise.resolve({ succeeded: true });
}

function buildChatHub(overrides: Partial<IChatHub> = {}): IChatHub {
  return {
    endSession: vi.fn(),
    sendChat: vi.fn(),
    sendAcceptQuest: vi.fn(succeeded),
    sendCompleteQuest: vi.fn(succeeded),
    ...overrides,
  } as IChatHub;
}

function renderDialog(
  quest: QuestDialogState | null,
  onClose: () => void,
  chatHubOverrides: Partial<IChatHub> = {},
) {
  const interactions = recordInteractions();
  const chatHub = buildChatHub({
    sendAcceptQuest: vi.fn(() => {
      interactions.calls.push('accept');
      return succeeded();
    }),
    ...chatHubOverrides,
  });
  const hubConnection: GameHubConnection = {
    connectionStatus: HubConnectionState.Connected,
    connectionError: false,
    chatHub,
  };

  const result = renderWithProviders(
    <GameHubConnectionContext.Provider value={hubConnection}>
      <QuestDialog playerId="player-id" quest={quest} onClose={onClose} />
    </GameHubConnectionContext.Provider>,
  );

  return { ...result, chatHub, interactions };
}

describe('QuestDialog', () => {
  it('accepts the quest and closes', async () => {
    const onClose = vi.fn();
    const { user, chatHub } = renderDialog(offerQuest, onClose);

    await user.click(screen.getByRole('button', { name: 'Accept quest' }));
    await waitFor(() => expect(onClose).toHaveBeenCalled());

    expect(chatHub.sendAcceptQuest).toHaveBeenCalledWith('quest-id');
  });

  it('closes without a server call when the offer is declined', async () => {
    const onClose = vi.fn();
    const { user, chatHub } = renderDialog(offerQuest, onClose);

    await user.click(screen.getByRole('button', { name: 'Not now' }));
    await waitFor(() => expect(onClose).toHaveBeenCalled());

    expect(chatHub.sendAcceptQuest).not.toHaveBeenCalled();
    expect(chatHub.sendCompleteQuest).not.toHaveBeenCalled();
  });

  it('completes the quest and closes', async () => {
    const onClose = vi.fn();
    const { user, chatHub } = renderDialog(turnInQuest, onClose);

    await user.click(screen.getByRole('button', { name: 'Complete quest' }));
    await waitFor(() => expect(onClose).toHaveBeenCalled());

    expect(chatHub.sendCompleteQuest).toHaveBeenCalledWith('quest-id');
  });

  it('engages the giver while open and releases them before the accept starts', async () => {
    const { user, interactions } = renderDialog(offerQuest, vi.fn());
    await waitFor(() => expect(interactions.calls).toEqual(['begin:creature:giver-id']));

    await user.click(screen.getByRole('button', { name: 'Accept quest' }));

    await waitFor(() =>
      expect(interactions.calls).toEqual([
        'begin:creature:giver-id',
        'end:creature:giver-id',
        'accept',
      ]),
    );
  });

  it('releases the giver when the dialog unmounts without acting', async () => {
    const { interactions, unmount } = renderDialog(turnInQuest, vi.fn());
    await waitFor(() => expect(interactions.calls).toEqual(['begin:creature:giver-id']));

    unmount();

    await waitFor(() =>
      expect(interactions.calls).toEqual(['begin:creature:giver-id', 'end:creature:giver-id']),
    );
  });

  it('shows each item name when an objective has more than one item', () => {
    renderDialog(stealOfferQuest, vi.fn());

    expect(screen.getByText("Lucan Ashvale's Pocket Watch")).toBeInTheDocument();
    expect(screen.getByText("The Crooked Chimney's Strongbox")).toBeInTheDocument();
    expect(screen.getByText("The Striker's Anvil's Ledger")).toBeInTheDocument();
  });
});
