import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Plus, Users, RadioTower } from 'lucide-react';
import { api, apiPost } from '../api';
import type { OperationalGroup } from '../types';
import { PageHeader } from '../components/PageHeader';
import { Modal } from '../components/Modal';

export function GroupsPage() {
  const [groups, setGroups] = useState<OperationalGroup[]>([]);
  const [loading, setLoading] = useState(true); const [error, setError] = useState('');
  const [modal, setModal] = useState(false); const [name, setName] = useState('');
  const [description, setDescription] = useState(''); const [saving, setSaving] = useState(false);
  const load = useCallback(async () => { setLoading(true); try { setGroups(await api<OperationalGroup[]>('/groups')); setError(''); } catch (e) { setError(e instanceof Error ? e.message : 'Error al cargar grupos.'); } finally { setLoading(false); } }, []);
  useEffect(() => { void load(); }, [load]);
  async function submit(event: FormEvent) {
    event.preventDefault(); setSaving(true); setError('');
    try { await apiPost('/groups', { name, description }); setModal(false); setName(''); setDescription(''); await load(); }
    catch (e) { setError(e instanceof Error ? e.message : 'No fue posible crear el grupo.'); }
    finally { setSaving(false); }
  }
  return <div className="page-content">
    <PageHeader eyebrow="OPERATIONS / GROUPS" title="Grupos operativos" description="Agrupa radios y equipos según la estructura de trabajo de cada organización." action={<button className="button button-primary" onClick={() => setModal(true)}><Plus size={16} /> Nuevo grupo</button>} />
    {error && <div className="form-error page-error">{error}</div>}
    <div className="section-summary"><span>{groups.length} grupos configurados</span><span>Los grupos operativos son independientes de los talkgroups del fabricante.</span></div>
    {loading ? <div className="loading-panel">Cargando grupos…</div> : groups.length ? <div className="group-grid">{groups.map((group, index) => <article className="group-card" key={group.id}>
      <div className="group-card-top"><span className="group-card-icon"><Users size={18} /></span><span className="group-index">{String(index + 1).padStart(2, '0')}</span></div>
      <h3>{group.name}</h3><p>{group.description || 'Sin descripción'}</p>
      <div className="group-card-bottom"><span><RadioTower size={14} />{group.radioCount} radios asignadas</span><span className="active-site"><i />{group.isActive ? 'Activo' : 'Inactivo'}</span></div>
    </article>)}</div> : <div className="empty-state"><Users size={28} /><b>Aún no hay grupos</b><span>Crea un grupo operativo para comenzar a organizar tus radios.</span></div>}
    {modal && <Modal title="Nuevo grupo operativo" eyebrow="OPERATIONS" onClose={() => setModal(false)}>
      <form className="form-stack" onSubmit={submit}>
        <label className="field-label">Nombre del grupo<input value={name} onChange={(e) => setName(e.target.value)} required maxLength={120} placeholder="Ej. Operaciones" /></label>
        <label className="field-label">Descripción<textarea value={description} onChange={(e) => setDescription(e.target.value)} maxLength={300} rows={3} placeholder="Responsabilidad o propósito del grupo" /></label>
        <div className="modal-actions"><button type="button" className="button button-quiet" onClick={() => setModal(false)}>Cancelar</button><button className="button button-primary" disabled={saving}>{saving ? 'Guardando…' : 'Crear grupo'}</button></div>
      </form>
    </Modal>}
  </div>;
}
