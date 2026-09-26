// Mirrors TRPG.Domain.GameClock: the fictional calendar is the proleptic Gregorian calendar with
// these names, and game time 0 is 0975-01-01 08:00.
const EPOCH_MILLISECONDS = Date.UTC(975, 0, 1, 8);

const MONTH_NAMES = [
  'Frostwane',
  'Coldmere',
  'Thawmoon',
  'Greentide',
  'Bloomrise',
  'Suncrest',
  'Highsun',
  'Emberfall',
  'Harvestide',
  'Russetmoon',
  'Graytide',
  'Hearthwane',
];

const WEEKDAY_NAMES = [
  'Emberday',
  'Ashday',
  'Ironday',
  'Ravenday',
  'Stormday',
  'Hollowday',
  'Duskday',
];

const SECONDS_PER_DAY = 24 * 60 * 60;

export interface GameClockAnchor {
  gameTimeMilliseconds: number;
  anchoredAtUnixMilliseconds: number;
}

export interface GameDateTime {
  year: number;
  monthName: string;
  day: number;
  weekdayName: string;
  hour: number;
  minute: number;
  second: number;
}

export interface WaitDuration {
  hours: number;
  minutes: number;
}

export function gameDateTimeAt(
  { gameTimeMilliseconds, anchoredAtUnixMilliseconds }: GameClockAnchor,
  nowUnixMilliseconds: number,
): GameDateTime {
  // A client clock slightly behind the server's must not run the game clock backwards.
  const elapsedMilliseconds = Math.max(0, nowUnixMilliseconds - anchoredAtUnixMilliseconds);
  const instant = new Date(EPOCH_MILLISECONDS + Number(gameTimeMilliseconds) + elapsedMilliseconds);

  return {
    year: instant.getUTCFullYear(),
    monthName: MONTH_NAMES[instant.getUTCMonth()],
    day: instant.getUTCDate(),
    weekdayName: WEEKDAY_NAMES[instant.getUTCDay()],
    hour: instant.getUTCHours(),
    minute: instant.getUTCMinutes(),
    second: instant.getUTCSeconds(),
  };
}

export function formatGameDate({ weekdayName, monthName, day }: GameDateTime): string {
  return `${weekdayName}, ${monthName} ${day}`;
}

export function formatGameClockTime({ hour, minute, second }: GameDateTime): string {
  return [hour, minute, second].map((part) => part.toString().padStart(2, '0')).join(':');
}

// Rounds up to whole minutes because the hub takes hours and minutes, so the wait never ends
// before the requested time; a target at or before now means the next day's occurrence.
export function durationUntilNextTime(
  now: GameDateTime,
  targetHour: number,
  targetMinute: number,
): WaitDuration {
  const nowSeconds = now.hour * 3600 + now.minute * 60 + now.second;
  let deltaSeconds = targetHour * 3600 + targetMinute * 60 - nowSeconds;
  if (deltaSeconds <= 0) {
    deltaSeconds += SECONDS_PER_DAY;
  }
  const totalMinutes = Math.ceil(deltaSeconds / 60);

  return { hours: Math.floor(totalMinutes / 60), minutes: totalMinutes % 60 };
}
