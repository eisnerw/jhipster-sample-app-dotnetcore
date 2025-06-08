import { Component, OnInit } from '@angular/core';
import { HttpHeaders, HttpResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { combineLatest } from 'rxjs';
import { NgbModal, NgbPagination } from '@ng-bootstrap/ng-bootstrap';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import ItemCountComponent from 'app/shared/pagination/item-count.component';
import { NgbPaginationModule } from '@ng-bootstrap/ng-bootstrap';

import { BirthdayService } from '../service/birthday.service';
import { IBirthday } from '../birthday.model';
import { BirthdayDeleteDialogComponent } from '../delete/birthday-delete-dialog.component';
import { SortDirective, SortByDirective } from 'app/shared/sort';
import SharedModule from 'app/shared/shared.module';

@Component({
  selector: 'jhi-birthday',
  templateUrl: './birthday.component.html',
  standalone: true,
  imports: [
    RouterModule,
    FormsModule,
    SharedModule,
    ItemCountComponent,
    NgbPaginationModule,
    FontAwesomeModule,
    CommonModule,
  ],
  schemas: [NO_ERRORS_SCHEMA],
})
export class BirthdayComponent implements OnInit {
  birthdays?: IBirthday[];
  isLoading = false;
  totalItems = 0;
  itemsPerPage = 20;
  page?: number;
  predicate!: string;
  ascending!: boolean;
  ngbPaginationPage = 1;

  constructor(
    protected birthdayService: BirthdayService,
    protected activatedRoute: ActivatedRoute,
    protected router: Router,
    protected modalService: NgbModal,
  ) {}

  loadPage(page?: number, dontNavigate?: boolean): void {
    this.isLoading = true;
    const pageToLoad: number = page ?? this.page ?? 1;

    this.birthdayService
      .query({
        page: pageToLoad - 1,
        size: this.itemsPerPage,
        sort: this.sort(),
      })
      .subscribe({
        next: (res: HttpResponse<{ hits: IBirthday[] }>) => {
          this.isLoading = false;
          this.onSuccess(res.body, res.headers, pageToLoad, !dontNavigate);
        },
        error: () => {
          this.isLoading = false;
          this.onError();
        },
      });
  }

  ngOnInit(): void {
    this.handleNavigation();
  }

  trackId(index: number, item: IBirthday): string {
    return item.id!;
  }

  delete(birthday: IBirthday): void {
    const modalRef = this.modalService.open(BirthdayDeleteDialogComponent, {
      size: 'lg',
      backdrop: 'static',
    });
    modalRef.componentInstance.birthday = birthday;
    // unsubscribe not needed because closed completes on modal close
    modalRef.closed.subscribe((reason: string) => {
      if (reason === 'deleted') {
        this.loadPage();
      }
    });
  }

  protected sort(): string[] {
    const result = [this.predicate + ',' + (this.ascending ? 'asc' : 'desc')];
    if (this.predicate !== 'id') {
      result.push('id');
    }
    return result;
  }

  protected handleNavigation(): void {
    combineLatest([
      this.activatedRoute.data,
      this.activatedRoute.queryParamMap,
    ]).subscribe(([data, params]) => {
      const page = params.get('page');
      const pageNumber = page !== null ? +page : 1;
      const sort = (params.get('sort') ?? data['defaultSort']).split(',');
      const predicate = sort[0];
      const ascending = sort[1] === 'asc';
      if (
        pageNumber !== this.page ||
        predicate !== this.predicate ||
        ascending !== this.ascending
      ) {
        this.predicate = predicate;
        this.ascending = ascending;
        this.loadPage(pageNumber, true);
      }
    });
  }

  protected onSuccess(
    data: { hits: IBirthday[] } | null,
    headers: HttpHeaders,
    page: number,
    navigate: boolean,
  ): void {
    this.totalItems = Number(headers.get('X-Total-Count'));
    this.page = page;
    this.birthdays = data?.hits ?? [];
    if (navigate) {
      this.router.navigate(['/birthday'], {
        queryParams: {
          page: this.page,
          size: this.itemsPerPage,
          sort: this.predicate + ',' + (this.ascending ? 'asc' : 'desc'),
        },
      });
    }
    this.ngbPaginationPage = this.page;
  }

  protected onError(): void {
    this.ngbPaginationPage = this.page ?? 1;
  }
}
