import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ExitFamiliarity } from './exit-familiarity';

describe('ExitFamiliarity', () => {
  it('marks the way you came, which is the one you rarely mean to take', () => {
    render(<ExitFamiliarity isVisited isWayBack />);

    expect(screen.getByText('back')).toBeInTheDocument();
    expect(screen.queryByText('explored')).not.toBeInTheDocument();
  });

  it('marks somewhere already walked', () => {
    render(<ExitFamiliarity isVisited isWayBack={false} />);

    expect(screen.getByText('explored')).toBeInTheDocument();
  });

  it('says nothing about somewhere new, so the unexplored ones stand out', () => {
    const { container } = render(<ExitFamiliarity isVisited={false} isWayBack={false} />);

    expect(container).toBeEmptyDOMElement();
  });
});
