import { Directive, TemplateRef, inject } from '@angular/core';

@Directive({selector: '[queryRulesetRemoveButton]', standalone: false})
export class QueryRulesetRemoveButtonDirective {
  template = inject<TemplateRef<any>>(TemplateRef);
}
