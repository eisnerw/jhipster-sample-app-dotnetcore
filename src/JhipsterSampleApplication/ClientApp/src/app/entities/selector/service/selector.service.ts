import { Injectable } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';

import { ApplicationConfigService } from 'app/core/config/application-config.service';
import { createRequestOption } from 'app/core/request/request-util';
import { ISelector } from '../selector.model';

type EntityResponseType = HttpResponse<ISelector>;
type EntityArrayResponseType = HttpResponse<ISelector[]>;

@Injectable({ providedIn: 'root' })
export class SelectorService {
  protected resourceUrl = this.applicationConfigService.getEndpointFor('api/selectors');

  constructor(
    protected http: HttpClient,
    protected applicationConfigService: ApplicationConfigService,
  ) {}

  create(selector: ISelector): Observable<EntityResponseType> {
    return this.http.post<ISelector>(this.resourceUrl, selector, { observe: 'response' });
  }

  update(selector: ISelector): Observable<EntityResponseType> {
    return this.http.put<ISelector>(this.resourceUrl, selector, { observe: 'response' });
  }

  find(id: number): Observable<EntityResponseType> {
    return this.http.get<ISelector>(`${this.resourceUrl}/${id}`, { observe: 'response' });
  }

  query(req?: any): Observable<EntityArrayResponseType> {
    const options = createRequestOption(req);
    return this.http.get<ISelector[]>(this.resourceUrl, { params: options, observe: 'response' });
  }

  delete(id: number): Observable<HttpResponse<{}>> {
    return this.http.delete(`${this.resourceUrl}/${id}`, { observe: 'response' });
  }
}
