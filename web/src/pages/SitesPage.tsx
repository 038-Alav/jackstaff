import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Building2, MapPin, Plus, RadioTower } from 'lucide-react';
import { api, apiPost } from '../api';
import type { Site } from '../types';
import { PageHeader } from '../components/PageHeader';
import { Modal } from '../components/Modal';

export function SitesPage() {
  const [sites, setSites] = useState<Site[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [modal, setModal] = useState(false);
  const [name, setName] = useState(''); const [code, setCode] = useState('');
  const [siteType, setSiteType] = useState('branch'); const [address, setAddress] = useState('');
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try { setSites(await api<Site[]>('/sites')); setError(''); }
    catch (err) { setError(err instanceof Error ? err.message : 'No fue posible cargar instalaciones.'); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); }, [load]);

  async function submit(event: FormEvent) {
    event.preventDefault(); setSaving(true); setError('');
    try {
      await apiPost('/sites', { name, code, siteType, address });
      setModal(false); setName(''); setCode(''); setAddress(''); await load();
    } catch (err) { setError(err instanceof Error ? err.message : 'No fue posible crear la instalación.'); }
    finally { setSaving(false); }
  }

  return <div className="page-content">
    <PageHeader eyebrow="ASSET MANAGEMENT / SITES" title="Instalaciones" description="Organiza activos por instalaciones, sucursales, centros operativos o faenas." action={<button className="button button-primary" onClick={() => setModal(true)}><Plus size={16} /> Nueva instalación</button>} />
    {error && <div className="form-error page-error">{error}</div>}
    <div className="section-summary"><span>{sites.length} instalaciones registradas</span><span>Espacio de trabajo: organización activa</span></div>
    {loading ? <div className="loading-panel">Cargando instalaciones…</div> : sites.length ? <div className="site-grid">{sites.map((site) => <article className="site-card" key={site.id}>
      <div className="site-card-top"><span className="site-card-icon"><Building2 size={19} /></span><span className="site-code">{site.code}</span></div>
      <h3>{site.name}</h3><div className="site-kind">{siteTypeLabel(site.siteType)}</div>
      <div className="site-card-meta"><span><MapPin size={14} />{site.address || 'Dirección no registrada'}</span><span><RadioTower size={14} />{site.radioCount} radios</span></div>
      <div className="site-card-footer"><span className="active-site"><i />{site.isActive ? 'Activa' : 'Inactiva'}</span><span>JACKSTAFF ASSET</span></div>
    </article>)}</div> : <div className="empty-state"><Building2 size={28} /><b>Aún no hay instalaciones</b><span>Agrega la primera ubicación de esta organización.</span></div>}

    {modal && <Modal title="Nueva instalación" eyebrow="ASSET MANAGEMENT" onClose={() => setModal(false)}>
      <form className="form-stack" onSubmit={submit}>
        <label className="field-label">Nombre de la instalación<input value={name} onChange={(e) => setName(e.target.value)} required maxLength={160} placeholder="Ej. Centro de Operaciones" /></label>
        <div className="form-row"><label className="field-label">Código<input value={code} onChange={(e) => setCode(e.target.value)} required maxLength={40} placeholder="CO-01" /></label><label className="field-label">Tipo<select value={siteType} onChange={(e) => setSiteType(e.target.value)}><option value="branch">Sucursal</option><option value="station">Estación</option><option value="plant">Planta</option><option value="warehouse">Bodega</option><option value="control_center">Centro de control</option><option value="workshop">Taller</option><option value="yard">Patio</option><option value="other">Otro</option></select></label></div>
        <label className="field-label">Dirección o referencia<input value={address} onChange={(e) => setAddress(e.target.value)} maxLength={300} placeholder="Ciudad, región o dirección" /></label>
        <div className="modal-actions"><button type="button" className="button button-quiet" onClick={() => setModal(false)}>Cancelar</button><button className="button button-primary" disabled={saving}>{saving ? 'Guardando…' : 'Crear instalación'}</button></div>
      </form>
    </Modal>}
  </div>;
}
function siteTypeLabel(type: string) { const labels: Record<string, string> = { branch: 'Sucursal', station: 'Estación', plant: 'Planta', warehouse: 'Bodega', control_center: 'Centro de control', workshop: 'Taller', yard: 'Patio', other: 'Otro' }; return labels[type] ?? type; }
