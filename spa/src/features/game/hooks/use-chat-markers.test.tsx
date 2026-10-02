import { act, renderHook } from '@testing-library/react';
import { useState } from 'react';
import { describe, expect, it } from 'vitest';

import type { SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { gameEventBus } from '@/lib/game-event-bus';

import type { ChatMessage } from '../components/chat-history';
import { useChatMarkers } from './use-chat-markers';

const scene = (districtName: string) =>
  ({ stateName: 'Northmarch', districtName }) as SceneSnapshot;

function renderMarkers() {
  return renderHook(() => {
    const [messages, setMessages] = useState<ChatMessage[]>([]);
    useChatMarkers(setMessages, { current: null });
    return messages.map((message) => (message.role === 'marker' ? message.text : ''));
  });
}

const emitScene = (snapshot: SceneSnapshot) =>
  act(() => gameEventBus.emit('SceneSnapshot', snapshot));

describe('useChatMarkers location', () => {
  it('adds no marker for the first scene', () => {
    const { result } = renderMarkers();

    emitScene(scene('Market'));

    expect(result.current).toEqual([]);
  });

  it('adds a marker when the player moves to a different place', () => {
    const { result } = renderMarkers();
    emitScene(scene('Market'));

    emitScene(scene('Harbor'));

    expect(result.current).toEqual(['Harbor, Northmarch']);
  });

  it('adds no marker when the scene refreshes in the same place', () => {
    const { result } = renderMarkers();
    emitScene(scene('Market'));

    emitScene(scene('Market'));

    expect(result.current).toEqual([]);
  });
});
