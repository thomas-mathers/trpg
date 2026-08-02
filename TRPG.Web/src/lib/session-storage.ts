import type { ChatMessage } from '@/GameScreen';

function storageKey(sessionId: string): string {
  return `trpg:session:${sessionId}:messages`;
}

export function loadStoredMessages(sessionId: string): ChatMessage[] | null {
  const raw = sessionStorage.getItem(storageKey(sessionId));
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as ChatMessage[];
  } catch {
    return null;
  }
}

export function saveMessages(sessionId: string, messages: ChatMessage[]): void {
  sessionStorage.setItem(storageKey(sessionId), JSON.stringify(messages));
}

export function clearStoredMessages(sessionId: string): void {
  sessionStorage.removeItem(storageKey(sessionId));
}
