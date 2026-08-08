import type { FightState, SceneSnapshot } from '@/api/client';
import type { CombatOutcome } from '@/features/combat/combat-outcome';
import type { CombatUpdatePayload } from '@/features/combat/combat-round-event';

export type ConnectionStatus = 'connected' | 'reconnecting' | 'reconnected' | 'disconnected';

export interface SkillLevelUp {
  skill: string;
  level: number;
  characterExperienceCurrent: number;
  characterExperienceToNextLevel: number;
}

export interface CharacterLevelUp {
  level: number;
}

interface GameEventMap {
  SceneSnapshot: SceneSnapshot;
  CombatStarted: FightState;
  CombatUpdated: CombatUpdatePayload;
  CombatNarrations: string[];
  CombatEnded: CombatOutcome;
  CombatResolved: CombatOutcome;
  SkillLevelUp: SkillLevelUp;
  CharacterLevelUp: CharacterLevelUp;
  ConnectionStatusChanged: ConnectionStatus;
}

class GameEventBus extends EventTarget {
  emit<K extends keyof GameEventMap>(event: K, detail?: GameEventMap[K]): void {
    if (import.meta.env.DEV && import.meta.env.MODE !== 'test') {
      console.debug(`[gameEventBus] ${event}`, detail);
    }
    this.dispatchEvent(new CustomEvent(event, { detail }));
  }

  on<K extends keyof GameEventMap>(
    event: K,
    listener: (payload: GameEventMap[K]) => void,
  ): () => void {
    const handler = (e: Event) => listener((e as CustomEvent<GameEventMap[K]>).detail);
    this.addEventListener(event, handler);
    return () => this.removeEventListener(event, handler);
  }
}

export const gameEventBus = new GameEventBus();
