import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { IBirthday } from '../birthday.model';
import { BirthdayService } from '../service/birthday.service';
import { HttpHeaders, HttpResponse } from '@angular/common/http';
import { TOTAL_COUNT_RESPONSE_HEADER } from 'app/config/pagination.constants';

type BirthdaySearchResponseData = {
  hits: IBirthday[];
  hitType: string;
  totalHits: number;
  searchAfter: string[];
  pitId: string | null;
} | null;

@Injectable()
export class BirthdayDataLoader {
  public data$: Observable<IBirthday[]>;
  public totalItems$: Observable<number>;
  public loading$: Observable<boolean>;
  public loadingMessage$: Observable<string>;

  private dataSubject = new BehaviorSubject<IBirthday[]>([]);
  private totalItemsSubject = new BehaviorSubject<number>(0);
  private loadingSubject = new BehaviorSubject<boolean>(false);
  private loadingMessageSubject = new BehaviorSubject<string>('');

  private pitId: string | null = null;
  private searchAfter: string[] = [];

  private itemsPerPage = 50;
  private predicate = 'id';
  private ascending = true;
  private filter: any;
  private readonly dataLoadLimit = 1000;

  constructor(private birthdayService: BirthdayService) {
    this.data$ = this.dataSubject.asObservable();
    this.totalItems$ = this.totalItemsSubject.asObservable();
    this.loading$ = this.loadingSubject.asObservable();
    this.loadingMessage$ = this.loadingMessageSubject.asObservable();
  }

  load(itemsPerPage: number, predicate: string, ascending: boolean, filter?: any): void {
    this.loadingSubject.next(true);
    this.loadingMessageSubject.next('loading...');

    this.itemsPerPage = itemsPerPage;
    this.predicate = predicate;
    this.ascending = ascending;
    this.filter = filter;

    const queryParams: any = {
      pageSize: this.itemsPerPage,
      sort: this.getSortQueryParam(this.predicate, this.ascending),
      ...this.filter,
      page: 0,
    };

    this.birthdayService.query(queryParams).subscribe({
      next: (response: HttpResponse<any>) => this.onSuccess(response.body, response.headers, true),
      error: () => this.onError(),
    });
  }

  private loadMore(): void {
    if (!this.pitId || this.searchAfter.length === 0) {
      this.loadingSubject.next(false);
      return;
    }

    const queryParams = {
      pageSize: this.itemsPerPage,
      sort: this.getSortQueryParam(this.predicate, this.ascending),
      pitId: this.pitId,
      searchAfter: this.searchAfter,
      ...this.filter,
    };

    this.birthdayService.query(queryParams).subscribe({
      next: (response: HttpResponse<any>) => this.onSuccess(response.body, response.headers, false),
      error: () => this.onError(),
    });
  }

  private onSuccess(data: BirthdaySearchResponseData, headers: HttpHeaders, isInitialLoad: boolean): void {
    const newHits = data?.hits ?? [];
    if (isInitialLoad) {
      this.totalItemsSubject.next(data?.totalHits ?? 0);
      this.dataSubject.next(newHits);
    } else {
      const currentData = this.dataSubject.getValue();
      this.dataSubject.next([...currentData, ...newHits]);
    }

    this.pitId = data?.pitId ?? null;
    this.searchAfter = data?.searchAfter ?? [];

    const currentLength = this.dataSubject.getValue().length;
    const totalItems = this.totalItemsSubject.getValue();

    if (currentLength >= this.dataLoadLimit) {
      const limitedData = this.dataSubject.getValue().slice(0, this.dataLoadLimit);
      this.dataSubject.next(limitedData);
      const message = `${totalItems} hits (too many to display, showing the first ${this.dataLoadLimit})`;
      this.loadingMessageSubject.next(message);
      this.loadingSubject.next(false);
      return;
    }

    if (this.pitId && this.searchAfter.length > 0 && currentLength < totalItems) {
      const message = `loading ${currentLength}...`;
      this.loadingMessageSubject.next(message);
      setTimeout(() => this.loadMore(), 10);
    } else {
      this.loadingMessageSubject.next('');
      this.loadingSubject.next(false);
    }
  }

  private onError(): void {
    this.loadingMessageSubject.next('Error loading data.');
    this.loadingSubject.next(false);
  }

  private getSortQueryParam(predicate: string, ascending: boolean): string[] {
    const direction = ascending ? 'asc' : 'desc';
    return [`${predicate},${direction}`];
  }
}
