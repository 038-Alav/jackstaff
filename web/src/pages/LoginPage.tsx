import { useState, type FormEvent } from 'react';
import { Anchor, ArrowRight, Eye, EyeOff, LockKeyhole, Mail } from 'lucide-react';
import { apiPost, setToken } from '../api';
import type { AuthSession } from '../types';

export function LoginPage({ onAuthenticated }: { onAuthenticated: (session: AuthSession) => void }) {
  const [email, setEmail] = useState('admin@jackstaff.demo');
  const [password, setPassword] = useState('JackstaffDemo!2026');
  const [tenantSlug, setTenantSlug] = useState('metro-demo');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault(); setError(''); setLoading(true);
    try {
      const session = await apiPost<AuthSession>('/auth/login', { email, password, tenantSlug });
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
        <label className="field-label">Organización</label>
        <select value={tenantSlug} onChange={(e) => setTenantSlug(e.target.value)}>
          <option value="metro-demo">Metro Demo</option><option value="security-demo">Security Demo</option><option value="logistics-demo">Logistics Demo</option>
        </select>
        <label className="field-label">Correo electrónico</label>
        <div className="input-icon-wrap"><Mail size={17} /><input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required autoComplete="username" /></div>
        <label className="field-label">Contraseña</label>
        <div className="input-icon-wrap"><LockKeyhole size={17} /><input type={showPassword ? 'text' : 'password'} value={password} onChange={(e) => setPassword(e.target.value)} required autoComplete="current-password" /><button type="button" className="password-toggle" onClick={() => setShowPassword(!showPassword)} aria-label="Mostrar contraseña">{showPassword ? <EyeOff size={16} /> : <Eye size={16} />}</button></div>
        {error && <div className="form-error">{error}</div>}
        <button type="submit" className="button button-primary login-submit" disabled={loading}>{loading ? 'Ingresando…' : 'Ingresar al workspace'}<ArrowRight size={17} /></button>
        <div className="demo-credentials"><b>ENTORNO DE DEMOSTRACIÓN</b><span>admin@jackstaff.demo</span><span>JackstaffDemo!2026</span></div>
        <p className="login-footnote">v0.1 · Datos ficticios de demostración · No controla radios reales.</p>
      </form>
    </div>
  </main>;
}
