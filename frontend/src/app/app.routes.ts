import { Routes } from '@angular/router';

import { Dashboard } from './pages/dashboard/dashboard';
import { Transactions } from './pages/transactions/transactions';
import { Categories } from './pages/categories/categories';
import { Comparator } from './pages/comparator/comparator';
import { Insights } from './pages/insights/insights';
import { Advisor } from './pages/advisor/advisor';
import { Settings } from './pages/settings/settings';

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
    path: 'insights',
    component: Insights
  },
  {
    path: 'advisor',
    component: Advisor
  },
  {
    path: 'settings',
    component: Settings
  },
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];