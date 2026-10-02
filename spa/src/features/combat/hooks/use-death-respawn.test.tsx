import { HubConnectionState } from '@microsoft/signalr';
import { act, render } from '@testing-library/react';
import { useEffect, useState } from 'react';
import { describe, expect, it, vi } from 'vitest';

import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import { GameChatContext } from '@/features/game/hooks/use-game-chat';
import { GameHubConnectionContext } from '@/features/game/hooks/use-game-hub-connection';
import { gameEventBus } from '@/lib/game-event-bus';

import { DeathRespawnEffect } from './use-death-respawn';

function ResolveOnTick({ tick }: { tick: number }) {
  useEffect(() => {
    if (tick > 0) {
      gameEventBus.emit('CombatResolved', 'Defeat');
    }
  }, [tick]);
  return null;
}

function renderGame() {
  const sendRespawn = vi.fn();
  const chatHub = { sendRespawn } as unknown as IChatHub;
  let bumpTick = () => {};

  function Game() {
    const [tick, setTick] = useState(0);
    bumpTick = () => setTick((current) => current + 1);
    // A fresh submitNarratedTurn per render mirrors the real chat builder.
    const chat = { messages: [], isStreaming: false, submitNarratedTurn: vi.fn() };
    return (
      <GameHubConnectionContext.Provider
        value={{ connectionStatus: HubConnectionState.Connected, connectionError: false, chatHub }}
      >
        <GameChatContext.Provider value={chat}>
          <ResolveOnTick tick={tick} />
          <DeathRespawnEffect />
        </GameChatContext.Provider>
      </GameHubConnectionContext.Provider>
    );
  }

  render(<Game />);
  return { sendRespawn, resolveDuringRerender: () => act(() => bumpTick()) };
}

describe('DeathRespawnEffect', () => {
  it('requests a respawn when a defeat resolves in the same render the chat context changes', () => {
    const { sendRespawn, resolveDuringRerender } = renderGame();

    resolveDuringRerender();

    expect(sendRespawn).toHaveBeenCalledTimes(1);
  });
});
