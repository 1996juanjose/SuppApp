import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { CompanyLookup } from '../../../core/models/auth.models';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './login-page.component.html',
  styleUrl: './login-page.component.scss'
})
export class LoginPageComponent {
  errorMessage = '';
  loading = false;
  loadingCompanies = false;
  companies: CompanyLookup[] = [];
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/dashboard';

  readonly form = this.fb.group({
    username: ['', [Validators.required]],
    password: ['', [Validators.required]]
    ,companyId: [null as number | null, [Validators.required]]
  });

  ngOnInit(): void {
    this.loadingCompanies = true;
    this.authService.getCompanies().subscribe({
      next: companies => {
        this.companies = companies;
        const defaultCompanyId = companies[0]?.id ?? null;
        this.form.patchValue({ companyId: defaultCompanyId });
      },
      error: () => {
        this.errorMessage = 'No fue posible cargar las empresas.';
      },
      complete: () => {
        this.loadingCompanies = false;
      }
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading = true;
    this.errorMessage = '';

    this.authService.login(this.form.getRawValue() as { username: string; password: string; companyId?: number | null }).subscribe({
      next: () => this.router.navigateByUrl(this.returnUrl),
      error: () => {
        this.errorMessage = 'No fue posible iniciar sesión. Verifica tus credenciales o el AuthService.';
        this.loading = false;
      },
      complete: () => {
        this.loading = false;
      }
    });
  }
}
