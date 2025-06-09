import { Injectable } from '@angular/core';
import {
  Resolve,
  ActivatedRouteSnapshot,
  RouterStateSnapshot,
} from '@angular/router';
import { Observable, of } from 'rxjs';
import { filter, map } from 'rxjs/operators';
import { HttpResponse } from '@angular/common/http';

import { IView, View } from '../view.model';
import { ViewService } from '../service/view.service';

@Injectable({ providedIn: 'root' })
export default class ViewResolve implements Resolve<IView> {
  constructor(private service: ViewService) {}

  resolve(
    route: ActivatedRouteSnapshot,
    state: RouterStateSnapshot,
  ): Observable<IView> {
    const id = route.params['id'];
    if (id) {
      return this.service.find(id).pipe(
        filter((response: HttpResponse<IView>) => response.ok),
        map((response: HttpResponse<IView>) => response.body as IView),
      );
    }
    return of(new View());
  }
}
