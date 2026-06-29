import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { AuthService } from '../../../core/services/auth.service';
import { RecordsService } from '../services/records.service';
import { RecordAlert, RecordAlertResponse, RecordFilter, RecordItem, RecordStatusOption } from '../models/records.models';
import { Subscription, interval } from 'rxjs';

@Component({
  selector: 'app-records-page',
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
    MatSnackBarModule,
    MatProgressBarModule,
    MatTableModule
  ],
  templateUrl: './records-page.component.html',
  styleUrl: './records-page.component.scss'
})
export class RecordsPageComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly recordsService = inject(RecordsService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly refreshSubscription = new Subscription();

  readonly loading = signal(false);
  readonly loadingAlerts = signal(false);
  readonly records = signal<RecordItem[]>([]);
  readonly statuses = signal<RecordStatusOption[]>([]);
  readonly alerts = signal<RecordAlertResponse | null>(null);
  readonly errorMessage = signal('');
  readonly alertMessage = signal('');
  readonly lastUpdatedAt = signal('');
  readonly refreshCountdown = signal(60);
  readonly displayedColumns = ['cellphone', 'nameOrReference', 'status', 'balanceDue', 'actions'];
  readonly companyId = computed(() => this.authService.currentUser()?.companyId ?? null);
  readonly mvcBaseUrl = 'https://localhost:7254';

  readonly totalRecords = computed(() => this.records().length);
  readonly totalPaid = computed(() => this.records().reduce((sum, item) => sum + (item.activePaidAmount ?? item.paidAmount ?? 0), 0));
  readonly totalBalance = computed(() => this.records().reduce((sum, item) => sum + (item.calculatedBalanceDue ?? item.balanceDue ?? 0), 0));
  readonly dueAlerts = computed(() => this.alerts()?.alerts.filter(alert => alert.isDue) ?? []);
  readonly upcomingAlerts = computed(() => this.alerts()?.alerts.filter(alert => !alert.isDue) ?? []);
  readonly dueCount = computed(() => this.dueAlerts().length);
  readonly upcomingCount = computed(() => this.upcomingAlerts().length);
  readonly nextCallScheduledAt = computed(() => this.alerts()?.nextCallScheduledAt ?? null);

  readonly filterForm = this.fb.nonNullable.group({
    search: [''],
    fromDate: [''],
    toDate: [''],
    statusIds: this.fb.nonNullable.control<number[]>([])
  });

  ngOnInit(): void {
    this.loadData(true);
    this.startTimer();
  }

  ngOnDestroy(): void {
    this.refreshSubscription.unsubscribe();
  }

  applyFilters(): void {
    this.loadData();
  }

  clearFilters(): void {
    this.filterForm.reset({ search: '', fromDate: '', toDate: '', statusIds: [] });
    this.loadData();
  }

  toggleStatus(statusId: number, checked: boolean): void {
    const current = this.filterForm.controls.statusIds.value;
    const next = checked
      ? [...current, statusId]
      : current.filter(id => id !== statusId);

    this.filterForm.controls.statusIds.setValue(next);
  }

  trackByRecordId(_: number, item: RecordItem): number {
    return item.id;
  }

  trackByAlertId(_: number, item: RecordAlert): number {
    return item.id;
  }

  editUrl(id: number): string {
    return `/records/${id}/edit`;
  }

  private loadData(onlyToday = false): void {
    const filter = this.filterForm.getRawValue() as RecordFilter;
    filter.companyId = this.companyId();
    filter.statusIds = this.filterForm.controls.statusIds.value;

    if (onlyToday) {
      const today = new Date();
      const value = today.toISOString().slice(0, 10);
      filter.fromDate = value;
      filter.toDate = value;
    }

    this.loading.set(true);
    this.loadingAlerts.set(true);
    this.errorMessage.set('');
    this.alertMessage.set('');

    this.recordsService.getStatuses(this.companyId()).subscribe({
      next: statuses => this.statuses.set(statuses),
      error: () => this.statuses.set([])
    });

    this.recordsService.getRecords(filter).subscribe({
      next: records => this.records.set(records),
      error: () => this.errorMessage.set('No fue posible cargar los registros.'),
      complete: () => this.loading.set(false)
    });

    this.recordsService.getNextCall(this.companyId()).subscribe({
      next: summary => {
        this.alerts.set(summary);
        this.lastUpdatedAt.set(new Date().toLocaleString('es-PE'));
      },
      error: () => this.alerts.set(null),
      complete: () => this.loadingAlerts.set(false)
    });

    this.refreshCountdown.set(60);
  }

  private startTimer(): void {
    this.refreshSubscription.add(
      interval(1000).subscribe(() => {
        const nextValue = this.refreshCountdown() - 1;
        if (nextValue <= 0) {
          this.loadData();
          return;
        }

        this.refreshCountdown.set(nextValue);
      })
    );
  }
}
