import { describe, expect, it } from 'vitest';

import {
  durationUntilNextTime,
  formatGameClockTime,
  formatGameDate,
  gameDateTimeAt,
  type GameClockAnchor,
  type GameDateTime,
} from './game-clock';

const HOUR = 60 * 60 * 1000;
const DAY = 24 * HOUR;
const ANCHORED_AT = 1_700_000_000_000;

function anchorAt(gameTimeMilliseconds: number): GameClockAnchor {
  return { gameTimeMilliseconds, anchoredAtUnixMilliseconds: ANCHORED_AT };
}

function timeOfDay(hour: number, minute = 0, second = 0): GameDateTime {
  return {
    ...gameDateTimeAt(anchorAt(0), ANCHORED_AT),
    hour,
    minute,
    second,
  };
}

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

  it('advances one game second per real second after the anchor', () => {
    const gameTime = gameDateTimeAt(anchorAt(0), ANCHORED_AT + 90_500);

    expect(formatGameClockTime(gameTime)).toBe('08:01:30');
  });

  it('adds the anchored game time to the epoch', () => {
    const gameTime = gameDateTimeAt(anchorAt(6 * HOUR + 30 * 60 * 1000), ANCHORED_AT);

    expect(formatGameClockTime(gameTime)).toBe('14:30:00');
  });

  it('rolls over to the next day and weekday at midnight', () => {
    const beforeMidnight = gameDateTimeAt(anchorAt(16 * HOUR - 1000), ANCHORED_AT);
    const afterMidnight = gameDateTimeAt(anchorAt(16 * HOUR - 1000), ANCHORED_AT + 1000);

    expect(formatGameClockTime(beforeMidnight)).toBe('23:59:59');
    expect(formatGameDate(beforeMidnight)).toBe('Emberday, Frostwane 1');
    expect(formatGameClockTime(afterMidnight)).toBe('00:00:00');
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

    expect(formatGameClockTime(gameTime)).toBe('08:00:00');
  });
});

describe('formatGameClockTime', () => {
  it('pads every part to two digits', () => {
    expect(formatGameClockTime(timeOfDay(3, 4, 5))).toBe('03:04:05');
  });
});

describe('durationUntilNextTime', () => {
  it('measures a later time the same day', () => {
    expect(durationUntilNextTime(timeOfDay(8), 14, 30)).toEqual({ hours: 6, minutes: 30 });
  });

  it('measures from the current minute rather than the current hour', () => {
    expect(durationUntilNextTime(timeOfDay(8, 45), 14, 30)).toEqual({ hours: 5, minutes: 45 });
  });

  it('rounds a partial minute up so the wait never ends early', () => {
    expect(durationUntilNextTime(timeOfDay(8, 0, 30), 8, 10)).toEqual({ hours: 0, minutes: 10 });
  });

  it('wraps to the next day when the target is earlier', () => {
    expect(durationUntilNextTime(timeOfDay(14), 8, 0)).toEqual({ hours: 18, minutes: 0 });
  });

  it('waits a full day when the target is the current minute', () => {
    expect(durationUntilNextTime(timeOfDay(8), 8, 0)).toEqual({ hours: 24, minutes: 0 });
  });

  it('waits a full day when the target passed seconds ago', () => {
    expect(durationUntilNextTime(timeOfDay(8, 0, 30), 8, 0)).toEqual({ hours: 24, minutes: 0 });
  });
});
