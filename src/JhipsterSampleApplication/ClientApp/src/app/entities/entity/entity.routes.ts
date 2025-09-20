import { Routes } from '@angular/router';

import { UserRouteAccessService } from 'app/core/auth/user-route-access.service';
import { ASC } from 'app/config/navigation.constants';

const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'birthday',
  },
  {
    path: ':entity',
    loadComponent: () => import('./list/generic-list.component').then(m => m.GenericListComponent),
    data: {
      defaultSort: `id,${ASC}`,
      pageTitle: 'Entity (Generic)'
    },
    canActivate: [UserRouteAccessService],
  },
];

export default routes;
