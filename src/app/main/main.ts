import { HttpClient } from '@angular/common/http';
import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AdminApiService, DataRow } from '../management/admin-api.service';
import { I18nService } from '../i18n.service';
import { statusLabels } from '../management/resource-config';

interface DashboardSummary {
  totalStudents: number;
  totalRooms: number;
  occupiedBeds: number;
  openRequests: number;
  outstandingBalance: number;
}

@Component({
  selector: 'app-main',
  imports: [RouterLink],
  templateUrl: './main.html',
  styleUrl: './main.css'
})
export class Main implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly api = inject(AdminApiService);
  private readonly cdr = inject(ChangeDetectorRef);
  readonly i18n = inject(I18nService);

  summary?: DashboardSummary;
  rooms: DataRow[] = [];
  requests: DataRow[] = [];
  errorMessage = '';

  ngOnInit(): void {
    forkJoin({
      summary: this.http.get<DashboardSummary>('/api/dashboard/summary'),
      rooms: this.api.list('rooms'),
      requests: this.api.list('maintenance')
    }).subscribe({
      next: ({ summary, rooms, requests }) => {
        this.summary = summary;
        this.rooms = rooms;
        this.requests = requests.filter(row => row['requestStatus'] !== 'RESOLVED').slice(0, 3);
        this.cdr.markForCheck();
      },
      error: () => {
        this.errorMessage = 'Không tải được dữ liệu tổng quan.';
        this.cdr.markForCheck();
      }
    });
  }

  get totalBeds(): number { return this.rooms.reduce((sum, room) => sum + Number(room['totalBeds'] || 0), 0); }
  get occupancyPercent(): number { return this.totalBeds ? Math.round((this.summary?.occupiedBeds || 0) / this.totalBeds * 100) : 0; }
  money(value: number): string { return new Intl.NumberFormat(this.i18n.language() === 'vi' ? 'vi-VN' : this.i18n.language()).format(value) + ' ₫'; }
  roomPercent(room: DataRow): number {
    return room['totalBeds'] ? Math.round(Number(room['occupiedBeds']) / Number(room['totalBeds']) * 100) : 0;
  }
  priority(value: unknown): string { return this.i18n.t(statusLabels[String(value)] || String(value)); }
}
