import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaIndicator } from './SlaIndicator';

describe('SlaIndicator', () => {
  const allStates: { status: SlaStatus; expectedText: string; expectedIcon: string; expectedTone: string }[] = [
    { status: 'WithinSla', expectedText: 'Within SLA', expectedIcon: '●', expectedTone: 'info' },
    { status: 'AtRisk', expectedText: 'At risk', expectedIcon: '⚠', expectedTone: 'warning' },
    { status: 'Breached', expectedText: 'Breached', expectedIcon: '✕', expectedTone: 'danger' },
    { status: 'Met', expectedText: 'Met', expectedIcon: '✓', expectedTone: 'success' },
    { status: 'NotApplicable', expectedText: 'No SLA', expectedIcon: '—', expectedTone: 'neutral' },
  ];

  it.each(allStates)(
    'renders $status state distinguishably with text "$expectedText" and icon "$expectedIcon"',
    ({ status, expectedText, expectedIcon, expectedTone }) => {
      render(<SlaIndicator status={status} />);

      const indicator = screen.getByTestId(`sla-indicator-${status}`);
      expect(indicator).toBeInTheDocument();

      // Meaning is carried by visible text
      expect(screen.getByText(expectedText)).toBeInTheDocument();

      // State is not conveyed by color alone: distinct icon is present
      expect(screen.getByText(expectedIcon)).toBeInTheDocument();

      // Correct tone class is applied
      const badge = indicator.querySelector('.badge');
      expect(badge).toHaveClass(`badge--${expectedTone}`);
    },
  );
});
