import { Directive, TemplateRef, inject } from '@angular/core';

@Directive({selector: '[queryEmptyWarning]', standalone: false})
export class QueryEmptyWarningDirective {
  template = inject<TemplateRef<any>>(TemplateRef);
}
