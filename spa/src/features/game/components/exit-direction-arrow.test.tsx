import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ExitDirectionArrow } from './exit-direction-arrow';

describe('ExitDirectionArrow', () => {
  it('points the way the passage runs', () => {
    render(<ExitDirectionArrow direction="East" />);

    const arrow = screen.getByRole('img', { name: 'Leads east' });
    expect(arrow).toHaveStyle({ transform: 'rotate(90deg)' });
  });

  it('turns the other way for the opposite bearing', () => {
    render(<ExitDirectionArrow direction="West" />);

    expect(screen.getByRole('img', { name: 'Leads west' })).toHaveStyle({
      transform: 'rotate(270deg)',
    });
  });

  it('falls back to a plain arrow where there is no position to measure from', () => {
    render(<ExitDirectionArrow direction={null} />);

    expect(screen.queryByRole('img')).not.toBeInTheDocument();
    expect(screen.getByText('→')).toBeInTheDocument();
  });
});
