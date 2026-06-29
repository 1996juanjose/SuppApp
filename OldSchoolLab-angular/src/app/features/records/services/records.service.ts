import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { RecordAlert, RecordAlertResponse, RecordDetail, RecordFilter, RecordItem, RecordProductsOption, RecordStatusOption, RecordSummaryResponse, RecordUpdateRequest } from '../models/records.models';

@Injectable({ providedIn: 'root' })
export class RecordsService {
  constructor(private readonly http: HttpClient) {}

  getRecords(filter: RecordFilter = {}): Observable<RecordItem[]> {
    let params = new HttpParams();

    if (filter.search) {
      params = params.set('search', filter.search);
    }

    if (filter.fromDate) {
      params = params.set('fromDate', filter.fromDate);
    }

    if (filter.toDate) {
      params = params.set('toDate', filter.toDate);
    }

    if (filter.companyId !== null && filter.companyId !== undefined) {
      params = params.set('companyId', filter.companyId);
    }

    if (filter.statusIds?.length) {
      for (const statusId of filter.statusIds) {
        params = params.append('statusIds', statusId);
      }
    }

    return this.http.get<RecordItem[]>(`${environment.businessApiUrl}/api/records`, { params });
  }

  getRecord(id: number, companyId?: number | null): Observable<RecordDetail> {
    let params = new HttpParams();

    if (companyId !== null && companyId !== undefined) {
      params = params.set('companyId', companyId);
    }

    return this.http.get<RecordDetail>(`${environment.businessApiUrl}/api/records/${id}`, { params });
  }

  updateRecord(id: number, payload: RecordUpdateRequest): Observable<RecordDetail> {
    return this.http.put<RecordDetail>(`${environment.businessApiUrl}/api/records/${id}`, payload);
  }

  getNextCall(companyId?: number | null): Observable<{ now: string; nextCallScheduledAt: string | null; alerts: RecordAlert[] }> {
    let params = new HttpParams();

    if (companyId !== null && companyId !== undefined) {
      params = params.set('companyId', companyId);
    }

    return this.http.get<{ now: string; nextCallScheduledAt: string | null; alerts: RecordAlert[] }>(
      `${environment.businessApiUrl}/api/alerts/calls`,
      { params }
    );
  }

  getCallSummary(): Observable<RecordSummaryResponse> {
    return this.http.get<RecordSummaryResponse>(`${environment.businessApiUrl}/api/records/calls/summary`);
  }

  getStatuses(companyId?: number | null): Observable<RecordStatusOption[]> {
    let params = new HttpParams();

    if (companyId !== null && companyId !== undefined) {
      params = params.set('companyId', companyId);
    }

    return this.http.get<RecordStatusOption[]>(`${environment.businessApiUrl}/api/records/statuses`, { params });
  }

  getProducts(companyId?: number | null): Observable<RecordProductsOption[]> {
    let params = new HttpParams();

    if (companyId !== null && companyId !== undefined) {
      params = params.set('companyId', companyId);
    }

    return this.http.get<RecordProductsOption[]>(`${environment.businessApiUrl}/api/records/products`, { params });
  }

  getDueAlerts(minutesAhead = 5): Observable<RecordAlertResponse> {
    return this.http.get<RecordAlertResponse>(
      `${environment.businessApiUrl}/api/alerts/calls`,
      { params: new HttpParams().set('minutesAhead', minutesAhead) }
    );
  }
}
