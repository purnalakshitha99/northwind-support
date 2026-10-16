import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Spinner } from '../../components/Spinner';
import { useReferenceData } from '../../hooks/useReferenceData';
import { formatDateTime } from '../../lib/format';
import type { TicketDetail } from '../../types/api';
import { CreateTicketForm } from './components/CreateTicketForm';
import { PriorityBadge } from './components/TicketBadges';

export function CreateTicketPage() {
  const reference = useReferenceData();
  const navigate = useNavigate();
  const [created, setCreated] = useState<TicketDetail>();

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="breadcrumb">
            <Link to="/tickets">Tickets</Link> / New
          </p>
          <h1>Raise a ticket</h1>
        </div>
      </header>

      {reference.isLoading && <Spinner label="Loading customers and categories" />}

      {reference.error && (
        <ErrorMessage message={reference.error} onRetry={() => void reference.reload()} />
      )}

      {created && (
        <div className="card notice" role="status" data-testid="triage-result">
          <h2 style={{ marginTop: 0 }}>
            Ticket Raised: <strong>{created.reference}</strong>
          </h2>
          <p style={{ margin: '0.25rem 0 1rem' }}>
            Customer: <strong>{created.customer.name}</strong> ({created.customer.tier} tier)
          </p>

          <dl className="summary" style={{ margin: '1rem 0' }}>
            <div>
              <dt>Applied Priority</dt>
              <dd>
                <PriorityBadge priority={created.priority} />
              </dd>
            </div>

            <div>
              <dt>SLA Due Date</dt>
              <dd>
                {created.dueAtUtc ? formatDateTime(created.dueAtUtc) : 'No SLA'}
              </dd>
            </div>

            <div>
              <dt>Assigned Agent</dt>
              <dd>
                {created.assignedAgent ? (
                  <strong>{created.assignedAgent.fullName}</strong>
                ) : (
                  <span style={{ color: 'var(--color-warning, #d97706)' }}>
                    Unassigned — {created.triageReason || 'No eligible active agent currently available'}
                  </span>
                )}
              </dd>
            </div>

            {created.triageReason && (
              <div>
                <dt>Triage Decision</dt>
                <dd style={{ fontStyle: 'italic' }}>{created.triageReason}</dd>
              </div>
            )}
          </dl>

          <div className="button-row">
            <button
              type="button"
              className="button button--primary"
              onClick={() => void navigate(`/tickets/${created.id}`)}
            >
              Open {created.reference}
            </button>
            <button
              type="button"
              className="button button--ghost"
              onClick={() => setCreated(undefined)}
            >
              Raise another
            </button>
          </div>
        </div>
      )}

      {reference.data && !created && (
        <CreateTicketForm
          customers={reference.data.customers}
          categories={reference.data.categories}
          onCreated={setCreated}
        />
      )}
    </section>
  );
}
