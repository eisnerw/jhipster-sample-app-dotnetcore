import { Directive, TemplateRef, inject } from '@angular/core';

@Directive({selector: '[queryOperator]', standalone: false})
export class QueryOperatorDirective {
  template = inject<TemplateRef<any>>(TemplateRef);
}
