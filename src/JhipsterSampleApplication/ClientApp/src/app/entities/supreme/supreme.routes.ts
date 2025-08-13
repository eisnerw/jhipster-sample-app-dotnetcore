import { Routes } from '@angular/router';

import { UserRouteAccessService } from 'app/core/auth/user-route-access.service';
import { ASC } from 'app/config/navigation.constants';

const supremeRoute: Routes = [
  {
    path: '',
    loadComponent: () => import('./list/supreme.component').then(m => m.SupremeComponent),
    data: {
      defaultSort: `name,${ASC}`,
    },
    canActivate: [UserRouteAccessService],
  },
];

export default supremeRoute;