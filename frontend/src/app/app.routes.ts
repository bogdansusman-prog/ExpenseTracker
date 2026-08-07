import { Routes } from '@angular/router';

import { Dashboard } from './pages/dashboard/dashboard';
import { Transactions } from './pages/transactions/transactions';
import { Categories } from './pages/categories/categories';
import { Comparator } from './pages/comparator/comparator';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full'
  },
  {
    path: 'dashboard',
    component: Dashboard
  },
  {
    path: 'transactions',
    component: Transactions
  },
  {
    path: 'categories',
    component: Categories
  },
  {
    path: 'comparator',
    component: Comparator
  },
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];