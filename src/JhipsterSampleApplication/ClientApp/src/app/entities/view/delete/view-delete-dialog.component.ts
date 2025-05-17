import { Component, inject } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';

import SharedModule from 'app/shared/shared.module';
import { ITEM_DELETED_EVENT } from 'app/config/navigation.constants';
import { IView } from '../view.model';
import { ViewService } from '../service/view.service';

@Component({
  standalone: true,
  templateUrl: './view-delete-dialog.component.html',
  imports: [SharedModule],
})
export default class ViewDeleteDialogComponent {
  view?: IView;

  private readonly viewService = inject(ViewService);
  private readonly activeModal = inject(NgbActiveModal);

  cancel(): void {
    this.activeModal.dismiss();
  }

  confirmDelete(id: string): void {
    this.viewService.delete(id).subscribe(() => {
      this.activeModal.close(ITEM_DELETED_EVENT);
    });
  }
}
