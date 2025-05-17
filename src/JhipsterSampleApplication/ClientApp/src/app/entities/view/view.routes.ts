import { Routes } from '@angular/router';

import { UserRouteAccessService } from 'app/core/auth/user-route-access.service';
import { ViewComponent } from './list/view.component';
import { ViewDetailComponent } from './detail/view-detail.component';
import { ViewUpdateComponent } from './update/view-update.component';
import { ViewResolve } from './view-routing-resolve.service';

export const VIEW_ROUTE: Routes = [
  {
    path: '',
    component: ViewComponent,
    data: {
      defaultSort: 'id,asc',
    },
    canActivate: [UserRouteAccessService],
  },
  {
    path: ':id/view',
    component: ViewDetailComponent,
    resolve: {
      view: ViewResolve,
    },
    canActivate: [UserRouteAccessService],
  },
  {
    path: 'new',
    component: ViewUpdateComponent,
    resolve: {
      view: ViewResolve,
    },
    canActivate: [UserRouteAccessService],
  },
  {
    path: ':id/edit',
    component: ViewUpdateComponent,
    resolve: {
      view: ViewResolve,
    },
    canActivate: [UserRouteAccessService],
  },
];
