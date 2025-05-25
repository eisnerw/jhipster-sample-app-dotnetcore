import { Component, inject } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { CommonModule } from '@angular/common';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { FormsModule } from '@angular/forms';

import SharedModule from 'app/shared/shared.module';
import { ITEM_DELETED_EVENT } from 'app/config/navigation.constants';
import { INamedQuery } from '../named-query.model';
import { NamedQueryService } from '../service/named-query.service';

@Component({
  standalone: true,
  templateUrl: './named-query-delete-dialog.component.html',
  imports: [CommonModule, FontAwesomeModule, SharedModule, FormsModule],
})
export class NamedQueryDeleteDialogComponent {
  namedQuery?: INamedQuery;

  protected readonly namedQueryService = inject(NamedQueryService);
  protected activeModal = inject(NgbActiveModal);

  cancel(): void {
    this.activeModal.dismiss();
  }

  confirmDelete(id: number): void {
    this.namedQueryService.delete(id).subscribe(() => {
      this.activeModal.close(ITEM_DELETED_EVENT);
    });
  }
}
