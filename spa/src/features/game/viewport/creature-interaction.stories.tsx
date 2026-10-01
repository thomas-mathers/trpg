import { HubConnectionState, type IStreamResult } from '@microsoft/signalr';
import { Canvas } from '@react-three/fiber';
import type { Meta, StoryObj } from '@storybook/react-vite';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { HttpResponse } from 'msw';
import { useState } from 'react';

import {
  handleBeginCreatureInteraction,
  handleEndCreatureInteraction,
  handleGetCreatureInventory,
  handleGetTrade,
} from '@/api/client/msw.gen';
import type { SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import { TooltipProvider } from '@/components/ui/tooltip';

import type { ChatMessage } from '../components/chat-history';
import { CreatureInteractionPanel } from '../components/creature-interaction-panel';
import { SceneContext } from '../contexts/scene-context';
import { GameChatContext, type GameChat } from '../hooks/use-game-chat';
import { GameHubConnectionContext } from '../hooks/use-game-hub-connection';
import { CreatureFocusController } from './creature-focus-controller';
import { LocationViewport } from './location-viewport';
import { Ground, Creatures } from './viewport-scene';

const scene = {
  worldId: 'world',
  playerStatus: { id: 'player', posture: 'Standing' },
  nearbyCreatures: [
    {
      id: 'npc',
      name: 'Tessa',
      creatureType: 'Human',
      condition: 'Awake',
      posture: 'Standing',
      tradeWorkstationId: 'shop',
      questMarkers: [],
    },
  ],
  nearbyBuildings: [],
  nearbyProps: [],
  exits: [],
  layout: {
    size: { width: 10, depth: 10 },
    props: [],
    buildings: [],
    connectors: [],
    creatures: [
      { id: 'player', placement: { x: 5, y: 6, angle: 0 } },
      { id: 'npc', placement: { x: 5, y: 3.8, angle: 0 } },
    ],
  },
} as unknown as SceneSnapshot;
const inventory = { gold: 128, items: [], weight: 0, carryingCapacity: null };
function reply(message: string): IStreamResult<string> {
  return {
    subscribe: (subscriber) => {
      subscriber.next?.(
        message.includes('end my conversation')
          ? 'Safe travels.'
          : 'Welcome. What can I do for you?',
      );
      subscriber.complete?.();
      return { dispose() {} };
    },
  } as IStreamResult<string>;
}

function InteractionPreview({ focused = false }: { focused?: boolean }) {
  const [queryClient] = useState(
    () => new QueryClient({ defaultOptions: { queries: { retry: false } } }),
  );
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const chatHub = { sendChat: reply } as IChatHub;
  const gameChat: GameChat = {
    messages,
    isStreaming: false,
    submitNarratedTurn: (text, stream, onError, settle) => {
      if (text)
        setMessages((previous) => [
          ...previous,
          { id: crypto.randomUUID(), role: 'player', content: text },
        ]);
      stream.subscribe({
        next: (content) =>
          setMessages((previous) => [
            ...previous,
            {
              id: crypto.randomUUID(),
              role: 'narrator',
              segments: [{ type: 'text', text: content, insideQuote: false }],
            },
          ]),
        error: (error) => onError?.(error),
        complete: () => settle?.(),
      });
    },
  };
  return (
    <QueryClientProvider client={queryClient}>
      <TooltipProvider>
        <SceneContext.Provider value={scene}>
          <GameHubConnectionContext.Provider
            value={{
              chatHub,
              connectionStatus: HubConnectionState.Connected,
              connectionError: false,
            }}
          >
            <GameChatContext.Provider value={gameChat}>
              <div className="relative h-screen w-screen">
                {focused ? (
                  <FocusedStage />
                ) : (
                  <LocationViewport
                    onQuestDialogRequested={() => {}}
                    onDeliverItemDialogRequested={() => {}}
                  />
                )}
              </div>
            </GameChatContext.Provider>
          </GameHubConnectionContext.Provider>
        </SceneContext.Provider>
      </TooltipProvider>
    </QueryClientProvider>
  );
}
const meta = {
  title: 'Game/Viewport/Creature Interaction',
  component: InteractionPreview,
  parameters: {
    layout: 'fullscreen',
    msw: {
      handlers: [
        handleBeginCreatureInteraction(() => new HttpResponse(null, { status: 204 })),
        handleEndCreatureInteraction(() => new HttpResponse(null, { status: 204 })),
        handleGetCreatureInventory({ body: inventory }),
        handleGetTrade({ body: { playerInventory: inventory, shopInventory: inventory } }),
      ],
    },
  },
} satisfies Meta<typeof InteractionPreview>;
export default meta;
type Story = StoryObj<typeof meta>;
export const ApproachFromBehind: Story = {};

function FocusedStage() {
  const [open, setOpen] = useState(true);
  const focus = open ? { id: 'npc', x: 5, y: 3.8, headHeight: 1.63, facing: Math.PI } : null;
  return (
    <>
      <div className="h-[40%] w-full md:h-full md:w-[calc(100%-28rem)]">
        <Canvas camera={{ position: [5, 1.7, 6], fov: 75 }}>
          <color attach="background" args={['#9bb7d4']} />
          <ambientLight intensity={0.8} />
          <directionalLight position={[2, 6, 8]} intensity={1.2} />
          <Ground size={scene.layout.size} />
          <Creatures
            creatures={scene.layout.creatures}
            playerId="player"
            names={new Map([['npc', 'Tessa']])}
            statuses={scene.nearbyCreatures}
            focus={focus}
          />
          <CreatureFocusController
            focus={focus}
            creatures={scene.layout.creatures}
            statuses={scene.nearbyCreatures}
            enabled={false}
            onTarget={() => {}}
            onFocus={() => {}}
            onRestored={() => {}}
          />
        </Canvas>
      </div>
      {open ? (
        <CreatureInteractionPanel
          scene={scene}
          creature={scene.nearbyCreatures[0]}
          onClose={() => setOpen(false)}
          onQuestDialogRequested={() => {}}
          onDeliverItemDialogRequested={() => {}}
        />
      ) : (
        <button className="absolute top-4 right-4" onClick={() => setOpen(true)}>
          Restart interaction preview
        </button>
      )}
    </>
  );
}
export const FocusedConversation: Story = { args: { focused: true } };
