import { Directive, TemplateRef, inject } from '@angular/core';

@Directive({selector: '[queryRulesetAddRuleButton]', standalone: false})
export class QueryRulesetAddRuleButtonDirective {
  template = inject<TemplateRef<any>>(TemplateRef);
}
