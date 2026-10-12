import { ApplicationConfig } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withViewTransitions } from '@angular/router';

import { routes } from './app.routes';
import { authInterceptor } from './auth/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideHttpClient(withInterceptors([authInterceptor])),
    // Pages cross-fade cinematically (styles.scss, ::view-transition); browsers without support just switch.
    provideRouter(routes, withViewTransitions({ skipInitialTransition: true }))
  ]
};