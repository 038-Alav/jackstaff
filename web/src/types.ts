export type TenantSummary = { id: string; name: string; slug: string; role: string };
export type AuthSession = {
  accessToken: string;
  expiresAtUtc: string;
  user: { id: string; email: string; name: string };
  activeTenant: TenantSummary;
  tenants: TenantSummary[];
};
export type DashboardSummary = {
  totalRadios: number;
  onlineRadios: number;
  offlineRadios: number;
  maintenanceRadios: number;
  activeSites: number;
  activeGroups: number;
  openTickets: number;
  criticalTickets: number;
};
export type DashboardTicket = {
  id: string; ticketNumber: string; title: string; type: string; priority: string;
  status: string; createdAt: string; radioAlias: string | null;
};
export type Site = {
  id: string; name: string; code: string; siteType: string; address: string | null;
  isActive: boolean; radioCount: number;
};
export type RadioModel = { id: string; manufacturer: string; model: string; family: string };
export type Radio = {
  id: string; assetTag: string; serialNumber: string; alias: string; manufacturer: string;
  model: string; status: string; batteryPercent: number | null; lastSeenAt: string | null;
  siteId: string; siteName: string; radioModelId: string; groupName: string | null; groupId: string | null;
};
export type OperationalGroup = {
  id: string; name: string; description: string | null; isActive: boolean; radioCount: number;
};
export type Ticket = {
  id: string; ticketNumber: string; title: string; description: string; type: string;
  priority: string; status: string; radioId: string | null; radioAlias: string | null;
  siteId: string | null; siteName: string | null; requestedGroupId: string | null;
  requestedGroupName: string | null; createdAt: string; updatedAt: string; createdByName: string;
};

export type TicketComment = {
  id: string; ticketId: string; body: string; authorName: string; createdAt: string;
};
export type TicketHistory = {
  id: string; fromStatus: string | null; toStatus: string; note: string | null; actorName: string; createdAt: string;
};
