export interface AuditItem {
  id: number;
  companyId?: number | null;
  tableName: string;
  recordId: number;
  action: string;
  changedByUserId: string;
  changedByUserName: string;
  changedAt: string;
  details?: string | null;
}
