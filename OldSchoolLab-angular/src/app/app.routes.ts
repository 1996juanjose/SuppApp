import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { AdminLayoutComponent } from './layouts/admin-layout.component';
import { LoginPageComponent } from './features/auth/pages/login-page.component';
import { DashboardPageComponent } from './features/dashboard/pages/dashboard-page.component';
import { RecordsPageComponent } from './features/records/pages/records-page.component';
import { RecordsEditPageComponent } from './features/records/pages/records-edit-page.component';
import { ProductsPageComponent } from './features/products/pages/products-page.component';
import { AuditPageComponent } from './features/audit/pages/audit-page.component';

export const routes: Routes = [
  {
    path: 'login',
    component: LoginPageComponent
  },
  {
    path: '',
    component: AdminLayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'dashboard'
      },
      {
        path: 'dashboard',
        component: DashboardPageComponent
      },
      {
        path: 'records',
        component: RecordsPageComponent
      },
      {
        path: 'records/:id/edit',
        component: RecordsEditPageComponent
      },
      {
        path: 'products',
        component: ProductsPageComponent
      },
      {
        path: 'audit',
        component: AuditPageComponent
      },
      {
        path: 'chat',
        canActivate: [roleGuard],
        data: { roles: ['Admin', 'SuperAdmin', 'Gerencia', 'Gestor', 'Vendedor'] },
        loadComponent: () => import('./features/chat/pages/chat-shell.component').then(m => m.ChatShellComponent)
      },
      {
        path: 'usuarios',
        canActivate: [roleGuard],
        data: { roles: ['Admin', 'SuperAdmin'] },
        loadComponent: () => import('./features/usuarios/pages/usuarios-shell.component').then(m => m.UsuariosShellComponent)
      }
    ]
  },
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];
