import { Routes } from '@angular/router';

import { UserRouteAccessService } from 'app/core/auth/user-route-access.service';
import { ASC } from 'app/config/navigation.constants';

const movieRoute: Routes = [
  {
    path: '',
    loadComponent: () => import('../entity/list/generic-list.component').then(m => m.GenericListComponent),
    data: {
      defaultSort: `title,${ASC}`,
      entity: 'movie',
      pageTitle: 'Movies'
    },
    canActivate: [UserRouteAccessService],
  },
];

export default movieRoute;
