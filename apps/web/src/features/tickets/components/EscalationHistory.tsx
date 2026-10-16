import { useCallback } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { EmptyState } from '../../../components/EmptyState';
import { ErrorMessage } from '../../../components/ErrorMessage';
import { Spinner } from '../../../components/Spinner';
import { useAsyncData } from '../../../hooks/useAsyncData';
import { formatDateTime } from '../../../lib/format';
import type { Agent, TicketEscalation } from '../../../types/api';
import { PriorityBadge } from './TicketBadges';

interface EscalationHistoryProps {
  ticketId: number;
  agents: Agent[];
  reloadKey?: number;
}

export function EscalationHistory({
  ticketId,
  agents,
  reloadKey = 0,
}: EscalationHistoryProps) {
  const load = useCallback(
    () => ticketsApi.getEscalations(ticketId),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [ticketId, reloadKey]
  );

  const { data: escalations = [], isLoading, error, reload } = useAsyncData<TicketEscalation[]>(
    load,
    'Unable to load escalation history.'
  );

  const resolveAgentName = (agentId: number | null) => {
    if (agentId === null) return 'Unassigned';
    const found = agents.find((a) => a.id === agentId);
    return found ? found.fullName : `Agent #${agentId}`;
  };

  return (
    <div className="card" data-testid="escalation-history">
      <h2>Escalation history</h2>

      {isLoading && <Spinner label="Loading history..." />}
      {error && <ErrorMessage message={error} onRetry={() => void reload()} />}

      {!isLoading && !error && escalations.length === 0 && (
        <EmptyState title="No escalations recorded for this ticket." />
      )}

      {!isLoading && !error && escalations.length > 0 && (
        <div className="escalations-list" style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          {escalations.map((item) => (
            <div
              key={item.id}
              className="notice"
              style={{ padding: '0.75rem 1rem', borderLeft: '4px solid var(--color-primary, #2563eb)' }}
            >
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'baseline',
                  marginBottom: '0.5rem',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                  <PriorityBadge priority={item.fromPriority} />
                  <span>&rarr;</span>
                  <PriorityBadge priority={item.toPriority} />
                </div>
                <span className="summary__note" style={{ fontSize: '0.8rem' }}>
                  {formatDateTime(item.escalatedAtUtc)}
                </span>
              </div>

              <p style={{ margin: '0.25rem 0', fontWeight: 500 }}>
                &ldquo;{item.reason}&rdquo;
              </p>

              <div
                style={{
                  display: 'flex',
                  flexWrap: 'wrap',
                  gap: '1rem',
                  fontSize: '0.85rem',
                  color: 'var(--color-text-muted, #64748b)',
                  marginTop: '0.5rem',
                }}
              >
                <span>
                  By: <strong>{item.escalatedBy}</strong>
                </span>
                <span>
                  Assignee: {resolveAgentName(item.fromAgentId)} &rarr;{' '}
                  <strong>{resolveAgentName(item.toAgentId)}</strong>
                </span>
                {item.newDueAtUtc && (
                  <span>
                    New deadline: <strong>{formatDateTime(item.newDueAtUtc)}</strong>
                  </span>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
