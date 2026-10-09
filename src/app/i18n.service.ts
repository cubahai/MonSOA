import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';

export type Language = 'vi' | 'en' | 'zh';
export type Theme = 'light' | 'dark' | 'system';

@Injectable({ providedIn: 'root' })
export class I18nService {
  private readonly http = inject(HttpClient);
  private readonly catalog = signal<Record<string, string>>({});
  readonly language = signal<Language>(this.savedLanguage());
  readonly theme = signal<Theme>(this.savedTheme());

  constructor() {
    this.load(this.language());
    this.applyTheme(this.theme());
  }

  t(key: string): string {
    return this.catalog()[key] || key;
  }

  setLanguage(language: Language): void {
    this.language.set(language);
    localStorage.setItem('ktx-language', language);
    document.documentElement.lang = language === 'zh' ? 'zh-CN' : language;
    this.load(language);
  }

  setTheme(theme: Theme): void {
    this.theme.set(theme);
    localStorage.setItem('ktx-theme', theme);
    this.applyTheme(theme);
  }

  private load(language: Language): void {
    if (language === 'vi') {
      this.catalog.set({});
      return;
    }
    this.http.get<Record<string, string>>(`/api/i18n/${language}`).subscribe({
      next: catalog => { if (this.language() === language) this.catalog.set(catalog); },
      error: () => { if (this.language() === language) this.catalog.set({}); }
    });
  }

  private applyTheme(theme: Theme): void {
    document.documentElement.dataset['theme'] = theme;
  }

  private savedLanguage(): Language {
    const value = localStorage.getItem('ktx-language');
    return value === 'en' || value === 'zh' ? value : 'vi';
  }

  private savedTheme(): Theme {
    const value = localStorage.getItem('ktx-theme');
    return value === 'dark' || value === 'system' ? value : 'light';
  }
}
