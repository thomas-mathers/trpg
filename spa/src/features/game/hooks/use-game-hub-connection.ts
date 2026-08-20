import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type IRetryPolicy,
} from '@microsoft/signalr';
import { createContext, useContext, useEffect, useRef, useState } from 'react';

import { getHubProxyFactory, getReceiverRegister } from '@/api/signalr-client/TypedSignalR.Client';
import type {
  IChatHub,
  IGameClient,
} from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import { gameEventBus } from '@/lib/game-event-bus';

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

    const gameClient: IGameClient = {
      sceneSnapshot: async (snapshot) => gameEventBus.emit('SceneSnapshot', snapshot),
      combatStarted: async (combatants) => gameEventBus.emit('CombatStarted', combatants),
      combatUpdated: async (update) => gameEventBus.emit('CombatUpdated', update),
      hostileEncounterStarted: async (encounter) =>
        gameEventBus.emit('HostileEncounterStarted', encounter),
      hostileEncounterResolved: async (fact) => gameEventBus.emit('HostileEncounterResolved', fact),
      guardEncounterStarted: async (encounter) =>
        gameEventBus.emit('GuardEncounterStarted', encounter),
      guardEncounterResolved: async (fact) => gameEventBus.emit('GuardEncounterResolved', fact),
      skillLevelUp: async (skillLevelUp) => gameEventBus.emit('SkillLevelUp', skillLevelUp),
      characterLevelUp: async (characterLevelUp) =>
        gameEventBus.emit('CharacterLevelUp', characterLevelUp),
      questDialogRequested: async (questDialog) =>
        gameEventBus.emit('QuestDialogRequested', questDialog),
      questObjectiveCompleted: async (objective) =>
        gameEventBus.emit('QuestObjectiveCompleted', objective),
      questJournalUpdated: async () => gameEventBus.emit('QuestJournalUpdated'),
    };
    const receiverSubscription = getReceiverRegister('IGameClient').register(
      connection,
      gameClient,
    );

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
      receiverSubscription.dispose();
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
