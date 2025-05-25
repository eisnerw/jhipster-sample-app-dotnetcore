import { inject } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import { ActivatedRouteSnapshot, Router } from '@angular/router';
import { EMPTY, Observable, of } from 'rxjs';
import { mergeMap } from 'rxjs/operators';

import { INamedQuery } from '../named-query.model';
import { NamedQueryService } from '../service/named-query.service';

const namedQueryResolve = (route: ActivatedRouteSnapshot): Observable<null | INamedQuery> => {
  const id = route.params.id;
  if (id) {
    return inject(NamedQueryService)
      .find(id)
      .pipe(
        mergeMap((namedQuery: HttpResponse<INamedQuery>) => {
          if (namedQuery.body) {
            return of(namedQuery.body);
          }
          inject(Router).navigate(['404']);
          return EMPTY;
        }),
      );
  }
  return of(null);
};

export default namedQueryResolve;
