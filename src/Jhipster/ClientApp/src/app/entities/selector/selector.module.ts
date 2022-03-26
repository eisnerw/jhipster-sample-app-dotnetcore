import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';

import { JhipsterSharedModule } from 'app/shared/shared.module';
import { SelectorComponent } from './selector.component';
import { SelectorDetailComponent } from './selector-detail.component';
import { SelectorUpdateComponent } from './selector-update.component';
import { SelectorDeleteDialogComponent } from './selector-delete-dialog.component';
import { selectorRoute } from './selector.route';

@NgModule({
  imports: [JhipsterSharedModule, RouterModule.forChild(selectorRoute)],
  declarations: [SelectorComponent, SelectorDetailComponent, SelectorUpdateComponent, SelectorDeleteDialogComponent],
  entryComponents: [SelectorDeleteDialogComponent],
})
export class JhipsterSelectorModule {}
