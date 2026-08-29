import {
  useCallback,
  useEffect,
  useRef,
  type Dispatch,
  type RefObject,
  type SetStateAction,
} from 'react';

import { gameEventBus } from '@/lib/game-event-bus';

import { OUTCOME_MARKER } from '../chat-marker-format';
import type { ChatMarkerVariant, ChatMessage } from '../components/chat-history';
import { formatLocation, locationKey } from '../scene-format';

export function useChatMarkers(
  setMessages: Dispatch<SetStateAction<ChatMessage[]>>,
  activeNarratorMessageId: RefObject<string | null>,
) {
  const previousLocation = useRef<string | null>(null);

  const appendChatMarker = useCallback(
    (text: string, variant: ChatMarkerVariant) => {
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
    },
    [setMessages, activeNarratorMessageId],
  );

  useEffect(
    () =>
      gameEventBus.on('SceneSnapshot', (scene) => {
        const key = locationKey(scene);
        if (previousLocation.current !== null && key !== previousLocation.current) {
          appendChatMarker(formatLocation(scene), 'location');
        }
        previousLocation.current = key;
      }),
    [appendChatMarker],
  );

  useEffect(
    () =>
      gameEventBus.on('CombatStarted', () => appendChatMarker('Combat started', 'combat-start')),
    [appendChatMarker],
  );

  useEffect(
    () =>
      gameEventBus.on('CombatOutcomeKnown', (outcome) =>
        appendChatMarker(OUTCOME_MARKER[outcome], 'combat-end'),
      ),
    [appendChatMarker],
  );
}
