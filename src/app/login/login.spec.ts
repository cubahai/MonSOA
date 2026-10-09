import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { vi } from 'vitest';
import { Login } from './login';

describe('Login', () => {
  let component: Login;
  let fixture: ComponentFixture<Login>;
  let http: HttpTestingController;
  const router = { navigateByUrl: vi.fn() };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: Router, useValue: router }]
    }).compileComponents();

    fixture = TestBed.createComponent(Login);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    router.navigateByUrl.mockReset();
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('moves to main after successful login', () => {
    component.username = 'admin';
    component.password = 'Admin@123456';
    component.onLogin();

    const request = http.expectOne('/api/auth/login');
    expect(request.request.method).toBe('POST');
    request.flush({ requiresTwoFactor: false, user: { username: 'admin', fullName: 'Quản trị viên Demo', role: 'ADMIN' } });
    expect(router.navigateByUrl).toHaveBeenCalledWith('/main');
  });

  it('requires a verification code before opening main when 2FA is enabled', () => {
    component.username = 'admin';
    component.password = 'Admin@123456';
    component.onLogin();
    http.expectOne('/api/auth/login').flush({ requiresTwoFactor: true, user: null });
    expect(component.awaitingCode).toBe(true);
    expect(router.navigateByUrl).not.toHaveBeenCalled();

    component.code = '123456';
    component.verifyCode();
    http.expectOne('/api/auth/2fa/verify').flush({ requiresTwoFactor: false, user: { username: 'admin', fullName: 'Quản trị viên Demo', role: 'ADMIN' } });
    expect(router.navigateByUrl).toHaveBeenCalledWith('/main');
  });

  it('shows an error for incorrect credentials', () => {
    component.username = 'admin';
    component.password = 'wrong';
    component.onLogin();

    http.expectOne('/api/auth/login').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(component.errorMessage).toContain('không chính xác');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });
});
