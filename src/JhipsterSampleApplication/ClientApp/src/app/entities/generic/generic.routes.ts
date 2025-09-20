import { Routes } from '@angular/router';

import { UserRouteAccessService } from 'app/core/auth/user-route-access.service';
import { ASC } from 'app/config/navigation.constants';

const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('../shared/generic-list/generic-list.component').then(m => m.GenericListComponent),
    data: {
      defaultSort: `id,${ASC}`,
      entity: 'birthday',
      pageTitle: 'Entity (Generic)'
    },
    canActivate: [UserRouteAccessService],
  },
];

export default routes;

