import { useEffect, useRef, useState } from 'react';

import type { PlayerCombatAction } from '@/features/combat/combat-action';
import type { CombatOutcome } from '@/features/combat/combat-outcome';
import { gameEventBus, type ConnectionStatus } from '@/lib/game-event-bus';
import { loadStoredMessages, saveMessages } from '@/lib/session-storage';

import type { ChatMarkerVariant, ChatMessage } from '../components/chat-history';
import { appendTokenToNarrationSegments } from '../narration-markup';
import { formatLocation, locationKey } from '../scene-format';
import { useGameHubConnection } from './use-game-hub-connection';

const CONNECTION_STATUS_TEXT: Record<ConnectionStatus, string> = {
  reconnecting: 'Reconnecting…',
  reconnected: 'Reconnected',
  disconnected: 'Connection lost',
};

const OUTCOME_MARKER: Record<CombatOutcome, string> = {
  Victory: 'Victory!',
  Defeat: 'You have died',
  Fled: 'You escaped',
};

export function useGameChat(sessionId: string) {
  const { isConnected, streamOpening, streamChat, streamCombatAction, streamFlee, endSession } =
    useGameHubConnection(sessionId);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const startedSessionId = useRef<string | null>(null);
  const previousLocation = useRef<string | null>(null);
  const activeNarratorMessageId = useRef<string | null>(null);

  const appendTokenToActiveNarrationMessage = (id: string, token: string) => {
    setMessages((current) =>
      current.map((m) =>
        m.id === id && m.role === 'narrator'
          ? { ...m, segments: appendTokenToNarrationSegments(m.segments, token) }
          : m,
      ),
    );
  };

  const appendChatMarker = (text: string, variant: ChatMarkerVariant) => {
    const marker: ChatMessage = { id: crypto.randomUUID(), role: 'marker', text, variant };
    setMessages((current) => {
      const insertIndex = activeNarratorMessageId.current
        ? current.findIndex((m) => m.id === activeNarratorMessageId.current)
        : -1;
      if (insertIndex === -1) {
        return [...current, marker];
      }
      return [...current.slice(0, insertIndex), marker, ...current.slice(insertIndex)];
    });
  };

  useEffect(
    () =>
      gameEventBus.on('SceneSnapshot', (scene) => {
        const key = locationKey(scene);
        if (previousLocation.current !== null && key !== previousLocation.current) {
          appendChatMarker(formatLocation(scene), 'location');
        }
        previousLocation.current = key;
      }),
    [],
  );

  useEffect(
    () =>
      gameEventBus.on('CombatStarted', () => appendChatMarker('Combat started', 'combat-start')),
    [],
  );

  useEffect(
    () =>
      gameEventBus.on('CombatResolved', (outcome) => {
        appendChatMarker(OUTCOME_MARKER[outcome], 'combat-end');
      }),
    [],
  );

  useEffect(
    () =>
      gameEventBus.on('ConnectionStatusChanged', (status) => {
        appendChatMarker(CONNECTION_STATUS_TEXT[status], status);
      }),
    [],
  );

  useEffect(() => {
    if (!isConnected || startedSessionId.current === sessionId) {
      return;
    }

    startedSessionId.current = sessionId;
    previousLocation.current = null;

    const stored = loadStoredMessages(sessionId);
    if (stored && stored.length > 0) {
      setMessages(stored);
      return;
    }

    setMessages([]);

    const narratorMessageId = crypto.randomUUID();

    setMessages([{ id: narratorMessageId, role: 'narrator', segments: [] }]);

    activeNarratorMessageId.current = narratorMessageId;

    setIsStreaming(true);

    streamOpening(
      (token) => appendTokenToActiveNarrationMessage(narratorMessageId, token),
      () => {
        activeNarratorMessageId.current = null;
        setIsStreaming(false);
      },
    );
  }, [isConnected, sessionId, streamOpening]);

  useEffect(() => {
    if (messages.length > 0) {
      saveMessages(sessionId, messages);
    }
  }, [messages, sessionId]);

  const startTurn = (
    playerInput: string,
    stream: (onToken: (token: string) => void, onComplete: () => void) => void,
  ) => {
    const narratorMessageId = crypto.randomUUID();

    setMessages((current) => [
      ...current,
      { id: crypto.randomUUID(), role: 'player', content: playerInput },
      { id: narratorMessageId, role: 'narrator', segments: [] },
    ]);

    activeNarratorMessageId.current = narratorMessageId;

    setIsStreaming(true);

    stream(
      (token) => appendTokenToActiveNarrationMessage(narratorMessageId, token),
      () => {
        activeNarratorMessageId.current = null;
        setIsStreaming(false);
      },
    );
  };

  const submitChatMessage = (text: string) => {
    if (isStreaming) {
      return;
    }
    startTurn(text, (onToken, onComplete) => streamChat(text, onToken, onComplete));
  };

  const submitCombatAction = (action: PlayerCombatAction, displayText: string) => {
    if (isStreaming) {
      return;
    }
    startTurn(displayText, (onToken, onComplete) =>
      streamCombatAction(action, onToken, onComplete),
    );
  };

  const submitFlee = () => {
    if (isStreaming) {
      return;
    }
    startTurn('Attempts to flee', (onToken, onComplete) => streamFlee(onToken, onComplete));
  };

  return {
    messages,
    isConnected,
    isStreaming,
    submitChatMessage,
    submitCombatAction,
    submitFlee,
    endSession,
  };
}
