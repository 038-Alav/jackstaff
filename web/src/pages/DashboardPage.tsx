import { useEffect, useState, type ReactNode } from 'react';
import { Activity, ArrowRight, ArrowUpRight, Building2, Headphones, RadioTower, Signal, Users } from 'lucide-react';
import { api } from '../api';
import type { DashboardSummary, DashboardTicket } from '../types';
import { StatusBadge } from '../components/StatusBadge';
import { PageHeader } from '../components/PageHeader';

const number = (value: number) => new Intl.NumberFormat('es-CL').format(value);
const ticketType: Record<string, string> = { group_change: 'Cambio de grupo', incident: 'Incidente', maintenance: 'Mantención', other: 'Otro' };

export function DashboardPage({ tenantName, onNavigate }: { tenantName: string; onNavigate: (page: string) => void }) {
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [tickets, setTickets] = useState<DashboardTicket[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;
    Promise.all([api<{ summary: DashboardSummary; recentTickets: DashboardTicket[] }>('/dashboard/summary')])
      .then(([result]) => { if (active) { setSummary(result.summary); setTickets(result.recentTickets); } })
      .catch((err: unknown) => { if (active) setError(err instanceof Error ? err.message : 'Error al cargar el centro de operaciones.'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  return <div className="page-content">
    <PageHeader eyebrow="COMMAND CENTER / OVERVIEW" title="Centro de operaciones" description={`Estado general de activos y solicitudes · ${tenantName}`} action={<span className="live-indicator"><i /> ENTORNO DEMO</span>} />
    {error && <div className="form-error page-error">{error}</div>}
    <div className="metric-grid">
      <MetricCard label="Radios inventariadas" value={summary ? number(summary.totalRadios) : '—'} detail={`${summary?.activeGroups ?? '—'} grupos operativos`} icon={<RadioTower size={19} />} accent="navy" loading={loading} />
      <MetricCard label="Radios en línea" value={summary ? number(summary.onlineRadios) : '—'} detail={summary ? `${Math.round((summary.onlineRadios / Math.max(summary.totalRadios, 1)) * 100)}% del inventario` : 'Conectividad no integrada'} icon={<Signal size={19} />} accent="green" loading={loading} />
      <MetricCard label="Solicitudes abiertas" value={summary ? number(summary.openTickets) : '—'} detail={`${summary?.criticalTickets ?? '—'} prioridad crítica`} icon={<Headphones size={19} />} accent="amber" loading={loading} />
      <MetricCard label="Instalaciones activas" value={summary ? number(summary.activeSites) : '—'} detail="Ubicaciones registradas" icon={<Building2 size={19} />} accent="blue" loading={loading} />
    </div>

    <div className="dashboard-grid">
      <section className="panel status-panel">
        <div className="panel-heading"><div><span className="eyebrow">ASSET STATUS</span><h2>Estado de radios</h2></div><button className="text-action" onClick={() => onNavigate('radios')}>Ver inventario <ArrowUpRight size={15} /></button></div>
        {summary && <div className="status-overview">
          <div className="status-total"><strong>{number(summary.totalRadios)}</strong><span>activos registrados</span></div>
          <div className="status-bar"><span className="bar-online" style={{ width: `${summary.totalRadios ? summary.onlineRadios / summary.totalRadios * 100 : 0}%` }} /><span className="bar-offline" style={{ width: `${summary.totalRadios ? summary.offlineRadios / summary.totalRadios * 100 : 0}%` }} /><span className="bar-maintenance" style={{ width: `${summary.totalRadios ? summary.maintenanceRadios / summary.totalRadios * 100 : 0}%` }} /></div>
          <div className="status-legend"><span><i className="dot dot-green" />En línea <b>{summary.onlineRadios}</b></span><span><i className="dot dot-slate" />Sin conexión <b>{summary.offlineRadios}</b></span><span><i className="dot dot-amber" />Mantención <b>{summary.maintenanceRadios}</b></span></div>
        </div>}
        <div className="signal-note"><Activity size={16} /><span>La conectividad mostrada corresponde a datos de demostración. Los adaptadores de fabricantes y telemetría aún no están conectados.</span></div>
      </section>

      <section className="panel ticket-panel">
        <div className="panel-heading"><div><span className="eyebrow">SERVICE DESK</span><h2>Solicitudes recientes</h2></div><button className="text-action" onClick={() => onNavigate('tickets')}>Ver todas <ArrowUpRight size={15} /></button></div>
        <div className="recent-ticket-list">
          {tickets.map((ticket) => <div className="recent-ticket" key={ticket.id}>
            <div className="ticket-type-icon"><Headphones size={16} /></div>
            <div className="recent-ticket-main"><div className="recent-ticket-top"><b>{ticket.ticketNumber}</b><StatusBadge value={ticket.status} /></div><strong>{ticket.title}</strong><span>{ticketType[ticket.type] ?? ticket.type} · {ticket.radioAlias ?? 'Sin radio asociada'}</span></div>
            <span className={`priority-mark priority-${ticket.priority}`} title={`Prioridad ${ticket.priority}`} />
          </div>)}
          {!tickets.length && !loading && <div className="empty-state compact">No hay solicitudes para mostrar.</div>}
        </div>
      </section>
    </div>

    <section className="panel quick-access-panel">
      <div className="panel-heading"><div><span className="eyebrow">WORKSPACE</span><h2>Accesos operativos</h2></div></div>
      <div className="quick-access-grid">
        <QuickAccess icon={<RadioTower size={18} />} title="Inventario de radios" description="Activos, modelos y asignaciones" onClick={() => onNavigate('radios')} />
        <QuickAccess icon={<Building2 size={18} />} title="Instalaciones" description="Sitios y ubicaciones de la organización" onClick={() => onNavigate('sites')} />
        <QuickAccess icon={<Users size={18} />} title="Grupos operativos" description="Equipos y radios asignadas" onClick={() => onNavigate('groups')} />
        <QuickAccess icon={<Headphones size={18} />} title="Mesa de ayuda" description="Incidentes y solicitudes de cambio" onClick={() => onNavigate('tickets')} />
      </div>
    </section>
    <div className="demo-footer"><span>JACKSTAFF v0.1</span><span>FOXTROT · INTERNAL CODENAME</span><span>ONE PLATFORM. EVERY FLEET.</span></div>
  </div>;
}

function MetricCard({ label, value, detail, icon, accent, loading }: {
  label: string; value: string; detail: string; icon: ReactNode; accent: string; loading: boolean;
}) {
  return <section className={`metric-card metric-${accent}`}><div className="metric-top"><span>{label}</span><span className="metric-icon">{icon}</span></div><strong>{loading ? <i className="skeleton-line" /> : value}</strong><small>{detail}</small></section>;
}
function QuickAccess({ icon, title, description, onClick }: { icon: ReactNode; title: string; description: string; onClick: () => void }) {
  return <button className="quick-access" onClick={onClick}><span className="quick-icon">{icon}</span><span><b>{title}</b><small>{description}</small></span><ArrowRight size={16} /></button>;
}
