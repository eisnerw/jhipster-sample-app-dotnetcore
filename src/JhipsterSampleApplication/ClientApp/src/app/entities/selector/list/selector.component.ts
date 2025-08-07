import { Component, OnInit, inject } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';

import { RouterModule } from '@angular/router';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import SharedModule from 'app/shared/shared.module';

import { ISelector } from '../selector.model';
import { SelectorService } from '../service/selector.service';
import { SelectorDeleteDialogComponent } from '../delete/selector-delete-dialog.component';

@Component({
  standalone: true,
  selector: 'jhi-selector',
  templateUrl: './selector.component.html',
  imports: [RouterModule, FontAwesomeModule, SharedModule],
})
export class SelectorComponent implements OnInit {
  selectors?: ISelector[];
  isLoading = false;

  protected selectorService = inject(SelectorService);
  protected modalService = inject(NgbModal);

  loadAll(): void {
    this.isLoading = true;

    this.selectorService.query().subscribe({
      next: (res: HttpResponse<ISelector[]>) => {
        this.isLoading = false;
        this.selectors = res.body ?? [];
      },
      error: () => {
        this.isLoading = false;
      },
    });
  }

  ngOnInit(): void {
    this.loadAll();
  }

  trackId(index: number, item: ISelector): number {
    return item.id!;
  }

  delete(selector: ISelector): void {
    const modalRef = this.modalService.open(SelectorDeleteDialogComponent, {
      size: 'lg',
      backdrop: 'static',
    });
    modalRef.componentInstance.selector = selector;
    // unsubscribe not needed because closed completes on modal close
    modalRef.closed.subscribe(reason => {
      if (reason === 'deleted') {
        this.loadAll();
      }
    });
  }
}
