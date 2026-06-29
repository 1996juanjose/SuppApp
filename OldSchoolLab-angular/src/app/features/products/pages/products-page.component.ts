import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { AuthService } from '../../../core/services/auth.service';
import { ProductItem } from '../models/products.models';
import { ProductsService } from '../services/products.service';

@Component({
  selector: 'app-products-page',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatCardModule, MatChipsModule, MatProgressBarModule, MatTableModule],
  templateUrl: './products-page.component.html',
  styleUrl: './products-page.component.scss'
})
export class ProductsPageComponent implements OnInit {
  private readonly productsService = inject(ProductsService);
  private readonly authService = inject(AuthService);

  readonly loading = signal(false);
  readonly errorMessage = signal('');
  readonly products = signal<ProductItem[]>([]);
  readonly displayedColumns = ['name', 'cost', 'prices', 'status'];
  readonly productCount = computed(() => this.products().length);
  readonly activeCount = computed(() => this.products().filter(product => product.isActive).length);
  readonly inactiveCount = computed(() => this.products().filter(product => !product.isActive).length);
  readonly totalCatalogValue = computed(() => this.products().reduce((sum, product) => sum + (product.purchaseUnitCost ?? 0), 0));

  ngOnInit(): void {
    this.loadProducts();
  }

  refresh(): void {
    this.loadProducts();
  }

  trackByProductId(_: number, item: ProductItem): number {
    return item.id;
  }

  formatPrices(product: ProductItem): string {
    if (!product.prices?.length) {
      return 'Sin precios';
    }

    return product.prices
      .map(price => `${price.quantity}u: S/ ${price.price.toFixed(2)}`)
      .join(' | ');
  }

  getPriceSummary(product: ProductItem): string {
    if (!product.prices?.length) {
      return 'Sin escalas';
    }

    const prices = [...product.prices].sort((left, right) => left.quantity - right.quantity);
    const first = prices[0];
    const last = prices[prices.length - 1];
    return `${prices.length} escalas · Desde ${first.quantity}u S/ ${first.price.toFixed(2)} · Hasta ${last.quantity}u S/ ${last.price.toFixed(2)}`;
  }

  private loadProducts(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    const companyId = this.authService.currentUser()?.companyId ?? null;

    this.productsService.getProducts(companyId).subscribe({
      next: products => this.products.set(products),
      error: () => this.errorMessage.set('No fue posible cargar los productos.'),
      complete: () => this.loading.set(false)
    });
  }
}
