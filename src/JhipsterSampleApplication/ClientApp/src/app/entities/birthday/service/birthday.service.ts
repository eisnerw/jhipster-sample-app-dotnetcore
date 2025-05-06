import { Injectable } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

import { ApplicationConfigService } from 'app/core/config/application-config.service';
import { createRequestOption } from 'app/core/request/request-util';
import { IBirthday } from '../birthday.model';

export type EntityResponseType = HttpResponse<IBirthday>;
export type EntityArrayResponseType = HttpResponse<{ hits: IBirthday[] }>;

@Injectable({ providedIn: 'root' })
export class BirthdayService {
  protected resourceUrl = this.applicationConfigService.getEndpointFor('api/birthdays');
  protected searchUrl = this.applicationConfigService.getEndpointFor('api/birthdays/search/lucene');

  constructor(
    protected http: HttpClient,
    protected applicationConfigService: ApplicationConfigService,
  ) {}

  create(birthday: IBirthday): Observable<EntityResponseType> {
    return this.http.post<IBirthday>(this.resourceUrl, birthday, { observe: 'response' });
  }

  update(birthday: IBirthday): Observable<EntityResponseType> {
    return this.http.put<IBirthday>(`${this.resourceUrl}/${birthday.id}`, birthday, { observe: 'response' });
  }

  find(id: string): Observable<EntityResponseType> {
    return this.http.get<IBirthday>(`${this.resourceUrl}/${id}`, { observe: 'response' });
  }

  query(req?: any): Observable<EntityArrayResponseType> {
    const options = createRequestOption(req);
    const page = req?.page ?? 0;
    const size = req?.size ?? 20;
    return this.http.get<{ hits: IBirthday[] }>(`${this.searchUrl}?query=*&from=${page}&size=${size}`, {
      params: options,
      observe: 'response',
    });
  }

  delete(id: string): Observable<HttpResponse<{}>> {
    return this.http.delete(`${this.resourceUrl}/${id}`, { observe: 'response' });
  }
}
