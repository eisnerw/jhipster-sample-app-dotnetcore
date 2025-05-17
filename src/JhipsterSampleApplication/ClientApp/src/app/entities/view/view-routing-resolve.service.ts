import { Injectable } from '@angular/core';
import { Resolve, ActivatedRouteSnapshot, RouterStateSnapshot, Routes } from '@angular/router';
import { Observable, of } from 'rxjs';
import { filter, map } from 'rxjs/operators';
import { HttpResponse } from '@angular/common/http';
import { IView } from './view.model';
import { ViewService } from './service/view.service';

@Injectable({ providedIn: 'root' })
export class ViewResolve implements Resolve<IView> {
  constructor(private service: ViewService) {}

  resolve(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): Observable<IView> {
    const id = route.params['id'];
    if (id) {
      return this.service.find(id).pipe(
        filter((response: HttpResponse<IView>) => response.ok),
        map((view: HttpResponse<IView>) => {
          const resolvedView = view.body!;
          if (!resolvedView.domain) {
            resolvedView.domain = 'birthdays';
          }
          return resolvedView;
        }),
      );
    }
    return of({ name: '', field: '', aggregation: '', query: '', categoryQuery: '', script: '', domain: 'birthdays' });
  }
}
