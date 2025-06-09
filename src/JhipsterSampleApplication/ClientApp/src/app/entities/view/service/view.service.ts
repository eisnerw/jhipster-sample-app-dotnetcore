import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { IView } from '../view.model';
import { ApplicationConfigService } from 'app/core/config/application-config.service';

export type EntityResponseType = HttpResponse<IView>;
export type EntityArrayResponseType = HttpResponse<IView[]>;

@Injectable({ providedIn: 'root' })
export class ViewService {
  protected readonly http = inject(HttpClient);
  protected readonly applicationConfigService = inject(
    ApplicationConfigService,
  );

  protected resourceUrl =
    this.applicationConfigService.getEndpointFor('api/views');

  create(view: IView): Observable<HttpResponse<IView>> {
    return this.http.post<IView>(this.resourceUrl, view, {
      observe: 'response',
    });
  }

  update(view: IView): Observable<HttpResponse<IView>> {
    return this.http.put<IView>(
      `${this.resourceUrl}/${encodeURIComponent(view.id!)}`,
      view,
      { observe: 'response' },
    );
  }

  find(id: string): Observable<HttpResponse<IView>> {
    return this.http.get<IView>(
      `${this.resourceUrl}/${encodeURIComponent(id)}`,
      { observe: 'response' },
    );
  }

  query(): Observable<HttpResponse<IView[]>> {
    return this.http.get<IView[]>(this.resourceUrl, { observe: 'response' });
  }

  delete(id: string): Observable<HttpResponse<{}>> {
    return this.http.delete(`${this.resourceUrl}/${encodeURIComponent(id)}`, {
      observe: 'response',
    });
  }

  getViewIdentifier(view: IView): string {
    return view.id!;
  }
}
