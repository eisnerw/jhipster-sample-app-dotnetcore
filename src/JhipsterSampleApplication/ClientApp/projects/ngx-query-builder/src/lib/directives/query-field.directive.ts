import { Directive, TemplateRef, inject } from '@angular/core';

@Directive({selector: '[queryField]', standalone: false})
export class QueryFieldDirective {
  template = inject<TemplateRef<any>>(TemplateRef);
}
