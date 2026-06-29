import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { AuthService } from '../../../core/services/auth.service';
import { RecordsService } from '../services/records.service';
import { RecordDetail, RecordProductsOption, RecordStatusOption } from '../models/records.models';

@Component({
  selector: 'app-records-edit-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule
  ],
  templateUrl: './records-edit-page.component.html',
  styleUrl: './records-edit-page.component.scss'
})
export class RecordsEditPageComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  private readonly recordsService = inject(RecordsService);
  
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly errorMessage = signal('');
  readonly statusOptions = signal<RecordStatusOption[]>([]);
  readonly productOptions = signal<RecordProductsOption[]>([]);
  readonly record = signal<RecordDetail | null>(null);
  readonly companyId = computed(() => this.authService.currentUser()?.companyId ?? null);
  readonly selectedProduct = computed(() => {

  const id =
    this.form.controls.productCatalogId.value;

  return this.productOptions()
    .find(x => x.id === id);

});
readonly selectedPrice = computed(() => {

  const priceId =
    this.form.controls.selectedPriceId.value;

  return this.selectedProduct()
    ?.prices
    ?.find(x => x.id === priceId);

});

readonly selectedAmount = computed(() => {
  return this.selectedPrice()?.price ?? 0;
});

  readonly form = this.fb.nonNullable.group({
    statusCatalogId: [0, Validators.required],
    recordDate: ['', Validators.required],
    cellphone: ['', Validators.required],
    nameOrReference: [''],
    callActivity: [''],
    callScheduledAt: [''],
    isCallConcrete: [false],
    dni: [''],
    productCatalogId: [0, Validators.required],
    selectedPriceId: [0], 
    quantity: [1, [Validators.required, Validators.min(1)]],
    folderPath: [''],
    destino: [''],
    clave: [''],
    guia: ['']
  });

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isFinite(id) || id <= 0) {
      this.errorMessage.set('Registro inválido.');
      return;
    }

    this.loadRecord(id);
    
    this.form.controls.selectedPriceId.valueChanges
    .subscribe(priceId => {

      const price =
       this.selectedProduct()
      ?.prices
      .find(x => x.id === priceId);

      if (!price) {
      return;
      }

     this.form.patchValue({
    quantity: price.quantity
    });

    });

      this.form.controls.productCatalogId.valueChanges
    .subscribe(() => {

      this.form.patchValue({
        selectedPriceId: 0,
        quantity: 1
      });

    });
  }

  back(): void {
    this.router.navigate(['/records']);
  }

  save(): void {
    const id = this.record()?.id;
    if (!id) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set('');

    const raw = this.form.getRawValue();
    this.recordsService.updateRecord(id, {
      statusCatalogId: raw.statusCatalogId,
      recordDate: raw.recordDate,
      cellphone: raw.cellphone,
      nameOrReference: raw.nameOrReference,
      callActivity: raw.callActivity,
      callScheduledAt: raw.callScheduledAt || null,
      isCallConcrete: raw.isCallConcrete,
      dni: raw.dni,
      productCatalogId: raw.productCatalogId,
      quantity: raw.quantity,
      folderPath: raw.folderPath,
      destino: raw.destino,
      clave: raw.clave,
      guia: raw.guia
    }).subscribe({
      next: updated => {
        this.record.set(updated);
        this.router.navigate(['/records']);
      },
      error: () => this.errorMessage.set('No fue posible guardar los cambios.'),
      complete: () => this.saving.set(false)
    });
  }

  private loadRecord(id: number): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.recordsService.getStatuses(this.companyId()).subscribe({
      next: statuses => this.statusOptions.set(statuses),
      error: () => this.statusOptions.set([])
    });
    
    this.recordsService.getProducts(this.companyId()).subscribe({
      next: products => this.productOptions.set(products),
      error: () => this.productOptions.set([])
    });

    this.recordsService.getRecord(id, this.companyId()).subscribe({
      next: record => {
        this.record.set(record);
        this.form.patchValue({
          statusCatalogId: record.statusCatalogId,
          recordDate: this.toDateValue(record.recordDate),
          cellphone: record.cellphone,
          nameOrReference: record.nameOrReference,
          callActivity: record.callActivity,
          callScheduledAt: this.toDateTimeValue(record.callScheduledAt),
          isCallConcrete: record.isCallConcrete ?? false,
          dni: record.dni,
          productCatalogId: record.productCatalogId ?? 0  ,
          quantity: record.quantity,
          folderPath: record.folderPath,
          destino: record.destino,
          clave: record.clave,
          guia: record.guia
        });
      },
      error: () => this.errorMessage.set('No fue posible cargar el registro.'),
      complete: () => this.loading.set(false)
    });
  }

  private toDateValue(value: string): string {
    return value ? value.slice(0, 10) : '';
  }

  private toDateTimeValue(value?: string | null): string {
    return value ? value.slice(0, 16) : '';
  }
}
