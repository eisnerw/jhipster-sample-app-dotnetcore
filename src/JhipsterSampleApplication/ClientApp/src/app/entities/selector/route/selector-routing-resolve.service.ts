import { inject } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import { ActivatedRouteSnapshot, Router } from '@angular/router';
import { EMPTY, Observable, of } from 'rxjs';
import { mergeMap } from 'rxjs/operators';

import { ISelector } from '../selector.model';
import { SelectorService } from '../service/selector.service';

const selectorResolve = (route: ActivatedRouteSnapshot): Observable<null | ISelector> => {
  const id = route.params.id;
  if (id) {
    return inject(SelectorService)
      .find(id)
      .pipe(
        mergeMap((selector: HttpResponse<ISelector>) => {
          if (selector.body) {
            return of(selector.body);
          }
          inject(Router).navigate(['404']);
          return EMPTY;
        }),
      );
  }
  return of(null);
};

export default selectorResolve;
