import { useGameClock } from '@/features/game/hooks/use-game-clock';

const NOON = 12;

export function useSceneHour(): number {
  const gameTime = useGameClock();
  return gameTime ? gameTime.hour + gameTime.minute / 60 + gameTime.second / 3600 : NOON;
}
