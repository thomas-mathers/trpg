import { describe, expect, it } from 'vitest';

import {
  durationUntilNextTime,
  formatGameClockTime,
  formatGameDate,
  formatRemainingGameTime,
  gameDateTimeAt,
  gameTimeMillisecondsAt,
  type GameClockAnchor,
  type GameDateTime,
} from './game-clock';

const HOUR = 60 * 60 * 1000;
const DAY = 24 * HOUR;
const ANCHORED_AT = 1_700_000_000_000;

function anchorAt(gameTimeMilliseconds: number): GameClockAnchor {
  return {
    gameTimeMilliseconds,
    anchoredAtUnixMilliseconds: ANCHORED_AT,
    timeScale: 1,
  };
}

function timeOfDay(hour: number, minute = 0, second = 0): GameDateTime {
  return {
    ...gameDateTimeAt(anchorAt(0), ANCHORED_AT),
    hour,
    minute,
    second,
  };
}

describe('gameTimeMillisecondsAt', () => {
  it('scales real elapsed time by the anchor time scale', () => {
    const gameTime = gameTimeMillisecondsAt(
      { ...anchorAt(5_000), timeScale: 6 },
      ANCHORED_AT + 10_000,
    );

    expect(gameTime).toBe(65_000);
  });

  it('holds at the anchor when the client clock is behind the server clock', () => {
    const gameTime = gameTimeMillisecondsAt(anchorAt(5_000), ANCHORED_AT - 10_000);

    expect(gameTime).toBe(5_000);
  });
});

describe('formatRemainingGameTime', () => {
  it.each([
    [1, '1s'],
    [12_000, '12s'],
    [12_001, '13s'],
    [59_000, '59s'],
    [60_000, '1m'],
    [60_001, '2m'],
    [300_000, '5m'],
  ])('formats %i ms as %s', (remainingMilliseconds, expected) => {
    expect(formatRemainingGameTime(remainingMilliseconds)).toBe(expected);
  });
});

describe('gameDateTimeAt', () => {
  it('starts at the fictional epoch when no game time has elapsed', () => {
    const gameTime = gameDateTimeAt(anchorAt(0), ANCHORED_AT);

    expect(gameTime).toEqual({
      year: 975,
      monthName: 'Frostwane',
      day: 1,
      weekdayName: 'Emberday',
      hour: 8,
      minute: 0,
      second: 0,
    });
  });

  it('advances by the anchor time scale per real second', () => {
    const gameTime = gameDateTimeAt({ ...anchorAt(0), timeScale: 6 }, ANCHORED_AT + 10 * 60 * 1000);

    expect(gameTime).toMatchObject({ hour: 9, minute: 0, second: 0 });
  });

  it('advances one game second per real second after the anchor', () => {
    const gameTime = gameDateTimeAt(anchorAt(0), ANCHORED_AT + 90_500);

    expect(formatGameClockTime(gameTime)).toBe('08:01');
  });

  it('adds the anchored game time to the epoch', () => {
    const gameTime = gameDateTimeAt(anchorAt(6 * HOUR + 30 * 60 * 1000), ANCHORED_AT);

    expect(formatGameClockTime(gameTime)).toBe('14:30');
  });

  it('rolls over to the next day and weekday at midnight', () => {
    const beforeMidnight = gameDateTimeAt(anchorAt(16 * HOUR - 1000), ANCHORED_AT);
    const afterMidnight = gameDateTimeAt(anchorAt(16 * HOUR - 1000), ANCHORED_AT + 1000);

    expect(formatGameClockTime(beforeMidnight)).toBe('23:59');
    expect(formatGameDate(beforeMidnight)).toBe('Emberday, Frostwane 1');
    expect(formatGameClockTime(afterMidnight)).toBe('00:00');
    expect(formatGameDate(afterMidnight)).toBe('Ashday, Frostwane 2');
  });

  it('rolls over into the next month and year', () => {
    const newMonth = gameDateTimeAt(anchorAt(31 * DAY), ANCHORED_AT);
    const newYear = gameDateTimeAt(anchorAt(365 * DAY), ANCHORED_AT);

    expect(formatGameDate(newMonth)).toBe('Ravenday, Coldmere 1');
    expect(newYear.year).toBe(976);
    expect(newYear.monthName).toBe('Frostwane');
    expect(newYear.day).toBe(1);
  });

  it('holds at the anchor when the client clock is behind the server clock', () => {
    const gameTime = gameDateTimeAt(anchorAt(0), ANCHORED_AT - 5000);

    expect(formatGameClockTime(gameTime)).toBe('08:00');
  });
});

describe('formatGameClockTime', () => {
  it('pads the hour and minute to two digits', () => {
    expect(formatGameClockTime(timeOfDay(3, 4, 5))).toBe('03:04');
  });
});

describe('durationUntilNextTime', () => {
  it('measures a later time the same day', () => {
    expect(durationUntilNextTime(timeOfDay(8), 14, 30)).toEqual({
      hours: 6,
      minutes: 30,
    });
  });

  it('measures from the current minute rather than the current hour', () => {
    expect(durationUntilNextTime(timeOfDay(8, 45), 14, 30)).toEqual({
      hours: 5,
      minutes: 45,
    });
  });

  it('rounds a partial minute up so the wait never ends early', () => {
    expect(durationUntilNextTime(timeOfDay(8, 0, 30), 8, 10)).toEqual({
      hours: 0,
      minutes: 10,
    });
  });

  it('wraps to the next day when the target is earlier', () => {
    expect(durationUntilNextTime(timeOfDay(14), 8, 0)).toEqual({
      hours: 18,
      minutes: 0,
    });
  });

  it('waits a full day when the target is the current minute', () => {
    expect(durationUntilNextTime(timeOfDay(8), 8, 0)).toEqual({
      hours: 24,
      minutes: 0,
    });
  });

  it('waits a full day when the target passed seconds ago', () => {
    expect(durationUntilNextTime(timeOfDay(8, 0, 30), 8, 0)).toEqual({
      hours: 24,
      minutes: 0,
    });
  });
});
