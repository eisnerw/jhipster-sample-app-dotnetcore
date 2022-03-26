import { Component } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { JhiEventManager } from 'ng-jhipster';

import { ISelector } from 'app/shared/model/selector.model';
import { SelectorService } from './selector.service';

@Component({
  templateUrl: './selector-delete-dialog.component.html',
})
export class SelectorDeleteDialogComponent {
  selector?: ISelector;

  constructor(protected selectorService: SelectorService, public activeModal: NgbActiveModal, protected eventManager: JhiEventManager) {}

  cancel(): void {
    this.activeModal.dismiss();
  }

  confirmDelete(id: number): void {
    this.selectorService.delete(id).subscribe(() => {
      this.eventManager.broadcast('selectorListModification');
      this.activeModal.close();
    });
  }
}
