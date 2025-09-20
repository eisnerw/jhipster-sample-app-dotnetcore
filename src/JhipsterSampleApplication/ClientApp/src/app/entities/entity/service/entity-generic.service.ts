import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApplicationConfigService } from 'app/core/config/application-config.service';

export type SearchResponse<T> = HttpResponse<{
  hits: T[];
  hitType: string;
  totalHits: number;
  searchAfter: string[];
  pitId: string | null;
}>;

export type ViewSearchResponse = HttpResponse<{
  hits: any[];
  hitType: string;
  viewName: string;
  viewCategory?: string;
  totalHits: number;
}>;

export interface SimpleApiResponse {
  success: boolean;
  message?: string;
}

export interface CategorizeMultipleRequest {
  rows: string[];
  add: string[];
  remove: string[];
}

@Injectable({ providedIn: 'root' })
export class EntityGenericService {
  private http = inject(HttpClient);
  private applicationConfigService = inject(ApplicationConfigService);

  private url(entity: string): string {
    return this.applicationConfigService.getEndpointFor(`api/entity/${encodeURIComponent(entity)}`);
  }

  private luceneUrl(entity: string): string {
    return this.applicationConfigService.getEndpointFor(`api/entity/${encodeURIComponent(entity)}/search/lucene`);
  }

  private bqlUrl(entity: string): string {
    return this.applicationConfigService.getEndpointFor(`api/entity/${encodeURIComponent(entity)}/search/bql`);
  }

  private qbSpecUrl(entity: string): string {
    return this.applicationConfigService.getEndpointFor(`api/entity/${encodeURIComponent(entity)}/query-builder-spec`);
  }

  private specUrl(entity: string): string {
    return this.applicationConfigService.getEndpointFor(`api/entity/${encodeURIComponent(entity)}/spec`);
  }

  query<T>(entity: string, req?: any): Observable<SearchResponse<T>> {
    const params: any = { ...(req || {}) };
    let queryString = 'query=';
    if (params.query) {
      queryString += String(params.query);
      delete params.query;
    } else {
      queryString += '*';
    }
    return this.http.get<any>(`${this.luceneUrl(entity)}?${queryString}`, { params, observe: 'response' });
  }

  searchWithBql<T>(entity: string, bqlQuery: string, req?: any): Observable<SearchResponse<T>> {
    return this.http.post<any>(this.bqlUrl(entity), bqlQuery, {
      params: req || {},
      headers: { 'Content-Type': 'text/plain' },
      observe: 'response',
    });
  }

  searchView(entity: string, req: any): Observable<ViewSearchResponse> {
    const params: any = { ...(req || {}) };
    let queryString = 'query=';
    if (params.query) {
      queryString += String(params.query);
      delete params.query;
    } else {
      queryString += '*';
    }
    return this.http.get<any>(`${this.luceneUrl(entity)}?${queryString}`, { params, observe: 'response' });
  }

  delete(entity: string, id: string): Observable<HttpResponse<{}>> {
    return this.http.delete(`${this.url(entity)}/${encodeURIComponent(id)}`, { observe: 'response' });
  }

  categorizeMultiple(entity: string, payload: CategorizeMultipleRequest): Observable<HttpResponse<SimpleApiResponse>> {
    return this.http.post<SimpleApiResponse>(`${this.url(entity)}/categorize-multiple`, payload, { observe: 'response' });
  }

  getQueryBuilderSpec(entity: string): Observable<any> {
    return this.http.get<any>(this.qbSpecUrl(entity));
  }

  getEntitySpec(entity: string): Observable<any> {
    return this.http.get<any>(this.specUrl(entity));
  }
}

