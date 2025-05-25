import { Routes } from '@angular/router';

import { UserRouteAccessService } from 'app/core/auth/user-route-access.service';
import { NamedQueryComponent } from './list/named-query.component';
import { NamedQueryDetailComponent } from './detail/named-query-detail.component';
import { NamedQueryUpdateComponent } from './update/named-query-update.component';
import namedQueryResolve from './route/named-query-routing-resolve.service';

export const NAMED_QUERY_ROUTE: Routes = [
  {
    path: '',
    component: NamedQueryComponent,
    data: {
      defaultSort: 'id,asc',
    },
    canActivate: [UserRouteAccessService],
  },
  {
    path: ':id/view',
    component: NamedQueryDetailComponent,
    resolve: {
      namedQuery: namedQueryResolve,
    },
    canActivate: [UserRouteAccessService],
  },
  {
    path: 'new',
    component: NamedQueryUpdateComponent,
    resolve: {
      namedQuery: namedQueryResolve,
    },
    canActivate: [UserRouteAccessService],
  },
  {
    path: ':id/edit',
    component: NamedQueryUpdateComponent,
    resolve: {
      namedQuery: namedQueryResolve,
    },
    canActivate: [UserRouteAccessService],
  },
];
