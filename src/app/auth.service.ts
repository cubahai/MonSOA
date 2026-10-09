import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export interface DemoUser {
  username: string;
  fullName: string;
  role: string;
}
export interface LoginResult { requiresTwoFactor: boolean; user: DemoUser | null; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  login(username: string, password: string, rememberMe: boolean) {
    return this.http.post<LoginResult>('/api/auth/login', { username, password, rememberMe });
  }

  verifyTwoFactor(code: string) {
    return this.http.post<LoginResult>('/api/auth/2fa/verify', { code });
  }

  currentUser() {
    return this.http.get<DemoUser>('/api/auth/me');
  }

  logout() {
    return this.http.post<void>('/api/auth/logout', {});
  }
}
