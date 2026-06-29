import { CommonModule } from '@angular/common';
import { Component, computed } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/services/auth.service';
import { MenuService } from '../core/services/menu.service';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterOutlet],
  templateUrl: './admin-layout.component.html',
  styleUrl: './admin-layout.component.scss'
})
export class AdminLayoutComponent {
  readonly currentUser = computed(() => this.authService.currentUser());
  readonly menuItems = computed(() => this.menuService.getItems(this.authService.roles()));

  constructor(
    private readonly authService: AuthService,
    private readonly menuService: MenuService
  ) {}

  logout(): void {
    this.authService.logout();
  }
}
