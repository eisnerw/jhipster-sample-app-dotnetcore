import { Component, NgZone, OnInit, inject, signal } from '@angular/core';
import {
  ActivatedRoute,
  Data,
  ParamMap,
  Router,
  RouterModule,
} from '@angular/router';
import { Observable, Subscription, combineLatest, filter, tap } from 'rxjs';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';

import SharedModule from 'app/shared/shared.module';
import {
  SortByDirective,
  SortDirective,
  SortService,
  type SortState,
  sortStateSignal,
} from 'app/shared/sort';
import {
  DEFAULT_SORT_DATA,
  ITEM_DELETED_EVENT,
  SORT,
} from 'app/config/navigation.constants';
import { INamedQuery } from '../named-query.model';
import {
  EntityArrayResponseType,
  NamedQueryService,
} from '../service/named-query.service';
import { NamedQueryDeleteDialogComponent } from '../delete/named-query-delete-dialog.component';
import { AccountService } from 'app/core/auth/account.service';

@Component({
  standalone: true,
  selector: 'jhi-named-query',
  templateUrl: './named-query.component.html',
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    FontAwesomeModule,
    SharedModule,
    SortDirective,
    SortByDirective,
  ],
})
export class NamedQueryComponent implements OnInit {
  subscription: Subscription | null = null;
  namedQueries = signal<INamedQuery[]>([]);
  isLoading = false;
  isAdmin = false;

  sortState = sortStateSignal({});

  public readonly router = inject(Router);
  protected readonly namedQueryService = inject(NamedQueryService);
  protected readonly activatedRoute = inject(ActivatedRoute);
  protected readonly sortService = inject(SortService);
  protected modalService = inject(NgbModal);
  protected ngZone = inject(NgZone);
  protected readonly accountService = inject(AccountService);

  trackId = (_index: number, item: INamedQuery): number =>
    this.namedQueryService.getNamedQueryIdentifier(item);

  ngOnInit(): void {
    this.accountService.identity().subscribe((account) => {
      if (account) {
        this.isAdmin = this.accountService.hasAnyAuthority('ROLE_ADMIN');
      }
    });

    this.subscription = combineLatest([
      this.activatedRoute.queryParamMap,
      this.activatedRoute.data,
    ])
      .pipe(
        tap(([params, data]) =>
          this.fillComponentAttributeFromRoute(params, data),
        ),
        tap(() => {
          if (this.namedQueries().length === 0) {
            this.load();
          } else {
            this.namedQueries.set(this.refineData(this.namedQueries()));
          }
        }),
      )
      .subscribe();
  }

  canDelete(namedQuery: INamedQuery): boolean {
    return this.isAdmin || namedQuery.owner !== 'GLOBAL';
  }

  delete(namedQuery: INamedQuery): void {
    const modalRef = this.modalService.open(NamedQueryDeleteDialogComponent, {
      size: 'lg',
      backdrop: 'static',
    });
    modalRef.componentInstance.namedQuery = namedQuery;
    // unsubscribe not needed because closed completes on modal close
    modalRef.closed
      .pipe(
        filter((reason) => reason === ITEM_DELETED_EVENT),
        tap(() => this.load()),
      )
      .subscribe();
  }

  load(): void {
    this.queryBackend().subscribe({
      next: (res: EntityArrayResponseType) => {
        this.onResponseSuccess(res);
      },
    });
  }

  navigateToWithComponentValues(event: SortState): void {
    this.handleNavigation(event);
  }

  protected fillComponentAttributeFromRoute(
    params: ParamMap,
    data: Data,
  ): void {
    this.sortState.set(
      this.sortService.parseSortParam(
        params.get(SORT) ?? data[DEFAULT_SORT_DATA],
      ),
    );
  }

  protected onResponseSuccess(response: EntityArrayResponseType): void {
    const dataFromBody = this.fillComponentAttributesFromResponseBody(
      response.body,
    );
    this.namedQueries.set(this.refineData(dataFromBody));
  }

  protected refineData(data: INamedQuery[]): INamedQuery[] {
    // const { predicate, order } = this.sortState();
    // return predicate && order ? data.sort(this.sortService.startSort({ predicate, order })) : data;
    return data;
  }

  protected fillComponentAttributesFromResponseBody(
    data: INamedQuery[] | null,
  ): INamedQuery[] {
    return data ?? [];
  }

  protected queryBackend(): Observable<EntityArrayResponseType> {
    this.isLoading = true;
    const queryObject: any = {
      sort: this.sortService.buildSortParam(this.sortState()),
    };
    return this.namedQueryService
      .query(queryObject)
      .pipe(tap(() => (this.isLoading = false)));
  }

  protected handleNavigation(sortState: SortState): void {
    const queryParamsObj = {
      sort: this.sortService.buildSortParam(sortState),
    };

    this.ngZone.run(() => {
      this.router.navigate(['./'], {
        relativeTo: this.activatedRoute,
        queryParams: queryParamsObj,
      });
    });
  }
}
