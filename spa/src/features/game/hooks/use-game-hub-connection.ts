import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
  type IRetryPolicy,
  type IStreamSubscriber,
  type ISubscription,
} from '@microsoft/signalr';
import { useRef, useEffect, useState, useCallback } from 'react';

import type { FightState, SceneSnapshot } from '@/api/client';
import type { PlayerCombatAction } from '@/features/combat/combat-action';
import type { CombatOutcome } from '@/features/combat/combat-outcome';
import type { CombatUpdatePayload } from '@/features/combat/combat-round-event';
import { gameEventBus } from '@/lib/game-event-bus';

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

export function useGameHubConnection(sessionId: string | null) {
  const hubConnection = useRef<HubConnection | null>(null);
  const isIntentionalStop = useRef(false);
  const [isConnected, setIsConnected] = useState(false);
  const [error, setError] = useState<boolean>(false);

  useEffect(() => {
    if (!sessionId) {
      return;
    }

    isIntentionalStop.current = false;

    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBaseUrl}/hubs/chat?sessionId=${encodeURIComponent(sessionId)}`)
      .withAutomaticReconnect(reconnectPolicy)
      .configureLogging(LogLevel.Information)
      .build();

    hubConnection.current = connection;

    connection.onreconnecting(() => {
      setIsConnected(false);
      gameEventBus.emit('ConnectionStatusChanged', 'reconnecting');
    });
    connection.onreconnected(() => {
      setIsConnected(true);
      setError(false);
      gameEventBus.emit('ConnectionStatusChanged', 'reconnected');
    });
    connection.onclose((e) => {
      setIsConnected(false);
      if (!isIntentionalStop.current) {
        console.error('SignalR connection lost', e);
        setError(true);
        gameEventBus.emit('ConnectionStatusChanged', 'disconnected');
      }
    });

    connection.on('SceneSnapshot', (payload: SceneSnapshot) =>
      gameEventBus.emit('SceneSnapshot', payload),
    );
    connection.on('CombatStarted', (payload: FightState) =>
      gameEventBus.emit('CombatStarted', payload),
    );
    connection.on('CombatUpdated', (payload: CombatUpdatePayload) =>
      gameEventBus.emit('CombatUpdated', payload),
    );
    connection.on('CombatEnded', (outcome: CombatOutcome) =>
      gameEventBus.emit('CombatEnded', outcome),
    );

    connection
      .start()
      .then(() => {
        setIsConnected(true);
        setError(false);
      })
      .catch((e) => {
        console.error('Error connecting to game hub', e);
        setError(true);
      });

    return () => {
      isIntentionalStop.current = true;
      connection.stop();
      setIsConnected(false);
      hubConnection.current = null;
    };
  }, [sessionId]);

  const streamTokens = useCallback(
    (
      methodName: string,
      onReceiveToken: (token: string) => void,
      onComplete: (() => void) | undefined,
      ...args: any[]
    ) => {
      const connection = hubConnection.current;

      if (!connection || !isConnected) {
        throw new Error(`Unable to stream ${methodName} while disconnected from the hub`);
      }

      let subscription: ISubscription<string> | null = null;

      const subscriber: IStreamSubscriber<string> = {
        complete() {
          subscription?.dispose();
          onComplete?.();
        },
        error(err: any) {
          console.error(`Error receiving response from ${methodName}`, err);
          setError(true);
          subscription?.dispose();
          onComplete?.();
        },
        next: onReceiveToken,
      };

      subscription = connection.stream(methodName, ...args).subscribe(subscriber);
    },
    [isConnected],
  );

  const streamOpening = useCallback(
    (onReceiveToken: (token: string) => void, onComplete?: () => void) =>
      streamTokens('ReceiveOpening', onReceiveToken, onComplete),
    [streamTokens],
  );

  const streamChat = useCallback(
    (message: string, onReceiveToken: (token: string) => void, onComplete?: () => void) =>
      streamTokens('SendChat', onReceiveToken, onComplete, message),
    [streamTokens],
  );

  const streamWait = useCallback(
    (hours: number, onReceiveToken: (token: string) => void, onComplete?: () => void) =>
      streamTokens('SendWait', onReceiveToken, onComplete, hours),
    [streamTokens],
  );

  const streamCombatAction = useCallback(
    (
      action: PlayerCombatAction,
      onReceiveToken: (token: string) => void,
      onComplete?: () => void,
    ) => streamTokens('SendCombatAction', onReceiveToken, onComplete, action),
    [streamTokens],
  );

  const streamFlee = useCallback(
    (onReceiveToken: (token: string) => void, onComplete?: () => void) =>
      streamTokens('SendFlee', onReceiveToken, onComplete),
    [streamTokens],
  );

  const endSession = useCallback(async () => {
    const connection = hubConnection.current;
    if (!connection || !isConnected) {
      return;
    }

    try {
      await connection.invoke('EndSession');
    } catch (e) {
      console.error('Error ending session', e);
    }
  }, [isConnected]);

  return {
    isConnected,
    error,
    streamOpening,
    streamChat,
    streamWait,
    streamCombatAction,
    streamFlee,
    endSession,
  };
}
