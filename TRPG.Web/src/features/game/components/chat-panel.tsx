import { useNavigate, useParams } from '@tanstack/react-router';
import { useState, useRef, useEffect } from 'react';

import { type PlayerCombatAction, formatCombatAction } from '@/features/combat/combat-action';
import { CombatConsole } from '@/features/combat/components/combat-console';
import { useCombatState } from '@/features/combat/hooks/use-combat-state';
import { gameEventBus, type ConnectionStatus } from '@/lib/game-event-bus';
import { clearStoredMessages, loadStoredMessages, saveMessages } from '@/lib/session-storage';

import { useGameHubConnection } from '../hooks/use-game-hub-connection';
import { useSceneQuery } from '../hooks/use-scene-query';
import { appendNarrationToken } from '../narration-markup';
import { locationKey, formatLocation } from '../scene-format';
import { ChatHistory, type ChatMarkerVariant, type ChatMessage } from './chat-history';
import { ChatInput } from './chat-input';

const CONNECTION_STATUS_TEXT: Record<ConnectionStatus, string> = {
  reconnecting: 'Reconnecting…',
  reconnected: 'Reconnected',
  disconnected: 'Connection lost',
};

export function ChatPanel() {
  const { sessionId } = useParams({ from: '/session/$sessionId' });
  const { isConnected, streamOpening, streamChat, streamCombatAction, streamFlee } =
    useGameHubConnection();
  const sceneQuery = useSceneQuery(sessionId);
  const {
    fight,
    isInCombat,
    activeAttackerId,
    cardEffects,
    isPlayingBack,
    combatOutcome,
    dismissOutcome,
  } = useCombatState();
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [input, setInput] = useState('');
  const startedSessionId = useRef<string | null>(null);
  const lastLocationKey = useRef<string | null>(null);
  const currentNarratorId = useRef<string | null>(null);

  const appendToken = (id: string, token: string) => {
    setMessages((current) =>
      current.map((m) =>
        m.id === id && m.role === 'narrator'
          ? { ...m, segments: appendNarrationToken(m.segments, token) }
          : m,
      ),
    );
  };

  const appendMarker = (text: string, variant: ChatMarkerVariant) => {
    const marker: ChatMessage = { id: crypto.randomUUID(), role: 'marker', text, variant };
    setMessages((current) => {
      const insertIndex = currentNarratorId.current
        ? current.findIndex((m) => m.id === currentNarratorId.current)
        : -1;
      if (insertIndex === -1) {
        return [...current, marker];
      }
      return [...current.slice(0, insertIndex), marker, ...current.slice(insertIndex)];
    });
  };

  useEffect(() => {
    if (sceneQuery.data && lastLocationKey.current === null) {
      lastLocationKey.current = locationKey(sceneQuery.data);
    }
  }, [sceneQuery.data]);

  useEffect(
    () =>
      gameEventBus.on('SceneChanged', (scene) => {
        const key = locationKey(scene);
        if (lastLocationKey.current !== null && key !== lastLocationKey.current) {
          appendMarker(formatLocation(scene), 'location');
        }
        lastLocationKey.current = key;
      }),
    [],
  );

  useEffect(() => {
    if (!isConnected || startedSessionId.current === sessionId) {
      return;
    }

    startedSessionId.current = sessionId;
    lastLocationKey.current = null;

    const stored = loadStoredMessages(sessionId);
    if (stored && stored.length > 0) {
      setMessages(stored);
      return;
    }

    setMessages([]);

    const narratorId = crypto.randomUUID();
    setMessages([{ id: narratorId, role: 'narrator', segments: [] }]);
    currentNarratorId.current = narratorId;

    setIsStreaming(true);

    streamOpening(
      (token) => appendToken(narratorId, token),
      () => {
        currentNarratorId.current = null;
        setIsStreaming(false);
      },
    );
  }, [isConnected, sessionId, streamOpening]);

  useEffect(() => {
    if (messages.length > 0) {
      saveMessages(sessionId, messages);
    }
  }, [messages, sessionId]);

  const runTurn = (
    playerLineText: string,
    stream: (onToken: (token: string) => void, onComplete: () => void) => void,
  ) => {
    const playerMessageId = crypto.randomUUID();
    const narratorId = crypto.randomUUID();
    setMessages((current) => [
      ...current,
      { id: playerMessageId, role: 'player', content: playerLineText },
      { id: narratorId, role: 'narrator', segments: [] },
    ]);
    currentNarratorId.current = narratorId;

    setIsStreaming(true);

    stream(
      (token) => appendToken(narratorId, token),
      () => {
        currentNarratorId.current = null;
        setIsStreaming(false);
      },
    );
  };

  const handleSend = () => {
    const text = input.trim();
    if (!text || isStreaming) {
      return;
    }

    setInput('');
    runTurn(text, (onToken, onComplete) => streamChat(text, onToken, onComplete));
  };

  const handleCombatAction = (action: PlayerCombatAction) => {
    if (isStreaming) {
      return;
    }

    runTurn(formatCombatAction(action, fight), (onToken, onComplete) =>
      streamCombatAction(action, onToken, onComplete),
    );
  };

  const handleFlee = () => {
    if (isStreaming) {
      return;
    }

    runTurn('Attempts to flee', (onToken, onComplete) => streamFlee(onToken, onComplete));
  };

  return (
    <>
      <ChatHistory sessionId={sessionId} messages={messages} />
      <div className="mx-auto w-full max-w-2xl p-4">
        {isInCombat && fight && sceneQuery.data ? (
          <CombatConsole
            playerId={sceneQuery.data.playerStatus.id}
            fight={fight}
            disabled={isStreaming || isPlayingBack}
            onUseAbility={handleCombatAction}
            onUseItem={handleCombatAction}
            onFlee={handleFlee}
            activeAttackerId={activeAttackerId}
            cardEffects={cardEffects}
            outcome={combatOutcome}
            onDismissOutcome={dismissOutcome}
            onExitToMenu={handleExitToMenu}
          />
        ) : (
          <ChatInput
            value={input}
            disabled={!isConnected || isStreaming}
            onChange={setInput}
            onSubmit={handleSend}
          />
        )}
      </div>
    </>
  );
}
