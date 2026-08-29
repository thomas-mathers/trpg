import type { IStreamResult, IStreamSubscriber, ISubscription } from '@microsoft/signalr';
import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';

import { loadStoredMessages, saveMessages } from '@/lib/session-storage';

import type { ChatMessage } from '../components/chat-history';
import { appendTokenToNarrationSegments } from '../narration-markup';
import { useChatMarkers } from './use-chat-markers';
import { useChatHub } from './use-game-hub-connection';

export interface GameChat {
  messages: ChatMessage[];
  isStreaming: boolean;
  submitNarratedTurn: (
    displayText: string | null,
    stream: IStreamResult<string>,
    onError?: (error: unknown) => void,
    onSettle?: () => void,
  ) => void;
}

export const GameChatContext = createContext<GameChat | null>(null);

export function useGameChat(): GameChat {
  const context = useContext(GameChatContext);
  if (!context) {
    throw new Error('useGameChat must be used within a GameChatContext provider');
  }
  return context;
}

export function useGameChatBuilder(sessionId: string): GameChat {
  const chatHub = useChatHub();
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const hasStarted = useRef(false);
  const activeNarratorMessageId = useRef<string | null>(null);

  useChatMarkers(setMessages, activeNarratorMessageId);

  const subscribeToStream = useCallback(
    (
      streamResult: IStreamResult<string>,
      onReceiveToken: (token: string) => void,
      onComplete: ((receivedAnyToken: boolean) => void) | undefined,
      onError?: (error: unknown) => void,
    ) => {
      setIsStreaming(true);
      let subscription: ISubscription<string> | null = null;
      let receivedAnyToken = false;

      const subscriber: IStreamSubscriber<string> = {
        complete() {
          subscription?.dispose();
          setIsStreaming(false);
          onComplete?.(receivedAnyToken);
        },
        error(err: unknown) {
          console.error('Error receiving stream response', err);
          subscription?.dispose();
          setIsStreaming(false);
          onError?.(err);
          onComplete?.(receivedAnyToken);
        },
        next(token) {
          receivedAnyToken = true;
          onReceiveToken(token);
        },
      };

      subscription = streamResult.subscribe(subscriber);
    },
    [],
  );

  const appendTokenToActiveNarrationMessage = (id: string, token: string) => {
    setMessages((current) =>
      current.map((m) =>
        m.id === id && m.role === 'narrator'
          ? { ...m, segments: appendTokenToNarrationSegments(m.segments, token) }
          : m,
      ),
    );
  };

  useEffect(() => {
    if (hasStarted.current) {
      return;
    }

    hasStarted.current = true;

    const stored = loadStoredMessages(sessionId);

    if (stored && stored.length > 0) {
      setMessages(stored);
      return;
    }

    const narratorMessageId = crypto.randomUUID();
    setMessages([{ id: narratorMessageId, role: 'narrator', segments: [] }]);

    activeNarratorMessageId.current = narratorMessageId;

    subscribeToStream(
      chatHub.receiveOpening(),
      (token) => appendTokenToActiveNarrationMessage(narratorMessageId, token),
      () => {
        activeNarratorMessageId.current = null;
      },
    );
  }, [chatHub, sessionId, subscribeToStream]);

  useEffect(() => {
    if (messages.length > 0) {
      saveMessages(sessionId, messages);
    }
  }, [messages, sessionId]);

  const startTurn = (
    playerInput: string | null,
    stream: (
      onToken: (token: string) => void,
      onComplete: (receivedAnyToken: boolean) => void,
    ) => void,
    onSettle?: () => void,
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

    stream(
      (token) => appendTokenToActiveNarrationMessage(narratorMessageId, token),
      (receivedAnyToken) => {
        activeNarratorMessageId.current = null;
        // A stream that never yielded a token (e.g. a combat action that didn't conclude the fight) has nothing to show — drop the placeholder bubble it started with.
        if (!receivedAnyToken) {
          setMessages((current) => current.filter((m) => m.id !== narratorMessageId));
        }
        onSettle?.();
      },
    );
  };

  const submitNarratedTurn = (
    displayText: string | null,
    stream: IStreamResult<string>,
    onError?: (error: unknown) => void,
    onSettle?: () => void,
  ) => {
    startTurn(
      displayText,
      (onToken, onComplete) => subscribeToStream(stream, onToken, onComplete, onError),
      onSettle,
    );
  };

  return {
    messages,
    isStreaming,
    submitNarratedTurn,
  };
}
