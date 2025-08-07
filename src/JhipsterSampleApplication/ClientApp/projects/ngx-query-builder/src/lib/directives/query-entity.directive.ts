import { Directive, TemplateRef, inject } from '@angular/core';

@Directive({selector: '[queryEntity]', standalone: false})
export class QueryEntityDirective {
  template = inject<TemplateRef<any>>(TemplateRef);
}
