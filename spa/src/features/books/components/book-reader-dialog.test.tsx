import { screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import { handleReadBookPage } from '@/api/client/msw.gen';
import { server } from '@/test/server';
import { renderWithProviders } from '@/test/test-utils';

import { BookReaderDialog } from './book-reader-dialog';

function renderReader() {
  return renderWithProviders(
    <BookReaderDialog
      playerId="player-id"
      itemId="book-item-id"
      title="A History of Ravenhollow"
      open
      onClose={vi.fn()}
    />,
  );
}

describe('BookReaderDialog', () => {
  it('reads the first page when opened', async () => {
    server.use(
      handleReadBookPage(() =>
        HttpResponse.json({
          title: 'A History of Ravenhollow',
          pageNumber: 1,
          pageCount: 3,
          text: 'The city was founded on a bend of the black river.',
          revealedSecret: false,
        }),
      ),
    );

    renderReader();

    expect(
      await screen.findByText('The city was founded on a bend of the black river.'),
    ).toBeInTheDocument();
    expect(screen.getByText('Page 1 of 3')).toBeInTheDocument();
  });

  it('requests the next page when the reader turns forward', async () => {
    const requestedPages: string[] = [];
    server.use(
      handleReadBookPage(({ params }) => {
        const pageNumber = String(params.pageNumber);
        requestedPages.push(pageNumber);
        return HttpResponse.json({
          title: 'A History of Ravenhollow',
          pageNumber: Number(pageNumber),
          pageCount: 3,
          text: `Page ${pageNumber} text.`,
          revealedSecret: false,
        });
      }),
    );

    const { user } = renderReader();
    await screen.findByText('Page 1 text.');

    await user.click(screen.getByRole('button', { name: 'Next page' }));

    expect(await screen.findByText('Page 2 text.')).toBeInTheDocument();
    await waitFor(() => expect(requestedPages).toEqual(['1', '2']));
  });

  it('stops the reader at the last page', async () => {
    server.use(
      handleReadBookPage(() =>
        HttpResponse.json({
          title: 'A History of Ravenhollow',
          pageNumber: 1,
          pageCount: 1,
          text: 'A single leaf, and nothing more.',
          revealedSecret: false,
        }),
      ),
    );

    renderReader();
    await screen.findByText('A single leaf, and nothing more.');

    expect(screen.getByRole('button', { name: 'Next page' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Previous page' })).toBeDisabled();
  });

  it('tells the reader when a page cannot be read', async () => {
    server.use(handleReadBookPage(() => new HttpResponse(null, { status: 500 })));

    renderReader();

    expect(await screen.findByText('This page could not be read.')).toBeInTheDocument();
  });
});
