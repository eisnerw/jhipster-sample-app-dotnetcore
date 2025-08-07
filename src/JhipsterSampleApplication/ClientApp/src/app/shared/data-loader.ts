import { BehaviorSubject, Observable } from 'rxjs';
import { HttpHeaders, HttpResponse } from '@angular/common/http';

export type SearchResponseData<T> = {
  hits: T[];
  hitType: string;
  totalHits: number;
  searchAfter: string[];
  pitId: string | null;
} | null;

export type FetchFunction<T> = (queryParams: any) => Observable<HttpResponse<SearchResponseData<T>>>;

export class DataLoader<T> {
  public data$: BehaviorSubject<T[]>;
  public totalItems$: Observable<number>;
  public loading$: Observable<boolean>;
  public loadingMessage$: Observable<string>;

  private buffer: T[] = [];
  private bufferSubject = new BehaviorSubject<T[]>(this.buffer);

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

  constructor(private fetchFunction: FetchFunction<T>) {
    this.data$ = this.bufferSubject;
    this.totalItems$ = this.totalItemsSubject.asObservable();
    this.loading$ = this.loadingSubject.asObservable();
    this.loadingMessage$ = this.loadingMessageSubject.asObservable();
  }

  load(itemsPerPage: number, predicate: string, ascending: boolean, filter?: any): void {
    this.loadingSubject.next(true);
    this.loadingMessageSubject.next('Loading... ');

    this.itemsPerPage = itemsPerPage;
    this.predicate = predicate;
    this.ascending = ascending;
    this.filter = filter;
    this.buffer = []; // Reset buffer on new load
    // Reset pagination state to ensure a fresh search
    this.pitId = null;
    this.searchAfter = [];

    const queryParams: any = {
      pageSize: this.itemsPerPage,
      sort: this.getSortQueryParam(this.predicate, this.ascending),
      ...this.filter,
      page: 0,
    };

    this.fetchFunction(queryParams).subscribe({
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

    this.fetchFunction(queryParams).subscribe({
      next: (response: HttpResponse<any>) => this.onSuccess(response.body, response.headers, false),
      error: () => this.onError(),
    });
  }

  private onSuccess(data: SearchResponseData<T>, headers: HttpHeaders, isInitialLoad: boolean): void {
    const newHits = data?.hits ?? [];
    if (isInitialLoad) {
      this.totalItemsSubject.next(data?.totalHits ?? 0);
      this.buffer = [];
    }

    const remaining = this.dataLoadLimit - this.buffer.length;
    const toAdd = newHits.slice(0, remaining);
    if (toAdd.length > 0) {
      this.buffer.push(...toAdd);

      // PERFORMANCE: Batch DOM updates - only emit every 100 items to reduce rendering
      const shouldEmitUpdate = this.buffer.length % 100 === 0 || this.buffer.length >= this.dataLoadLimit;

      if (shouldEmitUpdate) {
        this.bufferSubject.next([...this.buffer]); // New array reference for change detection
      }
    }

    this.pitId = data?.pitId ?? null;
    this.searchAfter = data?.searchAfter ?? [];

    const currentLength = this.buffer.length;
    const totalItems = this.totalItemsSubject.getValue();

    if (totalItems === 0) {
      this.bufferSubject.next([...this.buffer]);
      this.loadingMessageSubject.next('No hits');
      this.loadingSubject.next(true);
      return;
    }

    if (currentLength >= this.dataLoadLimit) {
      if (totalItems > this.dataLoadLimit) {
        const hitLabel = totalItems >= 10000 ? 'Over 10000' : totalItems.toString();
        const message = `${hitLabel} hits (too many to display, showing the first ${this.dataLoadLimit})`;
        this.loadingMessageSubject.next(message);
        this.loadingSubject.next(true);
      } else {
        this.loadingMessageSubject.next('');
        this.loadingSubject.next(false);
      }
      return;
    }

    if (this.pitId && this.searchAfter.length > 0 && currentLength < totalItems) {
      const message = `loading ${currentLength}...`;
      this.loadingMessageSubject.next(message);
      // Keep loading state as true since we're going to load more data
      this.loadingSubject.next(true);
      setTimeout(() => this.loadMore(), 10);
    } else {
      this.loadingMessageSubject.next('');
      this.loadingSubject.next(false);
      // PERFORMANCE: Ensure final data is emitted even if below batch threshold
      this.bufferSubject.next([...this.buffer]);
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
