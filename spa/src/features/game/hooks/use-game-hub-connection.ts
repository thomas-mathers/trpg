import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type IRetryPolicy,
} from '@microsoft/signalr';
import { createContext, useContext, useEffect, useRef, useState } from 'react';

import type { CombatantState, SceneSnapshot } from '@/api/client';
import { getHubProxyFactory } from '@/api/signalr-client/TypedSignalR.Client';
import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import type { CombatUpdatePayload } from '@/features/combat/combat-round-event';
import type {
  EncounterResolutionFact,
  HostileEncounterState,
} from '@/features/encounters/encounter';
import {
  gameEventBus,
  type CharacterLevelUp,
  type QuestDialogRequested,
  type SkillLevelUp,
} from '@/lib/game-event-bus';

export interface GameHubConnection {
  connectionStatus: HubConnectionState;
  connectionError: boolean;
  chatHub: IChatHub | null;
}

export const GameHubConnectionContext = createContext<GameHubConnection | null>(null);

export function useGameHubConnection(): GameHubConnection {
  const context = useContext(GameHubConnectionContext);
  if (!context) {
    throw new Error('useGameHubConnection must be used within a GameHubConnectionContext provider');
  }
  return context;
}

export function useChatHub(): IChatHub {
  const { chatHub } = useGameHubConnection();
  if (!chatHub) {
    throw new Error('useChatHub must be used after the hub connection has been established');
  }
  return chatHub;
}

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL as string;

const RECONNECTION_MAX_ATTEMPTS = 3;
const RECONNECTION_DELAY_MS = 5000;

const reconnectPolicy: IRetryPolicy = {
  nextRetryDelayInMilliseconds(retryContext) {
    console.warn(`SignalR reconnect attempt #${retryContext.previousRetryCount + 1}`);

    if (retryContext.previousRetryCount >= RECONNECTION_MAX_ATTEMPTS) {
      return null;
    }

    return RECONNECTION_DELAY_MS;
  },
};

export function useConnectToHub(sessionId: string): GameHubConnection {
  const hubConnection = useRef<HubConnection | null>(null);
  const [chatHub, setChatHub] = useState<IChatHub | null>(null);
  const isIntentionalStop = useRef(false);
  const [connectionStatus, setConnectionStatus] = useState<HubConnectionState>(
    HubConnectionState.Disconnected,
  );
  const [connectionError, setConnectionError] = useState<boolean>(false);

  useEffect(() => {
    isIntentionalStop.current = false;

    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBaseUrl}/hubs/chat?sessionId=${encodeURIComponent(sessionId)}`)
      .withAutomaticReconnect(reconnectPolicy)
      .configureLogging(LogLevel.Information)
      .build();

    hubConnection.current = connection;

    connection.onreconnecting(() => {
      setConnectionStatus(connection.state);
      gameEventBus.emit('ConnectionStatusChanged', 'reconnecting');
    });
    connection.onreconnected(() => {
      setConnectionStatus(connection.state);
      setConnectionError(false);
      gameEventBus.emit('ConnectionStatusChanged', 'reconnected');
    });
    connection.onclose((e) => {
      setConnectionStatus(connection.state);
      if (!isIntentionalStop.current) {
        console.error('SignalR connection lost', e);
        setConnectionError(true);
        gameEventBus.emit('ConnectionStatusChanged', 'disconnected');
      }
    });

    connection.on('SceneSnapshot', (payload: SceneSnapshot) =>
      gameEventBus.emit('SceneSnapshot', payload),
    );
    connection.on('CombatStarted', (payload: CombatantState[]) =>
      gameEventBus.emit('CombatStarted', payload),
    );
    connection.on('CombatUpdated', (payload: CombatUpdatePayload) =>
      gameEventBus.emit('CombatUpdated', payload),
    );
    connection.on('EncounterStarted', (payload: HostileEncounterState) =>
      gameEventBus.emit('EncounterStarted', payload),
    );
    connection.on('EncounterResolved', (payload: EncounterResolutionFact) =>
      gameEventBus.emit('EncounterResolved', payload),
    );
    connection.on('SkillLevelUp', (payload: SkillLevelUp) =>
      gameEventBus.emit('SkillLevelUp', payload),
    );
    connection.on('CharacterLevelUp', (payload: CharacterLevelUp) =>
      gameEventBus.emit('CharacterLevelUp', payload),
    );
    connection.on('QuestDialogRequested', (payload: QuestDialogRequested) =>
      gameEventBus.emit('QuestDialogRequested', payload),
    );
    connection.on('QuestObjectiveCompleted', (payload) =>
      gameEventBus.emit('QuestObjectiveCompleted', payload),
    );
    connection.on('QuestJournalUpdated', () => gameEventBus.emit('QuestJournalUpdated'));

    connection
      .start()
      .then(() => {
        setConnectionStatus(connection.state);
        setConnectionError(false);
        setChatHub(getHubProxyFactory('IChatHub').createHubProxy(connection));
      })
      .catch((e) => {
        console.error('Error connecting to game hub', e);
        setConnectionStatus(connection.state);
        setConnectionError(true);
      });

    return () => {
      isIntentionalStop.current = true;
      connection.stop();
      setConnectionStatus(HubConnectionState.Disconnected);
      hubConnection.current = null;
      setChatHub(null);
    };
  }, [sessionId]);

  return {
    connectionStatus,
    connectionError,
    chatHub,
  };
}
