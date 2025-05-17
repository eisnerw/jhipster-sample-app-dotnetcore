import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import SharedModule from 'app/shared/shared.module';
import { SortDirective } from 'app/shared/sort/sort.directive';
import { SortByDirective } from 'app/shared/sort/sort-by.directive';
import { IView } from '../view.model';
import { EntityArrayResponseType, ViewService } from '../service/view.service';
import ViewDeleteDialogComponent from '../delete/view-delete-dialog.component';
import { SortService } from 'app/shared/sort/sort.service';
import { SortState } from 'app/shared/sort/sort-state';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { NgIf } from '@angular/common';

@Component({
  standalone: true,
  selector: 'jhi-view',
  templateUrl: './view.component.html',
  imports: [SharedModule, RouterModule, FormsModule, SortDirective, SortByDirective, FontAwesomeModule, NgIf],
})
export class ViewComponent implements OnInit {
  views: IView[] | null = null;
  isLoading = false;
  sortState: SortState = { predicate: '', order: 'asc' };

  constructor(
    protected viewService: ViewService,
    protected sortService: SortService,
    protected modalService: NgbModal,
    protected router: Router,
    protected activatedRoute: ActivatedRoute,
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.viewService.query().subscribe({
      next: (res: EntityArrayResponseType) => {
        this.isLoading = false;
        this.views = res.body;
      },
      error: () => {
        this.isLoading = false;
      },
    });
  }

  trackId(_index: number, item: IView): string {
    return item.id!;
  }

  delete(view: IView): void {
    const modalRef = this.modalService.open(ViewDeleteDialogComponent, { size: 'lg', backdrop: 'static' });
    modalRef.componentInstance.view = view;
    modalRef.closed.subscribe((reason: string) => {
      if (reason === 'deleted') {
        this.load();
      }
    });
  }

  protected sort(): string[] {
    const result = [(this.sortState.predicate ?? '') + ',' + (this.sortState.order ?? 'asc')];
    if (this.sortState.predicate !== 'id') {
      result.push('id');
    }
    return result;
  }

  protected onResponseSuccess(response: EntityArrayResponseType): void {
    const dataFromBody = this.fillComponentAttributesFromResponseBody(response.body);
    this.views = this.refill(dataFromBody);
  }

  protected fillComponentAttributesFromResponseBody(data: IView[] | null): IView[] {
    return data ?? [];
  }

  protected refill(data: IView[]): IView[] {
    return data;
  }

  protected navigateToWithComponentValues(sortState: SortState): void {
    this.handleNavigation(sortState);
  }

  protected handleNavigation(sortState: SortState): void {
    const queryParamsObj = {
      sort: this.sortService.buildSortParam(sortState),
    };
    this.router.navigate(['./'], {
      relativeTo: this.activatedRoute,
      queryParams: queryParamsObj,
    });
  }
}
