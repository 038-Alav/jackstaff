import { useEffect, useState } from 'react';
import {
  Activity, Anchor, Bell, Building2, ChevronDown, CircleHelp, Command, Headphones,
  LayoutDashboard, LogOut, RadioTower, Settings2, ShieldCheck, Users,
} from 'lucide-react';
import { api, apiPost, clearToken, getToken, setToken } from './api';
import type { AuthSession } from './types';
import { LoginPage } from './pages/LoginPage';
import { DashboardPage } from './pages/DashboardPage';
import { RadiosPage } from './pages/RadiosPage';
import { SitesPage } from './pages/SitesPage';
import { GroupsPage } from './pages/GroupsPage';
import { TicketsPage } from './pages/TicketsPage';

type Page = 'dashboard' | 'radios' | 'sites' | 'groups' | 'tickets';

const navigation: { id: Page; label: string; icon: typeof LayoutDashboard; group: string }[] = [
  { id: 'dashboard', label: 'Command Center', icon: LayoutDashboard, group: 'WORKSPACE' },
  { id: 'radios', label: 'Inventario de radios', icon: RadioTower, group: 'ASSET MANAGEMENT' },
  { id: 'sites', label: 'Instalaciones', icon: Building2, group: 'ASSET MANAGEMENT' },
  { id: 'groups', label: 'Grupos operativos', icon: Users, group: 'OPERACIONES' },
  { id: 'tickets', label: 'Mesa de ayuda', icon: Headphones, group: 'OPERACIONES' },
];

export default function App() {
  const [session, setSession] = useState<AuthSession | null>(null);
  const [restoring, setRestoring] = useState(Boolean(getToken()));
  const [page, setPage] = useState<Page>('dashboard');
  const [switching, setSwitching] = useState(false);
  const [toast, setToast] = useState('');
  const [tenantMenuOpen, setTenantMenuOpen] = useState(false);
  const [apiHealth, setApiHealth] = useState<'checking' | 'online' | 'offline'>('checking');

  useEffect(() => {
    const token = getToken();
    if (!token) return;
    let active = true;
    api<AuthSession>('/auth/me').then((result) => {
      if (active) { setToken(result.accessToken); setSession(result); setPage(result.workspaceType === 'platform_staff' ? 'tickets' : 'dashboard'); }
    }).catch(() => { if (active) { clearToken(); setSession(null); } })
      .finally(() => { if (active) setRestoring(false); });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    if (!toast) return;
    const timeout = window.setTimeout(() => setToast(''), 3600);
    return () => window.clearTimeout(timeout);
  }, [toast]);

  useEffect(() => {
    let active = true;
    const checkHealth = () => {
      api<{ status: string }>('/health')
        .then(() => { if (active) setApiHealth('online'); })
        .catch(() => { if (active) setApiHealth('offline'); });
    };
    checkHealth();
    const interval = window.setInterval(checkHealth, 15000);
    return () => { active = false; window.clearInterval(interval); };
  }, []);

  async function switchTenant(slug: string) {
    if (!session || !session.activeTenant || session.workspaceType !== 'tenant' || slug === session.activeTenant.slug) { setTenantMenuOpen(false); return; }
    setSwitching(true); setTenantMenuOpen(false);
    try {
      const next = await apiPost<AuthSession>('/auth/switch-tenant', { tenantSlug: slug });
      setToken(next.accessToken); setSession(next); setPage('dashboard');
      setToast(`Espacio de trabajo cambiado a ${next.activeTenant?.name ?? 'la organización seleccionada'}.`);
    } catch (error) { setToast(error instanceof Error ? error.message : 'No fue posible cambiar de organización.'); }
    finally { setSwitching(false); }
  }

  function logout() { clearToken(); setSession(null); setPage('dashboard'); }

  if (restoring) return <div className="app-loading"><span className="loader-mark"><Anchor size={22} /></span><span>Preparando JACKSTAFF…</span></div>;
  if (!session) return <LoginPage onAuthenticated={(next) => {
    setSession(next);
    setPage(next.workspaceType === 'platform_staff' ? 'tickets' : 'dashboard');
  }} />;

  const isPlatformStaff = session.workspaceType === 'platform_staff';
  const visibleNavigation = isPlatformStaff ? navigation.filter((item) => item.id === 'tickets') : navigation;
  const current = visibleNavigation.find((item) => item.id === page) ?? visibleNavigation[0];
  const canManage = isPlatformStaff || ['tenant_admin', 'support'].includes(session.activeTenant?.role ?? '');
  const groups = [...new Set(visibleNavigation.map((item) => item.group))];
  const workspaceName = session.activeTenant?.name ?? 'Soporte JACKSTAFF';

  return <div className="app-shell">
    <aside className="sidebar">
      <div className="sidebar-brand"><span className="brand-mark"><Anchor size={20} /></span><div><b>JACKSTAFF</b><small>FLEET OPERATIONS</small></div></div>
      <div className="workspace-selector-wrap">
        <span className="sidebar-label">WORKSPACE</span>
        {isPlatformStaff ? <div className="workspace-selector">
          <span className="tenant-avatar">S</span>
          <span className="tenant-selector-text"><b>Soporte JACKSTAFF</b><small>platform support</small></span>
        </div> : <>
          <button className="workspace-selector" onClick={() => setTenantMenuOpen(!tenantMenuOpen)} disabled={switching}>
            <span className="tenant-avatar">{workspaceName.slice(0, 1).toUpperCase()}</span>
            <span className="tenant-selector-text"><b>{workspaceName}</b><small>{session.activeTenant?.role.replace('_', ' ')}</small></span>
            <ChevronDown size={15} className={tenantMenuOpen ? 'rotate-chevron' : ''} />
          </button>
          {tenantMenuOpen && <div className="tenant-menu">{session.tenants.map((tenant) => <button key={tenant.id} className={tenant.slug === session.activeTenant?.slug ? 'tenant-option selected' : 'tenant-option'} onClick={() => void switchTenant(tenant.slug)}><span className="tenant-avatar small">{tenant.name.slice(0, 1).toUpperCase()}</span><span><b>{tenant.name}</b><small>{tenant.role.replace('_', ' ')}</small></span>{tenant.slug === session.activeTenant?.slug && <ShieldCheck size={15} />}</button>)}</div>}
        </>}
      </div>
      <nav className="sidebar-nav" aria-label="Navegación principal">
        {groups.map((groupName) => <div className="nav-group" key={groupName}><span className="sidebar-label">{groupName}</span>{visibleNavigation.filter((item) => item.group === groupName).map((item) => {
          const Icon = item.icon;
          return <button key={item.id} className={`nav-item ${page === item.id ? 'active' : ''}`} onClick={() => setPage(item.id)}><Icon size={17} /><span>{item.label}</span>{item.id === 'tickets' && <span className="nav-new-dot" />}</button>;
        })}</div>)}
      </nav>
      <div className="sidebar-bottom">
        <div className="sidebar-status"><span className="sidebar-status-icon"><Activity size={16} /></span><span><b>{apiHealth === 'online' ? 'API conectada' : apiHealth === 'checking' ? 'Verificando API' : 'API sin conexión'}</b><small>JACKSTAFF v0.1</small></span><i className={`api-health-${apiHealth}`} /></div>
        <button className="nav-item utility-nav" onClick={() => setToast('La configuración de plataforma se incorpora en una versión posterior.')}><Settings2 size={17} /><span>Configuración</span></button>
        <button className="nav-item utility-nav" onClick={() => setToast('Consulta la documentación del proyecto en la carpeta docs.')}><CircleHelp size={17} /><span>Ayuda y documentación</span></button>
        <div className="sidebar-user"><span className="user-avatar">{session.user.name.split(' ').map((part) => part[0]).slice(0, 2).join('').toUpperCase()}</span><span className="user-meta"><b>{session.user.name}</b><small>{session.user.email}</small></span><button className="logout-button" onClick={logout} title="Cerrar sesión"><LogOut size={16} /></button></div>
      </div>
    </aside>
    <main className="main-area">
      <header className="topbar"><div className="topbar-breadcrumb"><span>JACKSTAFF</span><span>/</span><b>{current.label}</b></div><div className="topbar-right"><span className="topbar-context"><Command size={14} /> {workspaceName}</span><button className="topbar-icon" onClick={() => setToast('No tienes notificaciones pendientes.')} title="Notificaciones"><Bell size={17} /></button><span className="topbar-divider" /><span className="topbar-version">v0.1.0</span></div></header>
      <div className="main-scroll" key={`${page}-${session.activeTenant?.slug ?? session.workspaceType}`}>
        {page === 'dashboard' && session.activeTenant && <DashboardPage tenantName={session.activeTenant.name} onNavigate={(next) => setPage(next as Page)} />}
        {page === 'radios' && <RadiosPage />}
        {page === 'sites' && <SitesPage />}
        {page === 'groups' && <GroupsPage />}
        {page === 'tickets' && <TicketsPage canManage={canManage} supportMode={isPlatformStaff} />}
      </div>
    </main>
    {toast && <div className="toast-message">{toast}</div>}
  </div>;
}
