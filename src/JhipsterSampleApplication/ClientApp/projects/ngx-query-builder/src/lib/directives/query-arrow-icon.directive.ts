import { Directive, TemplateRef, inject } from '@angular/core';

@Directive({selector: '[queryArrowIcon]', standalone: false})
export class QueryArrowIconDirective {
  template = inject<TemplateRef<any>>(TemplateRef);
}
