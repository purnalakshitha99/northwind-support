import { useState } from 'react';
import { toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { ErrorMessage } from '../../../components/ErrorMessage';
import { Spinner } from '../../../components/Spinner';
import type { Agent, TicketDetail } from '../../../types/api';

interface EscalateTicketPanelProps {
  ticket: TicketDetail;
  agents: Agent[];
  onEscalated: (updatedTicket: TicketDetail) => void;
}

export function EscalateTicketPanel({
  ticket,
  agents,
  onEscalated,
}: EscalateTicketPanelProps) {
  const [reason, setReason] = useState('');
  const [escalatedBy, setEscalatedBy] = useState(
    agents.find((a) => a.isActive)?.fullName ?? 'Alex Turner'
  );
  const [error, setError] = useState<string>();
  const [validationError, setValidationError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);

  const isCritical = ticket.priority === 'Critical';
  const isResolvedOrClosed =
    ticket.status === 'Resolved' || ticket.status === 'Closed';
  const canEscalate = !isCritical && !isResolvedOrClosed;

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(undefined);
    setValidationError(undefined);

    const trimmedReason = reason.trim();
    if (trimmedReason.length < 5 || trimmedReason.length > 500) {
      setValidationError('Reason must contain between 5 and 500 characters.');
      return;
    }

    const trimmedEscalatedBy = escalatedBy.trim();
    if (!trimmedEscalatedBy || trimmedEscalatedBy.length > 100) {
      setValidationError('EscalatedBy is required and must not exceed 100 characters.');
      return;
    }

    setIsSubmitting(true);

    try {
      const response = await ticketsApi.escalateTicket(ticket.id, {
        reason: trimmedReason,
        escalatedBy: trimmedEscalatedBy,
      });

      setReason('');
      onEscalated(response.ticket);
    } catch (err) {
      setError(toErrorMessage(err, 'Failed to escalate ticket.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="card" data-testid="escalate-ticket-panel">
      <h2>Escalate ticket</h2>

      {!canEscalate && (
        <div className="notice" role="note">
          {isCritical ? (
            <p>Ticket is already Critical and cannot be escalated further.</p>
          ) : (
            <p>Resolved or closed tickets cannot be escalated.</p>
          )}
        </div>
      )}

      {error && <ErrorMessage message={error} />}

      <form onSubmit={handleSubmit} noValidate>
        <div className="field">
          <label className="field__label" htmlFor="escalation-actor">
            Escalated by
          </label>
          <input
            id="escalation-actor"
            type="text"
            value={escalatedBy}
            onChange={(e) => setEscalatedBy(e.target.value)}
            disabled={!canEscalate || isSubmitting}
            placeholder="Agent name"
            required
          />
        </div>

        <div className="field">
          <label className="field__label" htmlFor="escalation-reason">
            Reason for escalation (5–500 characters)
          </label>
          <textarea
            id="escalation-reason"
            rows={3}
            value={reason}
            onChange={(e) => {
              setReason(e.target.value);
              if (validationError) setValidationError(undefined);
            }}
            disabled={!canEscalate || isSubmitting}
            placeholder="Explain why this ticket priority needs to be raised..."
            aria-invalid={validationError ? 'true' : 'false'}
            aria-describedby={validationError ? 'escalation-reason-error' : undefined}
          />
          {validationError && (
            <span
              id="escalation-reason-error"
              className="field__error"
              role="alert"
              style={{ color: 'var(--color-danger, #dc2626)', fontSize: '0.875rem' }}
            >
              {validationError}
            </span>
          )}
        </div>

        <div className="button-row" style={{ marginTop: '1rem' }}>
          <button
            type="submit"
            className="button button--primary"
            disabled={!canEscalate || isSubmitting}
          >
            {isSubmitting ? <Spinner label="Escalating..." /> : 'Escalate ticket'}
          </button>
        </div>
      </form>
    </div>
  );
}
