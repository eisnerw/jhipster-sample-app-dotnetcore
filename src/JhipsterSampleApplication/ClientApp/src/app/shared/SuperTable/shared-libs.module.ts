import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { InfiniteScrollDirective } from 'ngx-infinite-scroll';
import { NgbModule } from '@ng-bootstrap/ng-bootstrap';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';

@NgModule({
  imports: [CommonModule, FormsModule, ReactiveFormsModule, NgbModule, FontAwesomeModule, InfiniteScrollDirective],
  exports: [CommonModule, FormsModule, ReactiveFormsModule, NgbModule, FontAwesomeModule, InfiniteScrollDirective],
})
export class SharedLibsModule {}
