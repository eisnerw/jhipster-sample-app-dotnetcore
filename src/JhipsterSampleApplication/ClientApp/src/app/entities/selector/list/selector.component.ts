import { Component, OnInit } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';

import { ISelector } from '../selector.model';
import { SelectorService } from '../service/selector.service';
import { SelectorDeleteDialogComponent } from '../delete/selector-delete-dialog.component';

@Component({
  selector: 'jhi-selector',
  templateUrl: './selector.component.html',
})
export class SelectorComponent implements OnInit {
  selectors?: ISelector[];
  isLoading = false;

  constructor(
    protected selectorService: SelectorService,
    protected modalService: NgbModal,
  ) {}

  loadAll(): void {
    this.isLoading = true;

    this.selectorService.query().subscribe(
      (res: HttpResponse<ISelector[]>) => {
        this.isLoading = false;
        this.selectors = res.body ?? [];
      },
      () => {
        this.isLoading = false;
      },
    );
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
