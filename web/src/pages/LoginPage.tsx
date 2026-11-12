import { useEffect, useState, type FormEvent } from 'react';
import { Anchor, ArrowRight, Eye, EyeOff, LockKeyhole, Mail } from 'lucide-react';
import { api, apiPost, setToken } from '../api';
import type { AuthSession, LoginWorkspace } from '../types';

function workspaceKey(workspace: LoginWorkspace): string {
  return `${workspace.workspaceType}:${workspace.tenantSlug ?? 'platform'}`;
}

export function LoginPage({ onAuthenticated }: { onAuthenticated: (session: AuthSession) => void }) {
  const [email, setEmail] = useState('admin@jackstaff.demo');
  const [password, setPassword] = useState('JackstaffDemo!2026');
  const [workspaces, setWorkspaces] = useState<LoginWorkspace[]>([]);
  const [selectedWorkspaceKey, setSelectedWorkspaceKey] = useState('');
  const [workspacesLoading, setWorkspacesLoading] = useState(true);
  const [workspacesError, setWorkspacesError] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let active = true;
    api<LoginWorkspace[]>('/auth/workspaces')
      .then((rows) => {
        if (!active) return;
        setWorkspaces(rows);
        if (rows.length) setSelectedWorkspaceKey(workspaceKey(rows[0]));
      })
      .catch((err) => {
        if (active) setWorkspacesError(err instanceof Error ? err.message : 'No fue posible cargar los espacios de trabajo.');
      })
      .finally(() => { if (active) setWorkspacesLoading(false); });
    return () => { active = false; };
  }, []);

  const selectedWorkspace = workspaces.find((workspace) => workspaceKey(workspace) === selectedWorkspaceKey);

  function selectWorkspace(key: string) {
    setSelectedWorkspaceKey(key);
    const workspace = workspaces.find((item) => workspaceKey(item) === key);
    if (workspace?.workspaceType === 'platform_staff') setEmail('support@jackstaff.demo');
    else if (workspace?.workspaceType === 'tenant') setEmail('admin@jackstaff.demo');
  }

  async function submit(event: FormEvent) {
    event.preventDefault(); setError(''); setLoading(true);
    try {
      if (!selectedWorkspace) throw new Error('Selecciona un espacio de trabajo disponible.');
      const session = await apiPost<AuthSession>('/auth/login', {
        email,
        password,
        workspaceType: selectedWorkspace.workspaceType,
        tenantSlug: selectedWorkspace.tenantSlug,
      });
      setToken(session.accessToken); onAuthenticated(session);
    } catch (err) { setError(err instanceof Error ? err.message : 'No fue posible iniciar sesión.'); }
    finally { setLoading(false); }
  }

  return <main className="login-shell">
    <div className="login-left">
      <div className="brand-lockup"><span className="brand-mark"><Anchor size={21} /></span><div><b>JACKSTAFF</b><small>FLEET OPERATIONS</small></div></div>
      <div className="login-story">
        <span className="eyebrow light">FLEET & COMMUNICATIONS OPERATIONS PLATFORM</span>
        <h1>Una operación.<br /><em>Cada activo.</em></h1>
        <p>Inventario, coordinación y soporte para activos de comunicación, en un espacio de trabajo diseñado para cada organización.</p>
      </div>
      <div className="login-bottom"><span>ONE PLATFORM. EVERY FLEET.</span><span>FOXTROT · INTERNAL CODENAME</span></div>
    </div>
    <div className="login-right">
      <form className="login-card" onSubmit={submit}>
        <div className="login-card-heading"><div className="eyebrow">WORKSPACE ACCESS</div><h2>Bienvenido de vuelta</h2><p>Ingresa a tu espacio de trabajo.</p></div>
        <label className="field-label">Espacio de trabajo</label>
        <select value={selectedWorkspaceKey} onChange={(e) => selectWorkspace(e.target.value)} disabled={workspacesLoading || workspaces.length === 0}>
          {workspacesLoading && <option value="">Cargando espacios…</option>}
          {!workspacesLoading && workspaces.length === 0 && <option value="">Sin espacios disponibles</option>}
          {workspaces.map((workspace) => <option key={workspaceKey(workspace)} value={workspaceKey(workspace)}>{workspace.name}</option>)}
        </select>
        {workspacesError && <div className="form-error">{workspacesError}</div>}
        <label className="field-label">Correo electrónico</label>
        <div className="input-icon-wrap"><Mail size={17} /><input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required autoComplete="username" /></div>
        <label className="field-label">Contraseña</label>
        <div className="input-icon-wrap"><LockKeyhole size={17} /><input type={showPassword ? 'text' : 'password'} value={password} onChange={(e) => setPassword(e.target.value)} required autoComplete="current-password" /><button type="button" className="password-toggle" onClick={() => setShowPassword(!showPassword)} aria-label="Mostrar contraseña">{showPassword ? <EyeOff size={16} /> : <Eye size={16} />}</button></div>
        {error && <div className="form-error">{error}</div>}
        <button type="submit" className="button button-primary login-submit" disabled={loading || workspacesLoading || !selectedWorkspace}>{loading ? 'Ingresando…' : 'Ingresar al workspace'}<ArrowRight size={17} /></button>
        <div className="demo-credentials"><b>ENTORNO DE DEMOSTRACIÓN</b><span>Clientes: admin@jackstaff.demo</span><span>Soporte: support@jackstaff.demo</span><span>Contraseña: JackstaffDemo!2026</span></div>
        <p className="login-footnote">v0.1 · Datos ficticios de demostración · No controla radios reales.</p>
      </form>
    </div>
  </main>;
}
