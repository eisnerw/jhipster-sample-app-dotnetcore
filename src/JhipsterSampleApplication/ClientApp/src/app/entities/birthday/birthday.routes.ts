import { Routes } from '@angular/router';

import { UserRouteAccessService } from 'app/core/auth/user-route-access.service';
import { ASC } from 'app/config/navigation.constants';
import { BirthdayResolve } from './route/birthday-routing-resolve.service';

const birthdayRoute: Routes = [
  {
    path: '',
    loadComponent: () => import('../entity/list/generic-list.component').then(m => m.GenericListComponent),
    data: {
      defaultSort: `id,${ASC}`,
      entity: 'birthday',
      pageTitle: 'Birthdays...'
    },
    canActivate: [UserRouteAccessService],
  },
  {
    path: ':id/view',
    loadComponent: () => import('./detail/birthday-detail.component').then(m => m.BirthdayDetailComponent),
    resolve: {
      birthday: BirthdayResolve,
    },
    canActivate: [UserRouteAccessService],
  },
  {
    path: 'new',
    loadComponent: () => import('./update/birthday-update.component').then(m => m.BirthdayUpdateComponent),
    resolve: {
      birthday: BirthdayResolve,
    },
    canActivate: [UserRouteAccessService],
  },
  {
    path: ':id/edit',
    loadComponent: () => import('./update/birthday-update.component').then(m => m.BirthdayUpdateComponent),
    resolve: {
      birthday: BirthdayResolve,
    },
    canActivate: [UserRouteAccessService],
  },
];

export default birthdayRoute;
