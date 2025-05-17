import { Injectable } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';

import { ApplicationConfigService } from 'app/core/config/application-config.service';
import { createRequestOption } from 'app/core/request/request-util';
import { IView } from '../view.model';

export type EntityResponseType = HttpResponse<IView>;
export type EntityArrayResponseType = HttpResponse<IView[]>;

@Injectable({ providedIn: 'root' })
export class ViewService {
  protected resourceUrl = this.applicationConfigService.getEndpointFor('api/views');

  constructor(
    protected http: HttpClient,
    protected applicationConfigService: ApplicationConfigService,
  ) {}

  create(view: IView): Observable<EntityResponseType> {
    return this.http.post<IView>(this.resourceUrl, view, { observe: 'response' });
  }

  update(view: IView): Observable<EntityResponseType> {
    return this.http.put<IView>(`${this.resourceUrl}/${view.id}`, view, { observe: 'response' });
  }

  find(id: string): Observable<EntityResponseType> {
    return this.http.get<IView>(`${this.resourceUrl}/${id}`, { observe: 'response' });
  }

  query(req?: any): Observable<EntityArrayResponseType> {
    const options = createRequestOption(req);
    return this.http.get<IView[]>(this.resourceUrl, { params: options, observe: 'response' });
  }

  delete(id: string): Observable<HttpResponse<{}>> {
    return this.http.delete(`${this.resourceUrl}/${id}`, { observe: 'response' });
  }

  getViewIdentifier(view: IView): string {
    return view.id!;
  }
}
