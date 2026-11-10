import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { Battery, Plus, RadioTower, Search } from 'lucide-react';
import { api, apiPost } from '../api';
import type { OperationalGroup, Radio, RadioModel, Site } from '../types';
import { PageHeader } from '../components/PageHeader';
import { Modal } from '../components/Modal';
import { StatusBadge } from '../components/StatusBadge';

export function RadiosPage() {
  const [radios, setRadios] = useState<Radio[]>([]); const [sites, setSites] = useState<Site[]>([]);
  const [models, setModels] = useState<RadioModel[]>([]); const [groups, setGroups] = useState<OperationalGroup[]>([]);
  const [loading, setLoading] = useState(true); const [error, setError] = useState('');
  const [search, setSearch] = useState(''); const [status, setStatus] = useState('all');
  const [modal, setModal] = useState(false); const [assetTag, setAssetTag] = useState('');
  const [serialNumber, setSerialNumber] = useState(''); const [alias, setAlias] = useState('');
  const [siteId, setSiteId] = useState(''); const [modelId, setModelId] = useState('');
  const [groupId, setGroupId] = useState(''); const [initialStatus, setInitialStatus] = useState('offline');
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [radioRows, siteRows, modelRows, groupRows] = await Promise.all([
        api<Radio[]>('/radios'), api<Site[]>('/sites'), api<RadioModel[]>('/radio-models'), api<OperationalGroup[]>('/groups'),
      ]);
      setRadios(radioRows); setSites(siteRows); setModels(modelRows); setGroups(groupRows);
      setError('');
    } catch (e) { setError(e instanceof Error ? e.message : 'Error al cargar inventario.'); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); }, [load]);

  const filtered = useMemo(() => radios.filter((radio) => {
    const match = `${radio.assetTag} ${radio.serialNumber} ${radio.alias} ${radio.model} ${radio.siteName} ${radio.groupName ?? ''}`.toLowerCase().includes(search.toLowerCase());
    return match && (status === 'all' || radio.status === status);
  }), [radios, search, status]);

  async function submit(event: FormEvent) {
    event.preventDefault(); setSaving(true); setError('');
    try {
      await apiPost('/radios', { radioModelId: modelId, siteId, assetTag, serialNumber, alias, initialStatus, initialGroupId: groupId || null });
      setModal(false); setAssetTag(''); setSerialNumber(''); setAlias(''); setGroupId(''); await load();
    } catch (e) { setError(e instanceof Error ? e.message : 'No fue posible registrar la radio.'); }
    finally { setSaving(false); }
  }

  const online = radios.filter((radio) => radio.status === 'online').length;
  const offline = radios.filter((radio) => radio.status === 'offline').length;
  const maintenance = radios.filter((radio) => radio.status === 'maintenance').length;

  return <div className="page-content">
    <PageHeader eyebrow="ASSET MANAGEMENT / RADIO INVENTORY" title="Inventario de radios" description="Activos de comunicación registrados en la organización." action={<button className="button button-primary" onClick={() => setModal(true)}><Plus size={16} /> Registrar radio</button>} />
    {error && <div className="form-error page-error">{error}</div>}
    <div className="inventory-summary"><div><span>Total activos</span><b>{radios.length}</b></div><div><span>En línea</span><b className="summary-green">{online}</b></div><div><span>Sin conexión</span><b>{offline}</b></div><div><span>Mantención</span><b className="summary-amber">{maintenance}</b></div></div>
    <section className="panel table-panel">
      <div className="table-toolbar"><div className="table-search"><Search size={16} /><input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Buscar por etiqueta, serie, radio, sitio…" /></div><select className="filter-select" value={status} onChange={(e) => setStatus(e.target.value)}><option value="all">Todos los estados</option><option value="online">En línea</option><option value="offline">Sin conexión</option><option value="maintenance">Mantención</option></select></div>
      {loading ? <div className="loading-panel">Cargando inventario…</div> : <div className="table-scroll"><table><thead><tr><th>ACTIVO</th><th>MODELO / SERIE</th><th>INSTALACIÓN</th><th>GRUPO OPERATIVO</th><th>ESTADO</th><th>BATERÍA</th></tr></thead><tbody>
        {filtered.map((radio) => <tr key={radio.id}>
          <td><div className="asset-cell"><span className="asset-icon"><RadioTower size={16} /></span><span><b>{radio.alias}</b><small>{radio.assetTag}</small></span></div></td>
          <td><div className="cell-primary">{radio.manufacturer} {radio.model}</div><small>{radio.serialNumber}</small></td>
          <td><span className="cell-primary">{radio.siteName}</span></td>
          <td>{radio.groupName ? <span className="group-chip">{radio.groupName}</span> : <span className="muted">Sin asignar</span>}</td>
          <td><StatusBadge value={radio.status} /></td>
          <td>{radio.batteryPercent !== null ? <span className="battery-value"><Battery size={15} />{radio.batteryPercent}%</span> : <span className="muted">—</span>}</td>
        </tr>)}
        {!filtered.length && <tr><td colSpan={6}><div className="empty-table">No se encontraron radios con esos filtros.</div></td></tr>}
      </tbody></table></div>}
      <div className="table-footer"><span>Mostrando {filtered.length} de {radios.length} radios</span><span>Valores de estado demo · No hay conexión con equipos reales</span></div>
    </section>
    {modal && <Modal title="Registrar radio" eyebrow="ASSET MANAGEMENT" onClose={() => setModal(false)} wide>
      <form className="form-stack" onSubmit={submit}>
        <div className="form-row"><label className="field-label">Etiqueta de activo<input value={assetTag} onChange={(e) => setAssetTag(e.target.value)} required maxLength={80} placeholder="RAD-0422" /></label><label className="field-label">Número de serie<input value={serialNumber} onChange={(e) => setSerialNumber(e.target.value)} required maxLength={120} placeholder="MTR-7A-00422" /></label></div>
        <label className="field-label">Nombre o alias de la radio<input value={alias} onChange={(e) => setAlias(e.target.value)} required maxLength={160} placeholder="Supervisor Línea 3" /></label>
        <label className="field-label">Modelo<select value={modelId} onChange={(e) => setModelId(e.target.value)} required><option value="">Selecciona un modelo</option>{models.map((model) => <option key={model.id} value={model.id}>{model.manufacturer} · {model.model}</option>)}</select></label>
        <div className="form-row"><label className="field-label">Instalación<select value={siteId} onChange={(e) => setSiteId(e.target.value)} required><option value="">Selecciona instalación</option>{sites.map((site) => <option key={site.id} value={site.id}>{site.name}</option>)}</select></label><label className="field-label">Grupo operativo (opcional)<select value={groupId} onChange={(e) => setGroupId(e.target.value)}><option value="">Sin asignar</option>{groups.map((group) => <option key={group.id} value={group.id}>{group.name}</option>)}</select></label></div>
        <label className="field-label">Estado inicial<select value={initialStatus} onChange={(e) => setInitialStatus(e.target.value)}><option value="offline">Sin conexión</option><option value="online">En línea (demo)</option><option value="maintenance">Mantención</option></select></label>
        <div className="modal-note">El estado y la batería son datos operativos locales de demostración. La integración de telemetría se incorpora mediante adaptadores de proveedor.</div>
        <div className="modal-actions"><button type="button" className="button button-quiet" onClick={() => setModal(false)}>Cancelar</button><button className="button button-primary" disabled={saving}>{saving ? 'Guardando…' : 'Registrar radio'}</button></div>
      </form>
    </Modal>}
  </div>;
}
