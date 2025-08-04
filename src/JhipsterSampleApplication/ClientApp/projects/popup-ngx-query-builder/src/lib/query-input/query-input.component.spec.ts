import { QueryInputComponent } from './query-input.component';
import { MatDialog } from '@angular/material/dialog';
import { RuleSet, Rule } from 'ngx-query-builder';

describe('QueryInputComponent', () => {
  it('should set default operator when parsing empty query', () => {
    const component = new QueryInputComponent({} as MatDialog);
    component.defaultRuleAttribute = 'document';
    const rs: RuleSet = component.parseQuery('');
    const rule = rs.rules[0] as Rule;
    expect(rule.field).toBe('document');
    expect(rule.operator).toBe('contains');
  });
});
