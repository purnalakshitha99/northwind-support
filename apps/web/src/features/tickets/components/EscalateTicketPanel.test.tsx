import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { ticketDetail } from '../../../test/fixtures';
import { EscalateTicketPanel } from './EscalateTicketPanel';

describe('EscalateTicketPanel', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('blocks submission and does not call the API when reason is shorter than 5 characters', async () => {
    const user = userEvent.setup();
    const escalateSpy = vi.spyOn(ticketsApi, 'escalateTicket');
    const onEscalated = vi.fn();
    const ticket = ticketDetail({ priority: 'Medium' });

    render(
      <EscalateTicketPanel
        ticket={ticket}
        agents={[]}
        onEscalated={onEscalated}
      />,
    );

    const reasonInput = screen.getByLabelText(/Reason for escalation/i);
    await user.type(reasonInput, 'help'); // 4 characters

    const submitButton = screen.getByRole('button', { name: /Escalate ticket/i });
    await user.click(submitButton);

    expect(
      screen.getByText('Reason must contain between 5 and 500 characters.'),
    ).toBeInTheDocument();
    expect(escalateSpy).not.toHaveBeenCalled();
    expect(onEscalated).not.toHaveBeenCalled();
  });

  it('shows a 409 conflict error and leaves the ticket unchanged when the API rejects escalation', async () => {
    const user = userEvent.setup();
    const conflictMessage = 'Ticket TCK-0012 is already Critical and cannot be escalated.';
    const escalateSpy = vi
      .spyOn(ticketsApi, 'escalateTicket')
      .mockRejectedValue(
        new ApiError(409, conflictMessage, {
          status: 409,
          detail: conflictMessage,
        }),
      );

    const onEscalated = vi.fn();
    const ticket = ticketDetail({ priority: 'Medium' });

    render(
      <EscalateTicketPanel
        ticket={ticket}
        agents={[]}
        onEscalated={onEscalated}
      />,
    );

    const reasonInput = screen.getByLabelText(/Reason for escalation/i);
    await user.type(reasonInput, 'Customer escalation requested urgently.');

    const submitButton = screen.getByRole('button', { name: /Escalate ticket/i });
    await user.click(submitButton);

    expect(await screen.findByText(conflictMessage)).toBeInTheDocument();
    expect(escalateSpy).toHaveBeenCalledTimes(1);
    expect(onEscalated).not.toHaveBeenCalled();
  });

  it('disables the escalate control when the ticket is already Critical', () => {
    const ticket = ticketDetail({ priority: 'Critical' });

    render(
      <EscalateTicketPanel
        ticket={ticket}
        agents={[]}
        onEscalated={vi.fn()}
      />,
    );

    expect(
      screen.getByText(/Ticket is already Critical and cannot be escalated further/i),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Escalate ticket/i })).toBeDisabled();
    expect(screen.getByLabelText(/Reason for escalation/i)).toBeDisabled();
  });

  it('disables the escalate control when the ticket is Resolved or Closed', () => {
    const ticket = ticketDetail({ status: 'Resolved' });

    render(
      <EscalateTicketPanel
        ticket={ticket}
        agents={[]}
        onEscalated={vi.fn()}
      />,
    );

    expect(
      screen.getByText(/Resolved or closed tickets cannot be escalated/i),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Escalate ticket/i })).toBeDisabled();
    expect(screen.getByLabelText(/Reason for escalation/i)).toBeDisabled();
  });

  it('calls onEscalated and clears reason on successful escalation', async () => {
    const user = userEvent.setup();
    const ticket = ticketDetail({ priority: 'Medium' });
    const updatedTicket = { ...ticket, priority: 'High' as const };

    const escalateSpy = vi
      .spyOn(ticketsApi, 'escalateTicket')
      .mockResolvedValue({
        ticket: updatedTicket,
        escalation: {
          id: 1,
          ticketId: ticket.id,
          fromPriority: 'Medium',
          toPriority: 'High',
          fromAgentId: null,
          toAgentId: null,
          previousDueAtUtc: null,
          newDueAtUtc: null,
          reason: 'Valid escalation reason provided here.',
          escalatedBy: 'Alex Turner',
          escalatedAtUtc: new Date().toISOString(),
        },
      });

    const onEscalated = vi.fn();

    render(
      <EscalateTicketPanel
        ticket={ticket}
        agents={[]}
        onEscalated={onEscalated}
      />,
    );

    const reasonInput = screen.getByLabelText(/Reason for escalation/i);
    await user.type(reasonInput, 'Valid escalation reason provided here.');

    const submitButton = screen.getByRole('button', { name: /Escalate ticket/i });
    await user.click(submitButton);

    expect(escalateSpy).toHaveBeenCalledTimes(1);
    expect(onEscalated).toHaveBeenCalledWith(updatedTicket);
    expect(reasonInput).toHaveValue('');
  });
});
