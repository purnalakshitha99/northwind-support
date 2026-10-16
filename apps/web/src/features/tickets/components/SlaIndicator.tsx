import { Badge, type BadgeTone } from '../../../components/Badge';
import type { SlaStatus } from '../../../types/api';

export interface SlaConfig {
  text: string;
  tone: BadgeTone;
  icon: string;
  ariaLabel: string;
}

const slaConfigurations: Record<SlaStatus, SlaConfig> = {
  WithinSla: {
    text: 'Within SLA',
    tone: 'info',
    icon: '●',
    ariaLabel: 'Status: Within SLA',
  },
  AtRisk: {
    text: 'At risk',
    tone: 'warning',
    icon: '⚠',
    ariaLabel: 'Status: At risk of breach',
  },
  Breached: {
    text: 'Breached',
    tone: 'danger',
    icon: '✕',
    ariaLabel: 'Status: SLA Breached',
  },
  Met: {
    text: 'Met',
    tone: 'success',
    icon: '✓',
    ariaLabel: 'Status: SLA Met',
  },
  NotApplicable: {
    text: 'No SLA',
    tone: 'neutral',
    icon: '—',
    ariaLabel: 'Status: No SLA applicable',
  },
};

interface SlaIndicatorProps {
  status: SlaStatus;
  showIcon?: boolean;
}

/**
 * Reusable SLA indicator used on both the ticket list and ticket detail pages.
 * Ensures state is distinguishable through shape/text, not color alone.
 */
export function SlaIndicator({ status, showIcon = true }: SlaIndicatorProps) {
  const config = slaConfigurations[status] ?? {
    text: status,
    tone: 'neutral' as BadgeTone,
    icon: '•',
    ariaLabel: `Status: ${status}`,
  };

  return (
    <span data-testid={`sla-indicator-${status}`} aria-label={config.ariaLabel}>
      <Badge tone={config.tone}>
        {showIcon && (
          <span className="sla-indicator__icon" aria-hidden="true" style={{ marginRight: '0.35rem' }}>
            {config.icon}
          </span>
        )}
        <span className="sla-indicator__text">{config.text}</span>
      </Badge>
    </span>
  );
}
