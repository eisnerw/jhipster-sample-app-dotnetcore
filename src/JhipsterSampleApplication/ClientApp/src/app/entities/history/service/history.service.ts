import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { IHistory, NewHistory } from '../history.model';
import { createRequestOption } from 'app/core/request/request-util';

export type EntityResponseType = HttpResponse<IHistory>;
export type EntityArrayResponseType = HttpResponse<IHistory[]>;

@Injectable({ providedIn: 'root' })
export class HistoryService {
  private http = inject(HttpClient);
  private resourceUrl = '/api/Histories';

  query(req?: any): Observable<EntityArrayResponseType> {
    const options = createRequestOption(req);
    return this.http.get<IHistory[]>(this.resourceUrl, { params: options, observe: 'response' });
  }

  create(history: NewHistory): Observable<EntityResponseType> {
    return this.http.post<IHistory>(this.resourceUrl, history, { observe: 'response' });
  }
}
