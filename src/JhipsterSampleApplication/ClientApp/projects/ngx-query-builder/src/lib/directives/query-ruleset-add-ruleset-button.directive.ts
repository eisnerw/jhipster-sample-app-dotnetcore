import { Directive, TemplateRef, inject } from '@angular/core';

@Directive({selector: '[queryRulesetAddRulesetButton]', standalone: false})
export class QueryRulesetAddRulesetButtonDirective {
  template = inject<TemplateRef<any>>(TemplateRef);
}
