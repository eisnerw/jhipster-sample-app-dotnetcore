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
    children: [
      {
        path: 'new',
        loadComponent: () => import('./edit/generic-edit.component').then(m => m.GenericEditComponent),
        canActivate: [UserRouteAccessService],
      },
      {
        path: ':id/edit',
        loadComponent: () => import('./edit/generic-edit.component').then(m => m.GenericEditComponent),
        canActivate: [UserRouteAccessService],
      },
      {
        path: '',
        loadComponent: () => import('./list/generic-list.component').then(m => m.GenericListComponent),
        data: {
          defaultSort: `id,${ASC}`,
          pageTitle: 'Entity (Generic)',
        },
        canActivate: [UserRouteAccessService],
      },
    ],
  },
];

export default routes;
