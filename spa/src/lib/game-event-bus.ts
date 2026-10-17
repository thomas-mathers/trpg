import type { CombatActionResult, CombatStarted } from '@/api/signalr-client/TRPG.Combat.Responses';
import type {
  CharacterLevelUp,
  CreatureVitalsUpdated,
  SkillLevelUp,
} from '@/api/signalr-client/TRPG.Creatures.Responses';
import type {
  GuardEncounterResolutionFact,
  GuardEncounterState,
  HostileEncounterResolutionFact,
  HostileEncounterState,
  ShakedownEncounterResolutionFact,
  ShakedownEncounterState,
  SuspicionEncounterResolutionFact,
  SuspicionEncounterState,
  TheftEncounterResolutionFact,
  TheftEncounterState,
  TrapEncounterResolutionFact,
  TrapEncounterState,
} from '@/api/signalr-client/TRPG.Encounters.Responses';
import type {
  CaravansArrivedPayload,
  CaravansLeftPayload,
  CaravansUpdatedPayload,
  ClockReanchoredPayload,
  PropOccupancyChangedPayload,
  SeatingChangedPayload,
  CreaturesArrivedPayload,
  CreaturesLeftPayload,
  CreaturesMovedPayload,
  CreaturesUpdatedPayload,
  CrimeNotification,
  PlayerCorrectedPayload,
  SceneSnapshot,
  WeatherChangedPayload,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type { QuestObjectiveCompleted } from '@/api/signalr-client/TRPG.Quests.Responses';
import type { TerminalCombatOutcome } from '@/features/combat/terminal-combat-outcome';

export type ConnectionStatus = 'connected' | 'reconnecting' | 'reconnected' | 'disconnected';

export type { CharacterLevelUp, QuestObjectiveCompleted, SkillLevelUp };

interface GameEventMap {
  SceneSnapshot: SceneSnapshot;
  CreaturesArrived: CreaturesArrivedPayload;
  CreaturesLeft: CreaturesLeftPayload;
  CreaturesMoved: CreaturesMovedPayload;
  CreaturesUpdated: CreaturesUpdatedPayload;
  CaravansArrived: CaravansArrivedPayload;
  CaravansLeft: CaravansLeftPayload;
  CaravansUpdated: CaravansUpdatedPayload;
  WeatherChanged: WeatherChangedPayload;
  ClockReanchored: ClockReanchoredPayload;
  SeatingChanged: SeatingChangedPayload;
  PropOccupancyChanged: PropOccupancyChangedPayload;
  CombatStarted: CombatStarted;
  CombatResolved: TerminalCombatOutcome;
  CombatActions: CombatActionResult[];
  HostileEncounterStarted: HostileEncounterState;
  HostileEncounterResolved: HostileEncounterResolutionFact;
  ShakedownEncounterStarted: ShakedownEncounterState;
  ShakedownEncounterResolved: ShakedownEncounterResolutionFact;
  GuardEncounterStarted: GuardEncounterState;
  GuardEncounterResolved: GuardEncounterResolutionFact;
  SuspicionEncounterStarted: SuspicionEncounterState;
  SuspicionEncounterResolved: SuspicionEncounterResolutionFact;
  TheftEncounterStarted: TheftEncounterState;
  TheftEncounterResolved: TheftEncounterResolutionFact;
  TrapEncounterStarted: TrapEncounterState;
  TrapEncounterResolved: TrapEncounterResolutionFact;
  CreatureVitalsUpdated: CreatureVitalsUpdated;
  PlayerCorrected: PlayerCorrectedPayload;
  SkillLevelUp: SkillLevelUp;
  CharacterLevelUp: CharacterLevelUp;
  QuestObjectiveCompleted: QuestObjectiveCompleted;
  QuestJournalUpdated: string | null;
  CrimeWitnessed: CrimeNotification;
  CrimeWitnessesRemoved: CrimeNotification;
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
