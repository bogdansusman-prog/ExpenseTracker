import { Routes } from '@angular/router';

import { authGuard, guestGuard } from './auth/auth.guards';
import { Login } from './pages/login/login';
import { Register } from './pages/register/register';
import { Profile } from './pages/profile/profile';
import { About } from './pages/about/about';

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
    component: Dashboard,
    canActivate: [authGuard]
  },
  {
    path: 'transactions',
    component: Transactions,
    canActivate: [authGuard]
  },
  {
    path: 'categories',
    component: Categories,
    canActivate: [authGuard]
  },
  {
    path: 'comparator',
    component: Comparator,
    canActivate: [authGuard]
  },
  {
    path: 'insights',
    component: Insights,
    canActivate: [authGuard]
  },
  {
    path: 'advisor',
    component: Advisor,
    canActivate: [authGuard]
  },
  {
    path: 'settings',
    component: Settings,
    canActivate: [authGuard]
  },
  {
    path: 'profile',
    component: Profile,
    canActivate: [authGuard]
  },
  {
    path: 'about',
    component: About
  },
  {
    path: 'login',
    component: Login,
    canActivate: [guestGuard]
  },
  {
    path: 'register',
    component: Register,
    canActivate: [guestGuard]
  },
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];