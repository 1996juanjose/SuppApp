import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ProductItem } from '../models/products.models';

@Injectable({ providedIn: 'root' })
export class ProductsService {
  constructor(private readonly http: HttpClient) {}

  getProducts(companyId?: number | null): Observable<ProductItem[]> {
    let params = new HttpParams();

    if (companyId !== null && companyId !== undefined) {
      params = params.set('companyId', companyId);
    }

    return this.http.get<ProductItem[]>(`${environment.businessApiUrl}/api/products`, { params });
  }
}
