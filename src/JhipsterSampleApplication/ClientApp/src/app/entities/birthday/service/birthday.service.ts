import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

import { ApplicationConfigService } from 'app/core/config/application-config.service';
import { createRequestOption } from 'app/core/request/request-util';
import { IBirthday, IViewResult } from '../birthday.model';

export type EntityResponseType = HttpResponse<IBirthday>;
export type EntityArrayResponseType = HttpResponse<{
  hits: IBirthday[];
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

export interface ISimpleApiResponse {
  success: boolean;
  message?: string;
}

export interface ICategorizeMultipleRequest {
  rows: string[];
  add: string[];
  remove: string[];
}

@Injectable({ providedIn: 'root' })
export class BirthdayService {
  protected http = inject(HttpClient);
  protected applicationConfigService = inject(ApplicationConfigService);

  protected resourceUrl = this.applicationConfigService.getEndpointFor('api/birthdays');
  protected searchUrl = this.applicationConfigService.getEndpointFor('api/birthdays/search/lucene');
  protected rulesetSearchUrl = this.applicationConfigService.getEndpointFor('api/birthdays/search/ruleset');
  protected bqlSearchUrl = this.applicationConfigService.getEndpointFor('api/birthdays/search/bql');

  create(birthday: IBirthday): Observable<EntityResponseType> {
    return this.http.post<IBirthday>(this.resourceUrl, birthday, {
      observe: 'response',
    });
  }

  update(birthday: IBirthday): Observable<EntityResponseType> {
    return this.http.put<IBirthday>(`${this.resourceUrl}/${birthday.id}`, birthday, {
      observe: 'response',
    });
  }

  find(id: string): Observable<EntityResponseType> {
    return this.http.get<IBirthday>(`${this.resourceUrl}/${id}`, {
      observe: 'response',
    });
  }

  query(req?: any): Observable<EntityArrayResponseType> {
    const options = createRequestOption(req);
    let queryString = 'query=';

    if (req?.query) {
      queryString += String(req.query);
      delete req.query; // Ensure it's not added as a URL parameter again
    } else {
      queryString += '*';
    }

    return this.http.get<{
      hits: IBirthday[];
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
      hits: IBirthday[];
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
      hits: IBirthday[];
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

  delete(id: string): Observable<HttpResponse<{}>> {
    return this.http.delete(`${this.resourceUrl}/${id}`, {
      observe: 'response',
    });
  }

  getUniqueValues(field: string): Observable<HttpResponse<string[]>> {
    return this.http.get<string[]>(`${this.resourceUrl}/unique-values/${field}`, {
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

  categorizeMultiple(payload: ICategorizeMultipleRequest): Observable<HttpResponse<ISimpleApiResponse>> {
    return this.http.post<ISimpleApiResponse>(`${this.resourceUrl}/categorize-multiple`, payload, {
      observe: 'response',
    });
  }
}
