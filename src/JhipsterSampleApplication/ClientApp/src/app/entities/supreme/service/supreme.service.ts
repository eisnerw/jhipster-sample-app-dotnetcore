import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';

import { ApplicationConfigService } from 'app/core/config/application-config.service';
import { createRequestOption } from 'app/core/request/request-util';
import { ISupreme, IViewResult } from '../supreme.model';

export type EntityArrayResponseType = HttpResponse<{
  hits: ISupreme[] | any[];
  hitType: string;
  totalHits: number;
  searchAfter: string[];
  pitId: string | null;
}>;

export type ViewArrayResponseType = HttpResponse<{
  hits: IViewResult[];
  hitType: string;
  viewName: string;
  viewCategory?: string;
  totalHits: number;
}>;

@Injectable({ providedIn: 'root' })
export class SupremeService {
  protected http = inject(HttpClient);
  protected applicationConfigService = inject(ApplicationConfigService);

  protected resourceUrl = this.applicationConfigService.getEndpointFor('api/supreme');
  protected searchUrl = this.applicationConfigService.getEndpointFor('api/supreme/search/lucene');
  protected rulesetSearchUrl = this.applicationConfigService.getEndpointFor('api/supreme/search/ruleset');
  protected bqlSearchUrl = this.applicationConfigService.getEndpointFor('api/supreme/search/bql');
  protected qbSpecUrl = this.applicationConfigService.getEndpointFor('api/supreme/query-builder-spec');

  query(req?: any): Observable<EntityArrayResponseType> {
    const options = createRequestOption(req);
    let queryString = 'query=';

    if (req?.query) {
      queryString += String(req.query);
      delete req.query;
    } else {
      queryString += '*';
    }

    return this.http.get<{
      hits: ISupreme[] | any[];
      hitType: string;
      totalHits: number;
      searchAfter: string[];
      pitId: string | null;
    }>(`${this.searchUrl}?${queryString}`, {
      params: options,
      observe: 'response',
    });
  }

  searchWithRuleset(ruleset: any): Observable<EntityArrayResponseType> {
    return this.http.post<{
      hits: ISupreme[] | any[];
      hitType: string;
      totalHits: number;
      searchAfter: string[];
      pitId: string | null;
    }>(this.rulesetSearchUrl, ruleset, {
      observe: 'response',
    });
  }

  searchWithBql(bqlQuery: string, req?: any): Observable<EntityArrayResponseType> {
    const options = createRequestOption(req);
    return this.http.post<{
      hits: ISupreme[] | any[];
      hitType: string;
      totalHits: number;
      searchAfter: string[];
      pitId: string | null;
    }>(this.bqlSearchUrl, bqlQuery, {
      params: options,
      headers: { 'Content-Type': 'text/plain' },
      observe: 'response',
    });
  }

  searchView(req: any): Observable<ViewArrayResponseType> {
    const options = createRequestOption(req);
    let queryString = 'query=';

    if (req?.query) {
      queryString += String(req.query);
      delete req.query;
    } else {
      queryString += '*';
    }

    return this.http.get<{
      hits: IViewResult[];
      hitType: string;
      viewName: string;
      viewCategory?: string;
      totalHits: number;
    }>(`${this.searchUrl}?${queryString}`, {
      params: options,
      observe: 'response',
    });
  }

  getQueryBuilderSpec(): Observable<any> {
    return this.http.get<any>(this.qbSpecUrl);
  }
}
