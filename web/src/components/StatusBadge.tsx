import { CheckCircle2, CircleAlert, Clock3, PauseCircle, XCircle } from 'lucide-react';

const labels: Record<string, string> = {
  online: 'En línea', offline: 'Sin conexión', maintenance: 'Mantención',
  open: 'Abierto', in_review: 'En revisión', approved: 'Aprobado',
  in_progress: 'En ejecución', resolved: 'Resuelto', rejected: 'Rechazado',
  low: 'Baja', normal: 'Normal', high: 'Alta', critical: 'Crítica',
};
const tones: Record<string, string> = {
  online: 'green', resolved: 'green', approved: 'green',
  offline: 'slate', open: 'blue', in_review: 'amber', in_progress: 'cyan',
  maintenance: 'amber', rejected: 'red', critical: 'red', high: 'amber',
  normal: 'slate', low: 'slate',
};

export function StatusBadge({ value }: { value: string }) {
  const Icon = value === 'online' || value === 'resolved' ? CheckCircle2
    : value === 'offline' ? XCircle
    : value === 'maintenance' || value === 'in_review' || value === 'high' ? Clock3
    : value === 'rejected' || value === 'critical' ? CircleAlert
    : value === 'in_progress' ? PauseCircle : CheckCircle2;
  return <span className={`status-badge tone-${tones[value] ?? 'slate'}`}><Icon size={13} />{labels[value] ?? value}</span>;
}
