import { act, renderHook } from '@testing-library/react';
import { useState } from 'react';
import { describe, expect, it } from 'vitest';

import type {
  CreaturesMoved,
  SceneSnapshot,
  WeatherCondition,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { gameEventBus } from '@/lib/game-event-bus';

import type { ChatMessage } from '../components/chat-history';
import { useChatMarkers } from './use-chat-markers';

const scene = (districtName: string, weather?: WeatherCondition) =>
  ({ stateName: 'Northmarch', districtName, weather }) as SceneSnapshot;

function renderMarkers() {
  return renderHook(() => {
    const [messages, setMessages] = useState<ChatMessage[]>([]);
    useChatMarkers(setMessages, { current: null });
    return messages.map((message) => (message.role === 'marker' ? message.text : ''));
  });
}

const emitScene = (snapshot: SceneSnapshot) =>
  act(() => gameEventBus.emit('SceneSnapshot', snapshot));

const emitMoved = (movement: CreaturesMoved) =>
  act(() => gameEventBus.emit('CreaturesMoved', movement));

describe('useChatMarkers weather', () => {
  it('adds a marker when the weather changes in the same place', () => {
    const { result } = renderMarkers();
    emitScene(scene('Market', 'Clear'));

    emitScene(scene('Market', 'Rain'));

    expect(result.current).toEqual(['Rain begins to fall']);
  });

  it('adds no weather marker when the player moved to a different place', () => {
    const { result } = renderMarkers();
    emitScene(scene('Market', 'Clear'));

    emitScene(scene('Harbor', 'Rain'));

    expect(result.current).toEqual(['Harbor, Northmarch']);
  });

  it('adds no marker when the weather is unchanged', () => {
    const { result } = renderMarkers();
    emitScene(scene('Market', 'Clear'));

    emitScene(scene('Market', 'Clear'));

    expect(result.current).toEqual([]);
  });

  it('adds no marker while the player is indoors and the weather is unknown', () => {
    const { result } = renderMarkers();
    emitScene(scene('Market', 'Clear'));

    emitScene(scene('Market'));

    expect(result.current).toEqual([]);
  });
});

describe('useChatMarkers creature movement', () => {
  it('says where an arriving creature came from', () => {
    const { result } = renderMarkers();

    emitMoved({ direction: 'Arrived', names: ['Marta'], placeName: 'the old mill road' });

    expect(result.current).toEqual(['Marta arrived from the old mill road']);
  });

  it('says where a departing creature is heading', () => {
    const { result } = renderMarkers();

    emitMoved({ direction: 'Departed', names: ['Marta'], placeName: 'the old mill road' });

    expect(result.current).toEqual(['Marta left, heading towards the old mill road']);
  });

  it('names every mover in one marker and drops an unknown place', () => {
    const { result } = renderMarkers();

    emitMoved({ direction: 'Arrived', names: ['Marta', 'Otto', 'Ilse'] });

    expect(result.current).toEqual(['Marta, Otto and Ilse arrived']);
  });
});
