import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { renderWithProviders } from '@/test/test-utils';

import { DistrictPreview } from './district-preview';

describe('district preview controls', () => {
  it('changes the roster and clears the selected interior when switching district', async () => {
    const { user } = renderWithProviders(<DistrictPreview />);
    await user.click(screen.getByRole('button', { name: 'Inspect Inn 2' }));
    expect(screen.getByRole('region', { name: 'Selected building' })).toBeInTheDocument();

    await user.selectOptions(screen.getByRole('combobox', { name: 'District type' }), 'Encampment');

    expect(screen.queryByRole('region', { name: 'Selected building' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Inspect Barracks 1' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Inspect Stable 3' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Inspect Inn/ })).not.toBeInTheDocument();
  });

  it('shows the inn guest rooms on their own floor', async () => {
    const { user } = renderWithProviders(<DistrictPreview />);
    await user.click(screen.getByRole('button', { name: 'Inspect Inn 2' }));

    await user.selectOptions(screen.getByRole('combobox', { name: 'Floor' }), '1');

    const plan = screen.getByLabelText('Floor 1 plan');
    expect(within(plan).getByText('North Guest Room')).toBeInTheDocument();
    expect(within(plan).getByText('West Guest Room')).toBeInTheDocument();
    expect(within(plan).queryByText('Lobby')).not.toBeInTheDocument();
  });
});
