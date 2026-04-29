import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, map, tap } from 'rxjs';
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

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly loginUrl = `${environment.apiUrl}/Auth/Login`;
  private readonly tokenKey = 'auth_token';
  private readonly userKey = 'auth_user';

  private readonly tokenSignal = signal<string | null>(this.getStoredToken());
  private readonly userSignal = signal<User | null>(this.getStoredUser());

  readonly isAuthenticated = computed(() => !!this.tokenSignal());
  readonly currentUser = computed(() => this.userSignal());
  readonly token = computed(() => this.tokenSignal());
  readonly role = computed<AppRole>(() => (this.userSignal()?.role as AppRole) ?? 'Commercial');

  constructor(private readonly http: HttpClient, private readonly router: Router) {}

  getRole(): AppRole {
    return this.role();
  }

  hasRole(roles: string[]): boolean {
    const currentRole = String(this.getRole() ?? '').trim().toLowerCase();
    return roles.some(role => String(role ?? '').trim().toLowerCase() === currentRole);
  }

  login(request: LoginRequest): Observable<LoginResponseData> {
    return this.http.post<ApiResponse<LoginResponseData> | LoginResponseData>(this.loginUrl, request).pipe(
      map(response => {
        const wrapped = response as ApiResponse<LoginResponseData>;
        if (wrapped?.success !== undefined) {
          if (!wrapped.success) {
            throw new Error(wrapped.message || 'Erreur de connexion');
          }
          return wrapped.data;
        }

        const direct = response as LoginResponseData;
        if (!direct?.token) {
          throw new Error('Reponse de login invalide.');
        }

        return direct;
      }),
      tap(data => this.storeSession(data))
    );
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

  private storeSession(data: LoginResponseData): void {
    let role = data.role || 'Commercial';
    let fullName = data.fullName;
    let email = `${data.username}@sapb1.local`;

    const claims = this.decodeJwt(data.token);
    if (claims) {
      role = (claims['role'] as string) || role;
      fullName = (claims['fullName'] as string) || fullName;
      email = (claims['email'] as string) || email;
    }

    const user: User = {
      id: 1,
      username: data.username,
      email,
      fullName,
      role
    };

    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(this.tokenKey, data.token);
      localStorage.setItem(this.userKey, JSON.stringify(user));
    }

    this.tokenSignal.set(data.token);
    this.userSignal.set(user);
  }

  private decodeJwt(token: string): Record<string, unknown> | null {
    try {
      const parts = token.split('.');
      if (parts.length !== 3) {
        return null;
      }

      const payload = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      const decoded = typeof atob !== 'undefined'
        ? atob(payload)
        : Buffer.from(payload, 'base64').toString();

      return JSON.parse(decoded);
    } catch {
      return null;
    }
  }

  private getStoredToken(): string | null {
    return typeof localStorage === 'undefined' ? null : localStorage.getItem(this.tokenKey);
  }

  private getStoredUser(): User | null {
    if (typeof localStorage === 'undefined') {
      return null;
    }

    const user = localStorage.getItem(this.userKey);
    return user ? JSON.parse(user) : null;
  }
}


