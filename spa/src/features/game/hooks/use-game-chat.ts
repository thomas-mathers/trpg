import { useEffect, useRef, useState } from 'react';

import type { PlayerCombatAction } from '@/features/combat/combat-action';
import type { TerminalCombatOutcome } from '@/features/combat/combat-outcome';
import type { PlayerEncounterAction } from '@/features/encounters/encounter';
import { gameEventBus, type ConnectionStatus } from '@/lib/game-event-bus';
import { loadStoredMessages, saveMessages } from '@/lib/session-storage';

import type { ChatMarkerVariant, ChatMessage } from '../components/chat-history';
import { appendTokenToNarrationSegments } from '../narration-markup';
import { formatLocation, locationKey } from '../scene-format';
import { useGameHubConnection } from './use-game-hub-connection';

const OUTCOME_MARKER: Record<TerminalCombatOutcome, string> = {
  Victory: 'Victory!',
  Defeat: 'You have died',
  Fled: 'You escaped',
};

export interface GameChat {
  messages: ChatMessage[];
  isConnected: boolean;
  connectionStatus: ConnectionStatus;
  isStreaming: boolean;
  submitChatMessage: (text: string) => void;
  submitEncounterAction: (action: PlayerEncounterAction, displayText: string) => void;
  submitFlee: () => void;
  submitCombatAction: (action: PlayerCombatAction) => Promise<void>;
  endSession: () => Promise<void>;
}

export function useGameChat(sessionId: string): GameChat {
  const {
    connectionStatus,
    isConnected,
    streamOpening,
    streamChat,
    streamFlee,
    streamEncounterAction,
    resolveCombatAction,
    endSession,
  } = useGameHubConnection(sessionId);
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
      gameEventBus.on('CombatResolved', (outcome) =>
        appendChatMarker(OUTCOME_MARKER[outcome], 'combat-end'),
      ),
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
    playerInput: string | null,
    stream: (onToken: (token: string) => void, onComplete: () => void) => void,
  ) => {
    const narratorMessageId = crypto.randomUUID();

    setMessages((current) => [
      ...current,
      ...(playerInput
        ? [{ id: crypto.randomUUID(), role: 'player' as const, content: playerInput }]
        : []),
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

  const submitFlee = () => {
    if (isStreaming) {
      return;
    }
    startTurn(null, (onToken, onComplete) => streamFlee(onToken, onComplete));
  };

  const submitEncounterAction = (action: PlayerEncounterAction, displayText: string) => {
    if (isStreaming) {
      return;
    }
    startTurn(displayText, (onToken, onComplete) =>
      streamEncounterAction(action, onToken, onComplete),
    );
  };

  const submitCombatAction = (action: PlayerCombatAction) => resolveCombatAction(action);

  return {
    messages,
    isConnected,
    connectionStatus,
    isStreaming,
    submitChatMessage,
    submitEncounterAction,
    submitFlee,
    submitCombatAction,
    endSession,
  };
}
