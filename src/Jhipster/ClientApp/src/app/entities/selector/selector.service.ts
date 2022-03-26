import { Injectable } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';

import { SERVER_API_URL } from 'app/app.constants';
import { createRequestOption } from 'app/shared/util/request-util';
import { ISelector } from 'app/shared/model/selector.model';

type EntityResponseType = HttpResponse<ISelector>;
type EntityArrayResponseType = HttpResponse<ISelector[]>;

@Injectable({ providedIn: 'root' })
export class SelectorService {
  public resourceUrl = SERVER_API_URL + 'api/selectors';

  constructor(protected http: HttpClient) {}

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
