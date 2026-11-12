import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { ArrowRight, Check, CircleAlert, Headphones, Plus, Search } from 'lucide-react';
import { api, apiPatch, apiPost } from '../api';
import type { OperationalGroup, Radio, Site, Ticket, TicketComment, TicketHistory } from '../types';
import { PageHeader } from '../components/PageHeader';
import { Modal } from '../components/Modal';
import { StatusBadge } from '../components/StatusBadge';

const typeLabels: Record<string, string> = { group_change: 'Cambio de grupo', incident: 'Incidente', maintenance: 'Mantención', other: 'Otro' };
const statusActions: Record<string, { status: string; label: string }[]> = {
  open: [{ status: 'in_review', label: 'Revisar' }, { status: 'rejected', label: 'Rechazar' }],
  in_review: [{ status: 'approved', label: 'Aprobar' }, { status: 'in_progress', label: 'Iniciar' }, { status: 'rejected', label: 'Rechazar' }],
  approved: [{ status: 'in_progress', label: 'Iniciar' }, { status: 'rejected', label: 'Rechazar' }],
  in_progress: [{ status: 'resolved', label: 'Resolver' }, { status: 'rejected', label: 'Rechazar' }],
};

export function TicketsPage({ canManage, supportMode = false }: { canManage: boolean; supportMode?: boolean }) {
  const [tickets, setTickets] = useState<Ticket[]>([]); const [radios, setRadios] = useState<Radio[]>([]);
  const [sites, setSites] = useState<Site[]>([]); const [groups, setGroups] = useState<OperationalGroup[]>([]);
  const [loading, setLoading] = useState(true); const [error, setError] = useState('');
  const [search, setSearch] = useState(''); const [filter, setFilter] = useState(supportMode ? 'all' : 'active');
  const [modal, setModal] = useState(false); const [title, setTitle] = useState('');
  const [description, setDescription] = useState(''); const [type, setType] = useState('incident');
  const [priority, setPriority] = useState('normal'); const [radioId, setRadioId] = useState('');
  const [siteId, setSiteId] = useState(''); const [requestedGroupId, setRequestedGroupId] = useState('');
  const [saving, setSaving] = useState(false); const [workingId, setWorkingId] = useState('');
  const [commentTicket, setCommentTicket] = useState<Ticket | null>(null); const [comments, setComments] = useState<TicketComment[]>([]); const [history, setHistory] = useState<TicketHistory[]>([]);
  const [commentDraft, setCommentDraft] = useState(''); const [commentsLoading, setCommentsLoading] = useState(false); const [commentSaving, setCommentSaving] = useState(false); const [commentError, setCommentError] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      if (supportMode) {
        const ticketRows = await api<Ticket[]>('/support/tickets');
        setTickets(ticketRows); setRadios([]); setSites([]); setGroups([]); setError('');
      } else {
        const [ticketRows, radioRows, siteRows, groupRows] = await Promise.all([
          api<Ticket[]>('/tickets'), api<Radio[]>('/radios'), api<Site[]>('/sites'), api<OperationalGroup[]>('/groups'),
        ]);
        setTickets(ticketRows); setRadios(radioRows); setSites(siteRows); setGroups(groupRows); setError('');
      }
    } catch (e) { setError(e instanceof Error ? e.message : 'No fue posible cargar tickets.'); }
    finally { setLoading(false); }
  }, [supportMode]);
  useEffect(() => { void load(); }, [load]);

  const filtered = useMemo(() => tickets.filter((ticket) => {
    const match = `${ticket.ticketNumber} ${ticket.title} ${ticket.tenantName} ${ticket.radioAlias ?? ''} ${ticket.siteName ?? ''}`.toLowerCase().includes(search.toLowerCase());
    const statusMatch = filter === 'all' || (filter === 'active' ? !['resolved', 'rejected'].includes(ticket.status) : ticket.status === filter);
    return match && statusMatch;
  }), [tickets, search, filter]);

  async function submit(event: FormEvent) {
    event.preventDefault(); setSaving(true); setError('');
    try {
      await apiPost('/tickets', { title, description, type, priority, radioId: radioId || null,
        siteId: siteId || null, requestedGroupId: type === 'group_change' ? requestedGroupId || null : null });
      setModal(false); setTitle(''); setDescription(''); setRadioId(''); setSiteId(''); setRequestedGroupId(''); await load();
    } catch (e) { setError(e instanceof Error ? e.message : 'No fue posible crear el ticket.'); }
    finally { setSaving(false); }
  }

  async function openComments(ticket: Ticket) {
    setCommentTicket(ticket); setCommentDraft(''); setCommentError(''); setCommentsLoading(true);
    try {
      const [commentRows, historyRows] = await Promise.all([
        api<TicketComment[]>(`${supportMode ? '/support/tickets' : '/tickets'}/${ticket.id}/comments`), api<TicketHistory[]>(`${supportMode ? '/support/tickets' : '/tickets'}/${ticket.id}/history`),
      ]);
      setComments(commentRows); setHistory(historyRows);
    }
    catch (e) { setError(e instanceof Error ? e.message : 'No fue posible cargar los comentarios.'); setCommentTicket(null); }
    finally { setCommentsLoading(false); }
  }

  async function addComment(event: FormEvent) {
    event.preventDefault();
    if (!commentTicket || !commentDraft.trim()) return;
    setCommentSaving(true); setCommentError('');
    try {
      await apiPost<TicketComment>(`${supportMode ? '/support/tickets' : '/tickets'}/${commentTicket.id}/comments`, { body: commentDraft });
      setComments(await api<TicketComment[]>(`${supportMode ? '/support/tickets' : '/tickets'}/${commentTicket.id}/comments`));
      setCommentDraft('');
    } catch (e) { setCommentError(e instanceof Error ? e.message : 'No fue posible publicar el comentario.'); }
    finally { setCommentSaving(false); }
  }

  async function changeStatus(ticket: Ticket, nextStatus: string) {
    const note = nextStatus === 'resolved' && ticket.type === 'group_change'
      ? `Cambio de grupo ejecutado: ${ticket.requestedGroupName}.` : '';
    setWorkingId(ticket.id); setError('');
    try { await apiPatch(`${supportMode ? '/support/tickets' : '/tickets'}/${ticket.id}/status`, { status: nextStatus, note }); await load(); }
    catch (e) { setError(e instanceof Error ? e.message : 'No fue posible actualizar el ticket.'); }
    finally { setWorkingId(''); }
  }

  const activeCount = tickets.filter((ticket) => !['resolved', 'rejected'].includes(ticket.status)).length;
  const criticalCount = tickets.filter((ticket) => ticket.priority === 'critical' && !['resolved', 'rejected'].includes(ticket.status)).length;

  return <div className="page-content">
    <PageHeader eyebrow={supportMode ? 'JACKSTAFF / PLATFORM SUPPORT' : 'SERVICE DESK / REQUEST MANAGEMENT'} title={supportMode ? 'Tickets de clientes' : 'Mesa de ayuda'} description={supportMode ? 'Bandeja transversal de solicitudes de las organizaciones atendidas.' : 'Incidentes, mantenciones y solicitudes de cambios operativos.'} action={supportMode ? undefined : <button className="button button-primary" onClick={() => setModal(true)}><Plus size={16} /> Nueva solicitud</button>} />
    {error && <div className="form-error page-error">{error}</div>}
    <div className="ticket-summary"><div><span>Solicitudes activas</span><b>{activeCount}</b></div><div><span>Prioridad crítica</span><b className="summary-red">{criticalCount}</b></div><div><span>Total de tickets</span><b>{tickets.length}</b></div><div className="ticket-workflow-hint"><CircleAlert size={16} /><span>Los cambios de grupo se aplican al inventario solo cuando el ticket queda resuelto.</span></div></div>
    <section className="panel table-panel">
      <div className="table-toolbar"><div className="table-search"><Search size={16} /><input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={supportMode ? "Buscar ticket, organización, radio o instalación…" : "Buscar ticket, radio o instalación…"} /></div><select className="filter-select" value={filter} onChange={(e) => setFilter(e.target.value)}><option value="active">Solicitudes activas</option><option value="all">Todos los tickets</option><option value="open">Abiertos</option><option value="in_review">En revisión</option><option value="approved">Aprobados</option><option value="in_progress">En ejecución</option><option value="resolved">Resueltos</option><option value="rejected">Rechazados</option></select></div>
      {loading ? <div className="loading-panel">Cargando tickets…</div> : <div className="ticket-list">{filtered.map((ticket) => {
        const actions = canManage ? (statusActions[ticket.status] ?? []) : [];
        return <article className="ticket-row" key={ticket.id}>
          <div className="ticket-row-icon"><Headphones size={17} /></div>
          <div className="ticket-row-main"><div className="ticket-row-meta"><b>{ticket.ticketNumber}</b>{supportMode && <span className="ticket-tenant-label">{ticket.tenantName}</span>}<span className={`priority-label priority-${ticket.priority}`}>{ticket.priority === 'critical' ? 'Crítica' : ticket.priority === 'high' ? 'Alta' : ticket.priority === 'low' ? 'Baja' : 'Normal'}</span><span>{typeLabels[ticket.type] ?? ticket.type}</span></div>
            <h3>{ticket.title}</h3><p>{ticket.description || 'Sin descripción adicional.'}</p>
            <div className="ticket-row-context"><span>{ticket.radioAlias ?? 'Sin radio asociada'}</span><i /> <span>{ticket.siteName ?? 'Sin instalación'}</span>{ticket.requestedGroupName && <><i /><span>Destino: {ticket.requestedGroupName}</span></> }</div>
          </div>
          <div className="ticket-row-side"><StatusBadge value={ticket.status} /><small>{new Date(ticket.createdAt).toLocaleDateString('es-CL')}</small>
            <div className="ticket-actions">
              <button className="mini-action" onClick={() => void openComments(ticket)}>Seguimiento</button>
              {actions.map((action) => <button key={action.status} className={`mini-action ${action.status === 'rejected' ? 'mini-danger' : action.status === 'resolved' ? 'mini-success' : ''}`} disabled={workingId === ticket.id} onClick={() => void changeStatus(ticket, action.status)}>{action.status === 'resolved' ? <Check size={13} /> : <ArrowRight size={13} />}{workingId === ticket.id ? '…' : action.label}</button>)}
            </div>
          </div>
        </article>;
      })}{!filtered.length && <div className="empty-state compact"><Headphones size={26} /><b>No hay tickets para mostrar</b><span>{supportMode ? "No hay solicitudes para los filtros seleccionados." : "Ajusta los filtros o crea una nueva solicitud."}</span></div>}</div>}
      <div className="table-footer"><span>Mostrando {filtered.length} de {tickets.length} tickets</span><span>Historial de estados y auditoría habilitados</span></div>
    </section>

    {modal && <Modal title="Nueva solicitud" eyebrow="SERVICE DESK" onClose={() => setModal(false)} wide>
      <form className="form-stack" onSubmit={submit}>
        <label className="field-label">Título<input value={title} onChange={(e) => setTitle(e.target.value)} required maxLength={180} placeholder="Describe brevemente la solicitud" /></label>
        <div className="form-row"><label className="field-label">Tipo<select value={type} onChange={(e) => setType(e.target.value)}><option value="incident">Incidente</option><option value="maintenance">Mantención</option><option value="group_change">Cambio de grupo</option><option value="other">Otro</option></select></label><label className="field-label">Prioridad<select value={priority} onChange={(e) => setPriority(e.target.value)}><option value="low">Baja</option><option value="normal">Normal</option><option value="high">Alta</option><option value="critical">Crítica</option></select></label></div>
        <label className="field-label">Descripción<textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={3} placeholder="Contexto, impacto o trabajo solicitado" /></label>
        <div className="form-row"><label className="field-label">Radio asociada<select value={radioId} onChange={(e) => setRadioId(e.target.value)}><option value="">Sin radio específica</option>{radios.map((radio) => <option key={radio.id} value={radio.id}>{radio.assetTag} · {radio.alias}</option>)}</select></label><label className="field-label">Instalación<select value={siteId} onChange={(e) => setSiteId(e.target.value)}><option value="">Sin instalación específica</option>{sites.map((site) => <option key={site.id} value={site.id}>{site.name}</option>)}</select></label></div>
        {type === 'group_change' && <label className="field-label">Grupo operativo solicitado<select value={requestedGroupId} onChange={(e) => setRequestedGroupId(e.target.value)} required><option value="">Selecciona el grupo de destino</option>{groups.map((group) => <option key={group.id} value={group.id}>{group.name}</option>)}</select><span className="field-help">La asignación actual no cambia hasta que soporte resuelva esta solicitud.</span></label>}
        <div className="modal-actions"><button type="button" className="button button-quiet" onClick={() => setModal(false)}>Cancelar</button><button className="button button-primary" disabled={saving}>{saving ? 'Creando…' : 'Crear solicitud'}</button></div>
      </form>
    </Modal>}
    {commentTicket && <Modal title={commentTicket.ticketNumber} eyebrow={supportMode ? `${commentTicket.tenantName.toUpperCase()} / TICKET FOLLOW-UP` : "TICKET FOLLOW-UP"} onClose={() => setCommentTicket(null)} wide>
      <div className="comment-thread">
        <div className="comment-ticket-context"><b>{commentTicket.title}</b><span>{typeLabels[commentTicket.type] ?? commentTicket.type} · {commentTicket.priority}</span></div>
        {commentsLoading ? <div className="loading-panel">Cargando seguimiento…</div> : <div className="follow-up-columns">
          <section><div className="follow-up-heading">Historial de estados</div>{history.length ? <div className="history-list">{history.map((entry) => <article className="history-item" key={entry.id}><span className="history-marker" /><div><div className="comment-item-head"><b>{entry.fromStatus ? `${entry.fromStatus} → ${entry.toStatus}` : `Creado · ${entry.toStatus}`}</b><time>{new Date(entry.createdAt).toLocaleString('es-CL')}</time></div><p>{entry.note || 'Sin detalle adicional'} · {entry.actorName}</p></div></article>)}</div> : <div className="field-help">Sin cambios de estado registrados.</div>}</section>
          <section><div className="follow-up-heading">Comentarios</div>{comments.length ? <div className="comment-list">{comments.map((comment) => <article className="comment-item" key={comment.id}><div className="comment-item-head"><b>{comment.authorName}</b><time>{new Date(comment.createdAt).toLocaleString('es-CL')}</time></div><p>{comment.body}</p></article>)}</div> : <div className="field-help">Todavía no hay comentarios.</div>}</section>
        </div>}
        {commentError && <div className="form-error">{commentError}</div>}
        <form className="comment-form" onSubmit={addComment}><label className="field-label">Agregar comentario<textarea value={commentDraft} onChange={(e) => setCommentDraft(e.target.value)} rows={3} maxLength={4000} required placeholder="Escribe una actualización para el seguimiento del ticket…" /></label><div className="modal-actions"><span className="field-help">Los comentarios se conservan en el historial.</span><button className="button button-primary" disabled={commentSaving || !commentDraft.trim()}>{commentSaving ? 'Publicando…' : 'Publicar comentario'}</button></div></form>
      </div>
    </Modal>}
  </div>;
}
