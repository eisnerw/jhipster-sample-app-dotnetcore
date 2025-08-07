import { Directive, TemplateRef, inject } from '@angular/core';

@Directive({selector: '[queryRuleRemoveButton]', standalone: false})
export class QueryRuleRemoveButtonDirective {
  template = inject<TemplateRef<any>>(TemplateRef);
}
