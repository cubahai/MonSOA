import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Language, Theme } from '../i18n.service';

export interface UserSettings { language: Language; theme: Theme; }
export interface TwoFactorStatus { enabled: boolean; recoveryCodesRemaining: number; }
export interface TwoFactorSetup { secret: string; provisioningUri: string; expiresInSeconds: number; }

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly http = inject(HttpClient);
  get() { return this.http.get<UserSettings>('/api/settings'); }
  save(settings: UserSettings) { return this.http.put<UserSettings>('/api/settings', settings); }
  twoFactorStatus() { return this.http.get<TwoFactorStatus>('/api/auth/2fa/status'); }
  beginSetup(password: string) { return this.http.post<TwoFactorSetup>('/api/auth/2fa/setup', { password }); }
  confirmSetup(code: string) { return this.http.post<{ recoveryCodes: string[] }>('/api/auth/2fa/confirm', { code }); }
  disable(password: string, code: string) { return this.http.post<void>('/api/auth/2fa/disable', { password, code }); }
  regenerate(password: string, code: string) { return this.http.post<{ recoveryCodes: string[] }>('/api/auth/2fa/recovery/regenerate', { password, code }); }
  changePassword(currentPassword: string, newPassword: string, code: string) {
    return this.http.post<void>('/api/auth/change-password', { currentPassword, newPassword, code });
  }
}
