import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, of, tap, catchError, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AppRole } from '../models/permissions';

export interface LoginRequest {
  username: string;
  password: string;
}

export interface ApiResponse<T> {
  success: boolean;
  message: string | null;
  data: T;
}

export interface LoginResponseData {
  token: string;
  username: string;
  fullName: string;
  role: string;
  expires: string;
}

export interface User {
  id: number;
  username: string;
  email: string;
  fullName: string;
  role: string;
}

const MOCK_USERS: Record<string, LoginResponseData> = {
  admin: {
    token: 'mock-jwt-token-dev-only', username: 'admin',
    fullName: 'Administrateur', role: 'Admin', expires: '2099-12-31T23:59:59Z'
  },
  manager: {
    token: 'mock-jwt-token-dev-only', username: 'manager',
    fullName: 'Responsable', role: 'Manager', expires: '2099-12-31T23:59:59Z'
  },
  commercial: {
    token: 'mock-jwt-token-dev-only', username: 'commercial',
    fullName: 'Commercial Terrain', role: 'Commercial', expires: '2099-12-31T23:59:59Z'
  }
};

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly apiUrl = `${environment.apiUrl}/auth`;
  private readonly tokenKey = 'auth_token';
  private readonly userKey = 'auth_user';

  private tokenSignal = signal<string | null>(this.getStoredToken());
  private userSignal = signal<User | null>(this.getStoredUser());

  isAuthenticated = computed(() => !!this.tokenSignal());
  currentUser = computed(() => this.userSignal());
  token = computed(() => this.tokenSignal());
  role = computed<AppRole>(() => (this.userSignal()?.role as AppRole) ?? 'Commercial');

  constructor(private http: HttpClient, private router: Router) {}

  /* ── Role helpers ──────────────────────────────────────────────────── */

  getRole(): AppRole { return this.role(); }

  hasRole(roles: string[]): boolean { return roles.includes(this.getRole()); }

  private decodeJwt(token: string): Record<string, unknown> | null {
    try {
      const parts = token.split('.');
      if (parts.length !== 3) return null;
      const payload = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      const decoded = typeof atob !== 'undefined'
        ? atob(payload)
        : Buffer.from(payload, 'base64').toString();
      return JSON.parse(decoded);
    } catch { return null; }
  }


  login(request: LoginRequest): Observable<LoginResponseData> {
    return this.http.post<ApiResponse<LoginResponseData>>(`${this.apiUrl}/login`, request).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Erreur de connexion');
        }
        return response.data;
      }),
      tap(data => this.storeSession(data)),
      catchError(err => {
        if (err.status === 0 || err.statusText === 'Timeout') {
          const mockData = MOCK_USERS[request.username];
          if (mockData && request.password === request.username) {
            console.warn(`Backend injoignable -> connexion mock (${mockData.role}).`);
            return of(mockData).pipe(tap(d => this.storeSession(d)));
          }
        }
        throw err;
      })
    );
  }


  private storeSession(data: LoginResponseData): void {
    let role = data.role || 'Commercial';
    let fullName = data.fullName;
    let email = `${data.username}@sapb1.local`;

    // Extract claims from real JWT tokens
    const claims = this.decodeJwt(data.token);
    if (claims) {
      role     = (claims['role'] as string) || role;
      fullName = (claims['fullName'] as string) || fullName;
      email    = (claims['email'] as string) || email;
    }

    const user: User = { id: 1, username: data.username, email, fullName, role };
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(this.tokenKey, data.token);
      localStorage.setItem(this.userKey, JSON.stringify(user));
    }
    this.tokenSignal.set(data.token);
    this.userSignal.set(user);
  }

  clearSession(): void {
    if (typeof localStorage !== 'undefined') {
      localStorage.removeItem(this.tokenKey);
      localStorage.removeItem(this.userKey);
    }
    this.tokenSignal.set(null);
    this.userSignal.set(null);
  }

  logout(): void {
    this.clearSession();
    this.router.navigate(['/login']);
  }


  private getStoredToken(): string | null {
    if (typeof localStorage === 'undefined') return null;
    return localStorage.getItem(this.tokenKey);
  }

  private getStoredUser(): User | null {
    if (typeof localStorage === 'undefined') return null;
    const user = localStorage.getItem(this.userKey);
    return user ? JSON.parse(user) : null;
  }
}
