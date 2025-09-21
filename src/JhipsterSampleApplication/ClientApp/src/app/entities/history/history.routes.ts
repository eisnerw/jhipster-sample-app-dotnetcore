import { Routes } from '@angular/router';
import { UserRouteAccessService } from 'app/core/auth/user-route-access.service';

const historyRoute: Routes = [
  {
    path: '',
    loadComponent: () => import('./list/history.component').then(m => m.HistoryComponent),
    canActivate: [UserRouteAccessService],
    data: { authorities: ['ROLE_ADMIN'] },
  },
];

export default historyRoute;
