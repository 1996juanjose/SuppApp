import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { AuthService } from '../../../core/services/auth.service';
import { AuditItem } from '../models/audit.models';
import { AuditService } from '../services/audit.service';

@Component({
  selector: 'app-audit-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatTableModule
  ],
  templateUrl: './audit-page.component.html',
  styleUrl: './audit-page.component.scss'
})
export class AuditPageComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly auditService = inject(AuditService);
  private readonly authService = inject(AuthService);

  readonly loading = signal(false);
  readonly errorMessage = signal('');
  readonly auditLogs = signal<AuditItem[]>([]);
  readonly displayedColumns = ['tableName', 'recordId', 'action', 'changedByUserName', 'changedAt'];

  readonly totalLogs = computed(() => this.auditLogs().length);

  readonly filterForm = this.fb.nonNullable.group({
    tableName: [''],
    userName: ['']
  });

  ngOnInit(): void {
    this.loadLogs();
  }

  applyFilters(): void {
    this.loadLogs();
  }

  clearFilters(): void {
    this.filterForm.reset({ tableName: '', userName: '' });
    this.loadLogs();
  }

  trackByLogId(_: number, item: AuditItem): number {
    return item.id;
  }

  private loadLogs(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    const companyId = this.authService.currentUser()?.companyId ?? null;
    const filters = this.filterForm.getRawValue();

    this.auditService.getAudit({
      companyId,
      tableName: filters.tableName || null,
      userName: filters.userName || null
    }).subscribe({
      next: logs => this.auditLogs.set(logs),
      error: () => this.errorMessage.set('No fue posible cargar la auditoría.'),
      complete: () => this.loading.set(false)
    });
  }
}
