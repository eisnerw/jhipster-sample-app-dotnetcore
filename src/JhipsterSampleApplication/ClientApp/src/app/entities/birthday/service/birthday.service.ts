import { Injectable } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

import { ApplicationConfigService } from 'app/core/config/application-config.service';
import { createRequestOption } from 'app/core/request/request-util';
import { IBirthday } from '../birthday.model';

export type EntityResponseType = HttpResponse<IBirthday>;
export type EntityArrayResponseType = HttpResponse<{
  hits: IBirthday[];
  hitType: string;
  totalHits: number;
  searchAfter: string[];
  pitId: string | null;
}>;

@Injectable({ providedIn: 'root' })
export class BirthdayService {
  protected resourceUrl =
    this.applicationConfigService.getEndpointFor('api/birthdays');
  protected searchUrl = this.applicationConfigService.getEndpointFor(
    'api/birthdays/search/lucene',
  );
  protected rulesetSearchUrl = this.applicationConfigService.getEndpointFor(
    'api/birthdays/search/ruleset',
  );

  constructor(
    protected http: HttpClient,
    protected applicationConfigService: ApplicationConfigService,
  ) {}

  create(birthday: IBirthday): Observable<EntityResponseType> {
    return this.http.post<IBirthday>(this.resourceUrl, birthday, {
      observe: 'response',
    });
  }

  update(birthday: IBirthday): Observable<EntityResponseType> {
    return this.http.put<IBirthday>(
      `${this.resourceUrl}/${birthday.id}`,
      birthday,
      {
        observe: 'response',
      },
    );
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

  delete(id: string): Observable<HttpResponse<{}>> {
    return this.http.delete(`${this.resourceUrl}/${id}`, {
      observe: 'response',
    });
  }

  getUniqueValues(field: string): Observable<HttpResponse<string[]>> {
    return this.http.get<string[]>(
      `${this.resourceUrl}/unique-values/${field}`,
      {
        observe: 'response',
      },
    );
  }
}
