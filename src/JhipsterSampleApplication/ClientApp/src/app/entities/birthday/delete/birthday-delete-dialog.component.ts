import { Component, Input, inject } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { NO_ERRORS_SCHEMA } from '@angular/core';

import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';

import { IBirthday } from '../birthday.model';
import { BirthdayService } from '../service/birthday.service';
import SharedModule from 'app/shared/shared.module';

@Component({
  standalone: true,
  templateUrl: './birthday-delete-dialog.component.html',
  imports: [FontAwesomeModule],
  schemas: [NO_ERRORS_SCHEMA],
})
export class BirthdayDeleteDialogComponent {
  @Input() birthday?: IBirthday;

  protected activeModal = inject(NgbActiveModal);

  cancel(): void {
    this.activeModal.dismiss();
  }

  confirmDelete(id: string): void {
    this.activeModal.close(id);
  }
}
