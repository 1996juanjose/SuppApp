export interface LoginRequest {
  username: string;
  password: string;
  companyId?: number | null;
}

export interface LoginResponse {
  token: string;
  tokenType: string;
  username?: string;
  roles?: string[];
  companyId?: number | null;
  companyName?: string | null;
}

export interface AuthUser {
  username: string;
  roles: string[];
  token: string;
  companyId?: number | null;
  companyName?: string | null;
}

export interface MenuItem {
  label: string;
  route: string;
  roles?: string[];
}

export interface CompanyLookup {
  id: number;
  name: string;
}
