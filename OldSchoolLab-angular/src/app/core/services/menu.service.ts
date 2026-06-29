import { Injectable } from '@angular/core';
import { MenuItem } from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class MenuService {
  private readonly items: MenuItem[] = [
    { label: 'Inicio', route: '/dashboard' },
    { label: 'Registros', route: '/records', roles: ['Admin', 'SuperAdmin', 'Gerencia', 'Gestor', 'Vendedor'] },
    { label: 'Productos', route: '/products', roles: ['Admin', 'SuperAdmin', 'Gerencia'] },
    { label: 'Auditoría', route: '/audit', roles: ['Admin', 'SuperAdmin', 'Gerencia'] },
    { label: 'Chat', route: '/chat', roles: ['Admin', 'SuperAdmin', 'Gerencia', 'Gestor', 'Vendedor'] },
    { label: 'Usuarios', route: '/usuarios', roles: ['Admin', 'SuperAdmin'] }
  ];

  getItems(userRoles: string[]): MenuItem[] {
    return this.items.filter(item => !item.roles?.length || item.roles.some(role => userRoles.includes(role)));
  }
}
