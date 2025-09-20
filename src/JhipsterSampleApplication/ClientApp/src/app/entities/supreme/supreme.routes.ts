import { Routes } from '@angular/router';

import { UserRouteAccessService } from 'app/core/auth/user-route-access.service';
import { ASC } from 'app/config/navigation.constants';

const supremeRoute: Routes = [
  {
    path: '',
    loadComponent: () => import('../entity/list/generic-list.component').then(m => m.GenericListComponent),
    data: {
      defaultSort: `name,${ASC}`,
      entity: 'supreme',
      pageTitle: 'Supreme'
    },
    canActivate: [UserRouteAccessService],
  },
];

export default supremeRoute;
