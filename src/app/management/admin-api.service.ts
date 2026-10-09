import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export type DataRow = Record<string, any> & { id: number };

@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly http = inject(HttpClient);

  list(resource: string) {
    return this.http.get<DataRow[]>(`/api/admin/${resource}`);
  }

  save(resource: string, model: Record<string, any>, id?: number) {
    return id === undefined
      ? this.http.post<{ id: number }>(`/api/admin/${resource}`, model)
      : this.http.put<{ id: number }>(`/api/admin/${resource}/${id}`, model);
  }

  delete(resource: string, id: number) {
    return this.http.delete<void>(`/api/admin/${resource}/${id}`);
  }
}
