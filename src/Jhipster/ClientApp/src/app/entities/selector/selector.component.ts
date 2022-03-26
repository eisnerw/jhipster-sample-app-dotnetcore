import { Component, OnInit, OnDestroy } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import { Subscription } from 'rxjs';
import { JhiEventManager } from 'ng-jhipster';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';

import { ISelector } from 'app/shared/model/selector.model';
import { SelectorService } from './selector.service';
import { SelectorDeleteDialogComponent } from './selector-delete-dialog.component';

@Component({
  selector: 'jhi-selector',
  templateUrl: './selector.component.html',
})
export class SelectorComponent implements OnInit, OnDestroy {
  selectors?: ISelector[];
  eventSubscriber?: Subscription;

  constructor(protected selectorService: SelectorService, protected eventManager: JhiEventManager, protected modalService: NgbModal) {}

  loadAll(): void {
    this.selectorService.query().subscribe((res: HttpResponse<ISelector[]>) => (this.selectors = res.body || []));
  }

  ngOnInit(): void {
    this.loadAll();
    this.registerChangeInSelectors();
  }

  ngOnDestroy(): void {
    if (this.eventSubscriber) {
      this.eventManager.destroy(this.eventSubscriber);
    }
  }

  trackId(index: number, item: ISelector): number {
    // eslint-disable-next-line @typescript-eslint/no-unnecessary-type-assertion
    return item.id!;
  }

  registerChangeInSelectors(): void {
    this.eventSubscriber = this.eventManager.subscribe('selectorListModification', () => this.loadAll());
  }

  delete(selector: ISelector): void {
    const modalRef = this.modalService.open(SelectorDeleteDialogComponent, { size: 'lg', backdrop: 'static' });
    modalRef.componentInstance.selector = selector;
  }
}
