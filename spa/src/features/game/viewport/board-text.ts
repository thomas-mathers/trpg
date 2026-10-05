export interface BoardTextLayout {
  lines: string[];
  fontSize: number;
}

export type MeasureLine = (line: string, fontSize: number) => number;

export const LINE_HEIGHT = 1.15;
const MAX_LINES = 3;
const BOARD_FILL = 0.8;

export function layoutBoardText(
  text: string,
  maxWidth: number,
  maxHeight: number,
  measure: MeasureLine,
): BoardTextLayout {
  const words = text.split(/\s+/).filter(Boolean);
  let best: BoardTextLayout = { lines: [text], fontSize: 0 };

  for (let count = 1; count <= Math.min(MAX_LINES, words.length); count++) {
    const lines = balancedLines(words, count);
    const heightFit = (maxHeight * BOARD_FILL) / (count * LINE_HEIGHT);
    const widest = Math.max(...lines.map((line) => measure(line, heightFit)));
    const fontSize = widest > maxWidth ? (heightFit * maxWidth) / widest : heightFit;
    if (fontSize > best.fontSize) {
      best = { lines, fontSize };
    }
  }

  return best;
}

function balancedLines(words: string[], count: number): string[] {
  const target = words.join(' ').length / count;
  const lines: string[] = [];
  let current = '';

  for (const word of words) {
    const candidate = current ? `${current} ${word}` : word;
    if (current && candidate.length > target && lines.length < count - 1) {
      lines.push(current);
      current = word;
    } else {
      current = candidate;
    }
  }
  lines.push(current);
  return lines;
}
