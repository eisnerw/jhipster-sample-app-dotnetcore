import { Injectable } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import { Resolve, ActivatedRouteSnapshot, Routes, Router } from '@angular/router';
import { Observable, of, EMPTY } from 'rxjs';
import { flatMap } from 'rxjs/operators';

import { Authority } from 'app/shared/constants/authority.constants';
import { UserRouteAccessService } from 'app/core/auth/user-route-access-service';
import { ISelector, Selector } from 'app/shared/model/selector.model';
import { SelectorService } from './selector.service';
import { SelectorComponent } from './selector.component';
import { SelectorDetailComponent } from './selector-detail.component';
import { SelectorUpdateComponent } from './selector-update.component';

@Injectable({ providedIn: 'root' })
export class SelectorResolve implements Resolve<ISelector> {
  constructor(private service: SelectorService, private router: Router) {}

  resolve(route: ActivatedRouteSnapshot): Observable<ISelector> | Observable<never> {
    const id = route.params['id'];
    if (id) {
      return this.service.find(id).pipe(
        flatMap((selector: HttpResponse<Selector>) => {
          if (selector.body) {
            return of(selector.body);
          } else {
            this.router.navigate(['404']);
            return EMPTY;
          }
        })
      );
    }
    return of(new Selector());
  }
}

export const selectorRoute: Routes = [
  {
    path: '',
    component: SelectorComponent,
    data: {
      authorities: [Authority.USER],
      pageTitle: 'jhipsterApp.selector.home.title',
    },
    canActivate: [UserRouteAccessService],
  },
  {
    path: ':id/view',
    component: SelectorDetailComponent,
    resolve: {
      selector: SelectorResolve,
    },
    data: {
      authorities: [Authority.USER],
      pageTitle: 'jhipsterApp.selector.home.title',
    },
    canActivate: [UserRouteAccessService],
  },
  {
    path: 'new',
    component: SelectorUpdateComponent,
    resolve: {
      selector: SelectorResolve,
    },
    data: {
      authorities: [Authority.USER],
      pageTitle: 'jhipsterApp.selector.home.title',
    },
    canActivate: [UserRouteAccessService],
  },
  {
    path: ':id/edit',
    component: SelectorUpdateComponent,
    resolve: {
      selector: SelectorResolve,
    },
    data: {
      authorities: [Authority.USER],
      pageTitle: 'jhipsterApp.selector.home.title',
    },
    canActivate: [UserRouteAccessService],
  },
];
