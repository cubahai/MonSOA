import { ChangeDetectorRef, Component, DestroyRef, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';
import { AdminApiService, DataRow } from './admin-api.service';
import { ColumnConfig, FieldConfig, ResourceConfig, resourceConfigs, statusLabels } from './resource-config';
import { I18nService } from '../i18n.service';

@Component({
  selector: 'app-management-page',
  imports: [FormsModule],
  templateUrl: './management-page.html',
  styleUrl: './management-page.css'
})
export class ManagementPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(AdminApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly cdr = inject(ChangeDetectorRef);
  readonly i18n = inject(I18nService);

  resource = '';
  config?: ResourceConfig;
  rows: DataRow[] = [];
  lookups: Record<string, DataRow[]> = {};
  search = '';
  loading = false;
  saving = false;
  showDialog = false;
  editingId: number | null = null;
  model: Record<string, any> = {};
  errorMessage = '';
  formError = '';
  successMessage = '';

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      this.resource = params.get('resource') || '';
      this.config = resourceConfigs[this.resource];
      this.search = '';
      this.successMessage = '';
      this.showDialog = false;
      this.load();
      this.cdr.markForCheck();
    });
  }

  get visibleRows(): DataRow[] {
    const term = this.search.trim().toLocaleLowerCase();
    return term ? this.rows.filter(row =>
      Object.values(row).some(value => String(value ?? '').toLocaleLowerCase().includes(term))
    ) : this.rows;
  }

  load(): void {
    if (!this.config) return;
    this.loading = true;
    this.errorMessage = '';
    const lookupNames = [...new Set(this.config.fields.map(field => field.lookup).filter((name): name is string => !!name))];
    forkJoin([this.api.list(this.resource), ...lookupNames.map(name => this.api.list(name))]).subscribe({
      next: results => {
        this.rows = results[0];
        this.lookups = {};
        lookupNames.forEach((name, index) => this.lookups[name] = results[index + 1]);
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Không tải được dữ liệu. Kiểm tra kết nối API và thử lại.';
        this.cdr.markForCheck();
      }
    });
  }

  openCreate(): void {
    if (!this.config) return;
    this.editingId = null;
    this.model = {};
    this.formError = '';
    for (const field of this.config.fields) {
      this.model[field.key] = field.defaultValue ?? '';
    }
    const today = this.localDate();
    if (this.resource === 'stays') this.model['startDate'] = today;
    if (this.resource === 'invoices') {
      this.model['billingMonth'] = `${today.slice(0, 7)}-01`;
      this.model['dueDate'] = `${today.slice(0, 7)}-10`;
    }
    if (this.resource === 'payments') this.model['paidAt'] = this.localDateTime();
    this.showDialog = true;
  }

  openEdit(row: DataRow): void {
    if (!this.config) return;
    this.editingId = row.id;
    this.formError = '';
    this.model = {};
    for (const field of this.config.fields) {
      const value = row[field.key];
      this.model[field.key] = value == null ? '' : field.type === 'date' ? String(value).slice(0, 10)
        : field.type === 'datetime' ? String(value).slice(0, 16) : value;
    }
    this.showDialog = true;
  }

  closeDialog(): void { this.showDialog = false; }

  save(): void {
    if (!this.config || this.saving) return;
    this.formError = '';
    if (this.resource === 'stays' && this.model['stayStatus'] === 'ENDED' && !this.model['endDate'])
      this.model['endDate'] = this.localDate();
    if (this.resource === 'maintenance') {
      if (this.model['requestStatus'] === 'RESOLVED' && !this.model['resolvedAt'])
        this.model['resolvedAt'] = this.localDateTime();
      if (this.model['requestStatus'] !== 'RESOLVED') this.model['resolvedAt'] = null;
    }
    this.saving = true;
    this.api.save(this.resource, this.model, this.editingId ?? undefined).subscribe({
      next: () => {
        this.saving = false;
        this.showDialog = false;
        this.successMessage = `${this.i18n.t(this.editingId === null ? 'Đã thêm' : 'Đã cập nhật')} ${this.i18n.t(this.config!.singular)}.`;
        this.load();
        this.cdr.markForCheck();
      },
      error: error => {
        this.saving = false;
        this.formError = error.error?.message || this.i18n.t('Không lưu được dữ liệu. Vui lòng kiểm tra và thử lại.');
        this.cdr.markForCheck();
      }
    });
  }

  delete(row: DataRow): void {
    if (!this.config || !window.confirm(`${this.i18n.t('Xóa')} ${this.i18n.t(this.config.singular)}? ${this.i18n.t('Thao tác này không thể hoàn tác.')}`)) return;
    this.errorMessage = '';
    this.api.delete(this.resource, row.id).subscribe({
      next: () => { this.successMessage = `${this.i18n.t('Đã xóa')} ${this.i18n.t(this.config!.singular)}.`; this.load(); this.cdr.markForCheck(); },
      error: error => { this.errorMessage = error.error?.message || this.i18n.t('Không thể xóa mục này.'); this.cdr.markForCheck(); }
    });
  }

  lookupOptions(field: FieldConfig): DataRow[] {
    const rows = this.lookups[field.lookup || ''] || [];
    if (this.resource === 'stays' && field.lookup === 'beds')
      return rows.filter(row => (!row['occupantName'] && row['bedStatus'] === 'AVAILABLE') || row.id === this.model[field.key]);
    if (this.resource === 'payments' && field.lookup === 'invoices')
      return rows.filter(row => Number(row['balanceDue']) > 0 || row.id === this.model[field.key]);
    return rows;
  }

  optionLabel(resource: string, row: DataRow): string {
    switch (resource) {
      case 'buildings': return `${row['buildingCode']} — ${row['buildingName']}`;
      case 'rooms': case 'beds': return row['label'];
      case 'students': return `${row['studentCode']} — ${row['fullName']}`;
      case 'stays': return `${row['studentCode']} — ${row['studentName']} (${row['bedLabel']})`;
      case 'invoices': return `${row['studentCode']} — ${String(row['billingMonth']).slice(0, 7)} (${this.money(row['balanceDue'])} ${this.i18n.t('còn lại')})`;
      default: return String(row.id);
    }
  }

  display(row: DataRow, column: ColumnConfig): string {
    const value = row[column.key];
    if (value === null || value === undefined || value === '') return '—';
    if (column.format === 'money') return this.money(value);
    if (column.format === 'status') return this.i18n.t(statusLabels[String(value)] || String(value));
    if (column.format === 'date' || column.format === 'datetime') {
      const date = new Date(value);
      return Number.isNaN(date.getTime()) ? String(value)
        : new Intl.DateTimeFormat(this.i18n.language() === 'vi' ? 'vi-VN' : this.i18n.language(), column.format === 'datetime'
          ? { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }
          : { day: '2-digit', month: '2-digit', year: 'numeric' }).format(date);
    }
    return String(value);
  }

  badgeClass(value: unknown): string {
    const text = String(value || '').toLowerCase();
    return ['active', 'available', 'paid', 'resolved'].includes(text) ? 'positive'
      : ['inactive', 'closed', 'cancelled', 'unpaid', 'high'].includes(text) ? 'negative'
      : 'neutral';
  }

  private money(value: unknown): string { return `${new Intl.NumberFormat(this.i18n.language() === 'vi' ? 'vi-VN' : this.i18n.language()).format(Number(value))} ₫`; }
  private localDate(): string {
    const now = new Date();
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
  }
  private localDateTime(): string {
    const now = new Date();
    return `${this.localDate()}T${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;
  }
}
