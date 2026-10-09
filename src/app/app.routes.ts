import { Routes } from '@angular/router';
import { Login } from './login/login';
import { Main } from './main/main';
import { AdminLayout } from './admin-layout/admin-layout';
import { ManagementPage } from './management/management-page';
import { authGuard } from './auth.guard';
import { Settings } from './settings/settings';
export const routes: Routes = [
  { path: 'login', component: Login },
  {
    path: '',
    component: AdminLayout,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'main' },
      { path: 'main', component: Main },
      { path: 'settings', component: Settings },
      { path: 'manage/:resource', component: ManagementPage }
    ]
  },
  { path: '**', redirectTo: 'main' }
];
