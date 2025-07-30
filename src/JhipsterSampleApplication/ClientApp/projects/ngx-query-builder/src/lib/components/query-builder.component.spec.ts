import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule } from '@angular/material/dialog';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { SimpleChange } from '@angular/core';
import { QueryBuilderComponent } from './query-builder.component';
import { RuleSet } from '../models/query-builder.interfaces';
import { AddNamedRulesetDialogComponent } from './add-named-ruleset-dialog.component';
import { NamedRulesetDialogComponent } from './named-ruleset-dialog.component';
import { MessageDialogComponent } from './message-dialog.component';

describe('QueryBuilderComponent', () => {
  let component: QueryBuilderComponent;
  let fixture: ComponentFixture<QueryBuilderComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [
        CommonModule,
        FormsModule,
        MatDialogModule,
        BrowserAnimationsModule,
      ],
      declarations: [
        QueryBuilderComponent,
        AddNamedRulesetDialogComponent,
        NamedRulesetDialogComponent,
        MessageDialogComponent,
      ],
    }).compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(QueryBuilderComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should be created', () => {
    expect(component).toBeTruthy();
  });

  it('should save unstored named rulesets when value is set', () => {
    const save = jasmine.createSpy('save');
    const list = jasmine.createSpy('list').and.returnValue([]);
    component.config = {
      fields: {},
      saveNamedRuleset: save,
      listNamedRulesets: list,
    } as any;
    const query: RuleSet = {
      condition: 'and',
      rules: [
        { field: 'a', operator: '=' },
        {
          condition: 'and',
          rules: [{ field: 'b', operator: '=' }],
          name: 'INNER',
        },
      ],
      name: 'ROOT',
    };
    component.value = query;
    expect(save.calls.count()).toBe(2);
    const names = save.calls.allArgs().map((a) => a[0].name);
    expect(names).toContain('ROOT');
    expect(names).toContain('INNER');
  });

  it('should add a rule when selecting a condition on a single-rule set', () => {
    component.operatorMap = { string: ['='] } as any;
    component.config = {
      fields: { name: { name: 'Name', type: 'string', operators: ['='] } },
    } as any;
    component.defaultRuleAttribute = 'name';
    component.data = { condition: 'and', rules: [] } as RuleSet;
    component.ngOnChanges({
      config: new SimpleChange(null, component.config, true),
    } as any);
    component.addRule();
    expect(component.data.rules.length).toBe(1);
    component.changeCondition('or');
    expect(component.data.condition).toBe('or');
    expect(component.data.rules.length).toBe(2);
    expect((component.data.rules[1] as any).field).toBe('name');
  });
});
