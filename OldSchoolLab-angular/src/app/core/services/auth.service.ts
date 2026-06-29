import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, map, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthUser, CompanyLookup, LoginRequest, LoginResponse } from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly storageKey = 'oldschoollab.angular.auth';
  private readonly _currentUser = signal<AuthUser | null>(this.restoreUser());

  readonly currentUser = computed(() => this._currentUser());
  readonly roles = computed(() => this._currentUser()?.roles ?? []);
  readonly isAuthenticated = computed(() => !!this._currentUser()?.token);

  constructor(private readonly http: HttpClient, private readonly router: Router) {}

  login(request: LoginRequest): Observable<AuthUser> {
    return this.http.post<LoginResponse & Partial<AuthUser>>(`${environment.authApiUrl}/auth/login`, request).pipe(
      map(response => this.createUserFromResponse(response)),
      tap(user => this.persistUser(user))
    );
  }

  getCompanies(): Observable<CompanyLookup[]> {
    return this.http.get<CompanyLookup[]>(`${environment.authApiUrl}/auth/companies`);
  }

  logout(): void {
    localStorage.removeItem(this.storageKey);
    this._currentUser.set(null);
    this.router.navigate(['/login']);
  }

  hasAnyRole(requiredRoles: readonly string[]): boolean {
    if (!requiredRoles.length) {
      return true;
    }

    const roles = this.roles();
    return roles.some(role => requiredRoles.includes(role));
  }

  getToken(): string | null {
    return this._currentUser()?.token ?? null;
  }

  private persistUser(user: AuthUser): void {
    localStorage.setItem(this.storageKey, JSON.stringify(user));
    this._currentUser.set(user);
  }

  private restoreUser(): AuthUser | null {
    const raw = localStorage.getItem(this.storageKey);
    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw) as AuthUser;
    } catch {
      localStorage.removeItem(this.storageKey);
      return null;
    }
  }

  private createUserFromResponse(response: LoginResponse & Partial<AuthUser>): AuthUser {
    if (response.username && response.roles) {
      return {
        username: response.username,
        roles: response.roles,
        token: response.token,
        companyId: response.companyId ?? null,
        companyName: response.companyName ?? null
      };
    }

    return this.createUserFromToken(response.token);
  }

  private createUserFromToken(token: string): AuthUser {
    const payload = this.decodeJwtPayload(token);
    const roles = this.extractRoles(payload);
    const username = this.getStringProperty(payload, 'unique_name')
      ?? this.getStringProperty(payload, 'name')
      ?? this.getStringProperty(payload, 'sub')
      ?? 'usuario';

    return {
      username,
      roles,
      token,
      companyId: this.parseNumber(payload?.['company_id']),
      companyName: this.getStringProperty(payload, 'company_name')
    };
  }

  private decodeJwtPayload(token: string): Record<string, unknown> | null {
    const parts = token.split('.');
    if (parts.length !== 3) {
      return null;
    }

    try {
      const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      const json = decodeURIComponent(
        atob(base64)
          .split('')
          .map(char => `%${char.charCodeAt(0).toString(16).padStart(2, '0')}`)
          .join('')
      );
      return JSON.parse(json) as Record<string, unknown>;
    } catch {
      return null;
    }
  }

  private extractRoles(payload: Record<string, unknown> | null): string[] {
    if (!payload) {
      return [];
    }

    const rawRoles = payload['role'] ?? payload['roles'] ?? payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
    if (Array.isArray(rawRoles)) {
      return rawRoles.map(role => String(role));
    }

    if (typeof rawRoles === 'string') {
      return [rawRoles];
    }

    return [];
  }

  private parseNumber(value: unknown): number | null {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }

  private getStringProperty(payload: Record<string, unknown> | null, key: string): string | null {
    const value = payload?.[key];
    return typeof value === 'string' ? value : null;
  }
}
