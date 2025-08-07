import { Directive, TemplateRef, inject } from '@angular/core';

@Directive({selector: '[queryButtonGroup]', standalone: false})
export class QueryButtonGroupDirective {
  template = inject<TemplateRef<any>>(TemplateRef);
}
