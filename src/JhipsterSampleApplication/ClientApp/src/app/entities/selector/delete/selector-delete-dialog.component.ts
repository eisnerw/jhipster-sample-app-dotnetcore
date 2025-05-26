import { Component } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';

import { ISelector } from '../selector.model';
import { SelectorService } from '../service/selector.service';

@Component({
  templateUrl: './selector-delete-dialog.component.html',
})
export class SelectorDeleteDialogComponent {
  selector?: ISelector;

  constructor(
    protected selectorService: SelectorService,
    protected activeModal: NgbActiveModal,
  ) {}

  cancel(): void {
    this.activeModal.dismiss();
  }

  confirmDelete(id: number): void {
    this.selectorService.delete(id).subscribe(() => {
      this.activeModal.close('deleted');
    });
  }
}
