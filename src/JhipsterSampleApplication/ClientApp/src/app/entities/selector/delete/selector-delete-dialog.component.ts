import { Component } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { CommonModule } from '@angular/common';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import SharedModule from 'app/shared/shared.module';

import { ISelector } from '../selector.model';
import { SelectorService } from '../service/selector.service';

@Component({
  standalone: true,
  selector: 'jhi-selector-delete-dialog',
  templateUrl: './selector-delete-dialog.component.html',
  imports: [CommonModule, FontAwesomeModule, SharedModule],
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
