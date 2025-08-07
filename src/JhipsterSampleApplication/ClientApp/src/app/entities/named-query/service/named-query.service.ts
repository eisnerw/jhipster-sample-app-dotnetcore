import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';

import { isPresent } from 'app/core/util/operators';
import { ApplicationConfigService } from 'app/core/config/application-config.service';
import { createRequestOption } from 'app/core/request/request-util';
import { INamedQuery, NewNamedQuery } from '../named-query.model';

export type EntityResponseType = HttpResponse<INamedQuery>;
export type EntityArrayResponseType = HttpResponse<INamedQuery[]>;

@Injectable({ providedIn: 'root' })
export class NamedQueryService {
  protected http = inject(HttpClient);
  protected applicationConfigService = inject(ApplicationConfigService);

  protected resourceUrl = this.applicationConfigService.getEndpointFor('api/NamedQueries');

  create(namedQuery: NewNamedQuery): Observable<EntityResponseType> {
    return this.http.post<INamedQuery>(this.resourceUrl, namedQuery, {
      observe: 'response',
    });
  }

  update(namedQuery: INamedQuery): Observable<EntityResponseType> {
    return this.http.put<INamedQuery>(`${this.resourceUrl}/${this.getNamedQueryIdentifier(namedQuery)}`, namedQuery, {
      observe: 'response',
    });
  }

  partialUpdate(namedQuery: INamedQuery): Observable<EntityResponseType> {
    return this.http.patch<INamedQuery>(`${this.resourceUrl}/${this.getNamedQueryIdentifier(namedQuery)}`, namedQuery, {
      observe: 'response',
    });
  }

  find(id: number): Observable<EntityResponseType> {
    return this.http.get<INamedQuery>(`${this.resourceUrl}/${id}`, {
      observe: 'response',
    });
  }

  query(req?: { name?: string; owner?: string }): Observable<EntityArrayResponseType> {
    const options = createRequestOption(req);
    return this.http.get<INamedQuery[]>(this.resourceUrl, {
      params: options,
      observe: 'response',
    });
  }

  delete(id: number): Observable<HttpResponse<{}>> {
    return this.http.delete(`${this.resourceUrl}/${id}`, {
      observe: 'response',
    });
  }

  getNamedQueryIdentifier(namedQuery: Pick<INamedQuery, 'id'>): number {
    return namedQuery.id;
  }

  compareNamedQuery(o1: Pick<INamedQuery, 'id'> | null, o2: Pick<INamedQuery, 'id'> | null): boolean {
    return o1 === o2;
  }

  addNamedQueryToCollectionIfMissing<T extends Pick<INamedQuery, 'id'>>(
    namedQueryCollection: T[],
    ...namedQueriesToCheck: (T | null | undefined)[]
  ): T[] {
    const namedQueries: T[] = namedQueriesToCheck.filter(isPresent);
    if (namedQueries.length > 0) {
      const namedQueryCollectionIdentifiers = namedQueryCollection.map(namedQueryItem => this.getNamedQueryIdentifier(namedQueryItem));
      const namedQueriesToAdd = namedQueries.filter(namedQueryItem => {
        const namedQueryIdentifier = this.getNamedQueryIdentifier(namedQueryItem);
        if (namedQueryCollectionIdentifiers.includes(namedQueryIdentifier)) {
          return false;
        }
        namedQueryCollectionIdentifiers.push(namedQueryIdentifier);
        return true;
      });
      return [...namedQueriesToAdd, ...namedQueryCollection];
    }
    return namedQueryCollection;
  }
}
