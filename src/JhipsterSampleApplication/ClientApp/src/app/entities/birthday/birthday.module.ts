import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { NgbPaginationModule } from '@ng-bootstrap/ng-bootstrap';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { ContextMenuModule } from 'primeng/contextmenu';
import SharedModule from 'app/shared/shared.module';
import ItemCountComponent from 'app/shared/pagination/item-count.component';

import { BirthdayComponent } from './list/birthday.component';
import { BirthdayDeleteDialogComponent } from './delete/birthday-delete-dialog.component';

@NgModule({
  imports: [
    RouterModule,
    FormsModule,
    SharedModule,
    ItemCountComponent,
    NgbPaginationModule,
    FontAwesomeModule,
    CommonModule,
    ContextMenuModule,
  ],
  declarations: [BirthdayComponent, BirthdayDeleteDialogComponent],
  exports: [BirthdayComponent]
})
export class BirthdayModule { }
