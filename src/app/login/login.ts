import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class Login {

  username: string = '';
  password: string = '';

  showPassword: boolean = false;
  rememberMe: boolean = false;

  errorMessage: string = '';

  togglePassword(): void {
    this.showPassword = !this.showPassword;
  }

  onLogin(): void {

    this.errorMessage = '';

    // Kiểm tra bỏ trống
    if (!this.username || !this.password) {

      this.errorMessage =
        'Vui lòng nhập đầy đủ tài khoản và mật khẩu.';

      return;
    }

    
    const correctUsername = 'admin';
    const correctPassword = '123456';

    if (
      this.username === correctUsername &&
      this.password === correctPassword
    ) {

      alert('Đăng nhập thành công!');

    } else {

      this.errorMessage =
        'Tài khoản hoặc mật khẩu không chính xác.';

    }
  }
}