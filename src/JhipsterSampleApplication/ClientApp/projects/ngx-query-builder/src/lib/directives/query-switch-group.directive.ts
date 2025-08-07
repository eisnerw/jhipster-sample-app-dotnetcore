import { Directive, TemplateRef, inject } from '@angular/core';

@Directive({selector: '[querySwitchGroup]', standalone: false})
export class QuerySwitchGroupDirective {
  template = inject<TemplateRef<any>>(TemplateRef);
}
