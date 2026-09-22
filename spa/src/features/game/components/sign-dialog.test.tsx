import { screen } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import { handleGetSignText } from '@/api/client/msw.gen';
import { server } from '@/test/server';
import { renderWithProviders } from '@/test/test-utils';

import { SignDialog } from './sign-dialog';

function renderDialog() {
  return renderWithProviders(
    <SignDialog
      worldId="world-id"
      signId="sign-id"
      name="Caravan Schedule"
      open
      onClose={vi.fn()}
    />,
  );
}

describe('SignDialog', () => {
  it('shows the live text fetched for the sign', async () => {
    server.use(
      handleGetSignText(() =>
        HttpResponse.json({
          text: 'Caravan schedule:\nClockwise: next arrival Duskday, Frostwane 6 - 15:00',
        }),
      ),
    );

    renderDialog();

    expect(await screen.findByRole('heading', { name: 'Caravan Schedule' })).toBeVisible();
    expect(
      await screen.findByText(/Clockwise: next arrival Duskday, Frostwane 6 - 15:00/),
    ).toBeVisible();
  });
});
