import { Routes } from '@angular/router';

import { UserRouteAccessService } from 'app/core/auth/user-route-access.service';
import { ASC } from 'app/config/navigation.constants';

const movieRoute: Routes = [
  {
    path: '',
    loadComponent: () => import('./list/movie.component').then(m => m.MovieComponent),
    data: {
      defaultSort: `title,${ASC}`,
    },
    canActivate: [UserRouteAccessService],
  },
];

export default movieRoute;
