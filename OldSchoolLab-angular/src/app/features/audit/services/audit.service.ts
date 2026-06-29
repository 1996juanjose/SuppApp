import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { AuditItem } from '../models/audit.models';

@Injectable({ providedIn: 'root' })
export class AuditService {
  constructor(private readonly http: HttpClient) {}

  getAudit(filters: { companyId?: number | null; tableName?: string | null; userName?: string | null } = {}): Observable<AuditItem[]> {
    let params = new HttpParams();

    if (filters.companyId !== null && filters.companyId !== undefined) {
      params = params.set('companyId', filters.companyId);
    }

    if (filters.tableName) {
      params = params.set('tableName', filters.tableName);
    }

    if (filters.userName) {
      params = params.set('userName', filters.userName);
    }

    return this.http.get<AuditItem[]>(`${environment.businessApiUrl}/api/audit`, { params });
  }
}
