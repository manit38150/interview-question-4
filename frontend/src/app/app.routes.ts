import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'person-form' },
  {
    path: 'person-form',
    loadComponent: () => import('./person-form/person-form').then((m) => m.PersonForm),
  },
  { path: 'setup', loadComponent: () => import('./setup/setup').then((m) => m.Setup) },
  { path: '**', redirectTo: 'person-form' },
];
