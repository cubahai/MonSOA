import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService, DemoUser } from '../auth.service';
import { I18nService } from '../i18n.service';
import { SettingsService } from '../settings/settings.service';

@Component({
  selector: 'app-admin-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './admin-layout.html',
  styleUrl: './admin-layout.css'
})
export class AdminLayout implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly settings = inject(SettingsService);
  readonly i18n = inject(I18nService);
  user?: DemoUser;
  message = '';

  readonly navigation = [
    { label: 'Tổng quan', icon: '▦', link: '/main' },
    { label: 'Tòa nhà', icon: '▤', link: '/manage/buildings' },
    { label: 'Phòng', icon: '▥', link: '/manage/rooms' },
    { label: 'Giường', icon: '▧', link: '/manage/beds' },
    { label: 'Sinh viên', icon: '♙', link: '/manage/students' },
    { label: 'Lượt ở', icon: '⌂', link: '/manage/stays' },
    { label: 'Hóa đơn', icon: '▤', link: '/manage/invoices' },
    { label: 'Thanh toán', icon: '◈', link: '/manage/payments' },
    { label: 'Bảo trì', icon: '⚒', link: '/manage/maintenance' },
    { label: 'Cài đặt', icon: '⚙', link: '/settings' }
  ];

  ngOnInit(): void {
    this.auth.currentUser().subscribe({ next: user => { this.user = user; this.cdr.markForCheck(); } });
    this.settings.get().subscribe({ next: settings => {
      this.i18n.setLanguage(settings.language);
      this.i18n.setTheme(settings.theme);
      this.cdr.markForCheck();
    } });
  }

  logout(): void {
    this.auth.logout().subscribe({
      next: () => void this.router.navigateByUrl('/login'),
      error: () => { this.message = 'Không thể đăng xuất. Vui lòng thử lại.'; this.cdr.markForCheck(); }
    });
  }
}
