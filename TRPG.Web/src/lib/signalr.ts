import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL as string

export function createChatHubConnection() {
  return new HubConnectionBuilder()
    .withUrl(`${apiBaseUrl}/hubs/chat`)
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build()
}
