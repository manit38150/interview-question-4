import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HealthApiService } from '../core/health-api.service';

@Component({
  selector: 'app-setup',
  standalone: true,
  templateUrl: './setup.html',
  styleUrl: './setup.scss',
})
export class Setup {
  protected readonly apiStatus = signal('Checking API…');

  constructor() {
    inject(HealthApiService)
      .getHealth()
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: () => this.apiStatus.set('API is healthy.'),
        error: () =>
          this.apiStatus.set('API is unavailable. Start the backend and reload this page.'),
      });
  }
}
