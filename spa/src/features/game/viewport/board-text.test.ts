import { describe, expect, it } from 'vitest';

import { layoutBoardText } from './board-text';

const measure = (line: string, fontSize: number) => line.length * fontSize * 0.5;

describe('layoutBoardText', () => {
  it('keeps a short name on one line', () => {
    const layout = layoutBoardText('Inn', 256, 128, measure);

    expect(layout.lines).toEqual(['Inn']);
  });

  it('wraps a long label onto two lines instead of squeezing it', () => {
    const layout = layoutBoardText('To The City Gates', 256, 128, measure);

    expect(layout.lines).toEqual(['To The', 'City Gates']);
  });

  it('never lets a line overflow the board width', () => {
    const layout = layoutBoardText('To The Merchants Quarter Gate', 256, 128, measure);

    const widest = Math.max(...layout.lines.map((line) => measure(line, layout.fontSize)));
    expect(widest).toBeLessThanOrEqual(256);
  });

  it('never lets the lines overflow the board height', () => {
    const layout = layoutBoardText('To The Merchants Quarter Gate', 256, 128, measure);

    expect(layout.fontSize * layout.lines.length).toBeLessThanOrEqual(128);
  });
});
