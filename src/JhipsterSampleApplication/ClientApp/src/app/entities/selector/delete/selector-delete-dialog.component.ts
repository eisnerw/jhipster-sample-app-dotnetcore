import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';

import SharedModule from 'app/shared/shared.module';
import { ITEM_DELETED_EVENT } from 'app/config/navigation.constants';
import { ISelector } from '../selector.model';
import { SelectorService } from '../service/selector.service';

@Component({
  templateUrl: './selector-delete-dialog.component.html',
  imports: [SharedModule, FormsModule],
})
export class SelectorDeleteDialogComponent {
  selector?: ISelector;

  protected selectorService = inject(SelectorService);
  protected activeModal = inject(NgbActiveModal);

  cancel(): void {
    this.activeModal.dismiss();
  }

  confirmDelete(id: number): void {
    this.selectorService.delete(id).subscribe(() => {
      this.activeModal.close(ITEM_DELETED_EVENT);
    });
  }
}
