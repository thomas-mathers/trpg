import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
  type IRetryPolicy,
  type IStreamSubscriber,
  type ISubscription,
} from '@microsoft/signalr';
import { useRef, useEffect, useState, useCallback } from 'react';

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

export function useGameHubConnection() {
  const hubConnection = useRef<HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [error, setError] = useState<boolean>(false);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBaseUrl}/hubs/chat`)
      .withAutomaticReconnect(reconnectPolicy)
      .configureLogging(LogLevel.Information)
      .build();

    hubConnection.current = connection;

    connection.onreconnecting(() => setIsConnected(false));
    connection.onreconnected(() => setIsConnected(true));
    connection.onclose((e) => {
      setIsConnected(false);
      if (e) {
        console.error('SignalR connection lost');
        setError(true);
      }
    });

    connection
      .start()
      .then(() => setIsConnected(true))
      .catch((e) => {
        console.error('Error connecting to game hub', e);
        setError(true);
      });

    return () => {
      connection.stop();
      setIsConnected(false);
      hubConnection.current = null;
    };
  }, []);

  const streamTokens = useCallback(
    (methodName: string, onReceiveToken: (token: string) => void, ...args: any[]) => {
      const connection = hubConnection.current;

      if (!connection || !isConnected) {
        throw new Error(`Unable to stream ${methodName} while disconnected from the hub`);
      }

      let subscription: ISubscription<string> | null = null;

      const subscriber: IStreamSubscriber<string> = {
        complete() {
          subscription?.dispose();
        },
        error(error: any) {
          console.error(`Error receiving response from ${methodName}`, error);
          setError(true);
          subscription?.dispose();
        },
        next: onReceiveToken,
      };

      subscription = connection.stream(methodName, ...args).subscribe(subscriber);
    },
    [isConnected],
  );

  const streamOpening = useCallback(
    (onReceiveToken: (token: string) => void) => streamTokens('ReceiveOpening', onReceiveToken),
    [streamTokens],
  );

  const streamChat = useCallback(
    (message: string, onReceiveToken: (token: string) => void) =>
      streamTokens('SendChat', onReceiveToken, message),
    [streamTokens],
  );

  const streamWait = useCallback(
    (hours: number, onReceiveToken: (token: string) => void) =>
      streamTokens('SendWait', onReceiveToken, hours),
    [streamTokens],
  );

  return { isConnected, error, streamOpening, streamChat, streamWait };
}
