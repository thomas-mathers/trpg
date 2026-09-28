import {
  useCallback,
  useEffect,
  useRef,
  type Dispatch,
  type RefObject,
  type SetStateAction,
} from 'react';

import type { WeatherCondition } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { gameEventBus } from '@/lib/game-event-bus';

import { formatMovement, OUTCOME_MARKER, WEATHER_MARKER } from '../chat-marker-format';
import type { ChatMarkerVariant, ChatMessage } from '../components/chat-history';
import { formatLocation, locationKey } from '../scene-format';

export function useChatMarkers(
  setMessages: Dispatch<SetStateAction<ChatMessage[]>>,
  activeNarratorMessageId: RefObject<string | null>,
) {
  const previousLocation = useRef<string | null>(null);
  const previousWeather = useRef<WeatherCondition | null>(null);

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
        const weather = scene.weather ?? null;
        const sameLocation = previousLocation.current === key;
        if (previousLocation.current !== null && !sameLocation) {
          appendChatMarker(formatLocation(scene), 'location');
        }
        const weatherChanged =
          weather !== null &&
          previousWeather.current !== null &&
          weather !== previousWeather.current;
        if (sameLocation && weatherChanged) {
          appendChatMarker(WEATHER_MARKER[weather], 'weather');
        }
        previousLocation.current = key;
        previousWeather.current = weather;
      }),
    [appendChatMarker],
  );

  useEffect(
    () =>
      gameEventBus.on('CreaturesMoved', (movement) =>
        appendChatMarker(
          formatMovement(movement),
          movement.direction === 'Arrived' ? 'arrival' : 'departure',
        ),
      ),
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
