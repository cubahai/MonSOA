import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../auth.service';
import { I18nService } from '../i18n.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly cdr = inject(ChangeDetectorRef);
  readonly i18n = inject(I18nService);

  username: string = '';
  password: string = '';

  showPassword: boolean = false;
  rememberMe: boolean = false;

  errorMessage: string = '';
  loading = false;
  awaitingCode = false;
  code = '';

  togglePassword(): void {
    this.showPassword = !this.showPassword;
  }

  onLogin(): void {
    this.errorMessage = '';
    if (!this.username.trim() || !this.password) {
      this.errorMessage = 'Vui lòng nhập đầy đủ tài khoản và mật khẩu.';
      return;
    }

    this.loading = true;
    this.auth.login(this.username.trim(), this.password, this.rememberMe).subscribe({
      next: result => {
        this.loading = false;
        if (result.requiresTwoFactor) {
          this.awaitingCode = true;
          this.password = '';
        } else {
          void this.router.navigateByUrl('/main');
        }
        this.cdr.markForCheck();
      },
      error: (error) => {
        this.loading = false;
        this.errorMessage = error.status === 429 ? 'Quá nhiều lần thử. Vui lòng chờ vài phút.' : error.status === 401
          ? 'Tài khoản hoặc mật khẩu không chính xác.'
          : 'Không thể kết nối máy chủ. Vui lòng thử lại.';
        this.cdr.markForCheck();
      }
    });
  }

  verifyCode(): void {
    if (!this.code.trim()) return;
    this.loading = true;
    this.errorMessage = '';
    this.auth.verifyTwoFactor(this.code.trim()).subscribe({
      next: () => { this.loading = false; void this.router.navigateByUrl('/main'); this.cdr.markForCheck(); },
      error: error => {
        this.loading = false;
        this.errorMessage = error.status === 429 ? 'Quá nhiều lần thử. Vui lòng chờ vài phút.'
          : 'Mã xác thực không đúng hoặc đã hết hạn.';
        this.cdr.markForCheck();
      }
    });
  }

  backToLogin(): void { this.awaitingCode = false; this.code = ''; this.errorMessage = ''; }
}
