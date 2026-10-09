import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import * as QRCode from 'qrcode';
import { I18nService, Language, Theme } from '../i18n.service';
import { SettingsService, TwoFactorSetup, TwoFactorStatus } from './settings.service';

@Component({
  selector: 'app-settings',
  imports: [FormsModule],
  templateUrl: './settings.html',
  styleUrl: './settings.css'
})
export class Settings implements OnInit {
  private readonly api = inject(SettingsService);
  private readonly router = inject(Router);
  private readonly cdr = inject(ChangeDetectorRef);
  readonly i18n = inject(I18nService);

  language: Language = this.i18n.language();
  theme: Theme = this.i18n.theme();
  status?: TwoFactorStatus;
  setup?: TwoFactorSetup;
  qrUrl = '';
  setupPassword = '';
  setupCode = '';
  actionPassword = '';
  actionCode = '';
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  changeCode = '';
  recoveryCodes: string[] = [];
  busy = false;
  message = '';
  error = '';

  ngOnInit(): void {
    this.api.get().subscribe({
      next: value => {
        this.language = value.language;
        this.theme = value.theme;
        this.i18n.setLanguage(value.language);
        this.i18n.setTheme(value.theme);
        this.cdr.markForCheck();
      },
      error: () => this.fail('Không tải được cài đặt.')
    });
    this.refreshStatus();
  }

  savePreferences(): void {
    this.run(() => this.api.save({ language: this.language, theme: this.theme }), () => {
      this.i18n.setLanguage(this.language);
      this.i18n.setTheme(this.theme);
      this.message = 'Đã lưu cài đặt.';
    });
  }

  beginSetup(): void {
    if (!this.setupPassword) return this.fail('Nhập mật khẩu hiện tại để tiếp tục.');
    this.run(() => this.api.beginSetup(this.setupPassword), async value => {
      this.setup = value;
      this.setupPassword = '';
      this.setupCode = '';
      this.qrUrl = await QRCode.toDataURL(value.provisioningUri, { width: 216, margin: 2 });
      this.cdr.markForCheck();
    });
  }

  confirmSetup(): void {
    this.run(() => this.api.confirmSetup(this.setupCode), value => {
      this.setup = undefined;
      this.qrUrl = '';
      this.setupCode = '';
      this.recoveryCodes = value.recoveryCodes;
      this.refreshStatus();
    });
  }

  disable(): void {
    this.run(() => this.api.disable(this.actionPassword, this.actionCode), () => {
      this.clearAction();
      void this.router.navigateByUrl('/login');
    });
  }

  regenerate(): void {
    this.run(() => this.api.regenerate(this.actionPassword, this.actionCode), value => {
      this.clearAction();
      this.recoveryCodes = value.recoveryCodes;
      this.refreshStatus();
    });
  }

  changePassword(): void {
    if (this.newPassword.length < 12) return this.fail('Mật khẩu mới cần ít nhất 12 ký tự.');
    if (this.newPassword !== this.confirmPassword) return this.fail('Mật khẩu xác nhận không khớp.');
    this.run(() => this.api.changePassword(this.currentPassword, this.newPassword, this.changeCode), () => {
      this.currentPassword = this.newPassword = this.confirmPassword = this.changeCode = '';
      void this.router.navigateByUrl('/login');
    });
  }

  closeRecoveryCodes(): void { this.recoveryCodes = []; }

  private refreshStatus(): void {
    this.api.twoFactorStatus().subscribe({
      next: status => { this.status = status; this.cdr.markForCheck(); },
      error: () => this.fail('Không tải được trạng thái bảo mật.')
    });
  }

  private clearAction(): void { this.actionPassword = this.actionCode = ''; }
  private fail(message: string): void { this.error = message; this.cdr.markForCheck(); }

  private run<T>(request: () => import('rxjs').Observable<T>, onSuccess: (value: T) => void | Promise<void>): void {
    if (this.busy) return;
    this.busy = true;
    this.error = this.message = '';
    request().subscribe({
      next: value => {
        this.busy = false;
        void onSuccess(value);
        this.cdr.markForCheck();
      },
      error: response => {
        this.busy = false;
        this.fail(response.error?.message || (response.status === 401
          ? 'Mật khẩu hoặc mã xác thực không đúng.' : 'Không thể lưu thay đổi. Vui lòng thử lại.'));
      }
    });
  }
}
