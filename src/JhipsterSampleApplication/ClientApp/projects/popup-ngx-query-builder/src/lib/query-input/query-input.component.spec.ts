import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { QueryInputComponent } from './query-input.component';
import { RuleSet, Rule, QueryBuilderConfig } from 'ngx-query-builder';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { MatDialogModule } from '@angular/material/dialog';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { FormsModule } from '@angular/forms';
import { BqlAutocompleteService } from '../services/bql-autocomplete.service';
import { NamedQueryService } from 'app/entities/named-query/service/named-query.service';
import { AccountService } from 'app/core/auth/account.service';
import { of } from 'rxjs';
import { HttpResponse } from '@angular/common/http';

describe('QueryInputComponent', () => {
  it('should set default operator when parsing empty query', () => {
    const component = new QueryInputComponent();
    component.defaultRuleAttribute = 'document';
    const rs = component.parseQuery('');
    const rule = rs.rules[0] as Rule;
    expect(rule.field).toBe('document');
    expect(rule.operator).toBe('contains');
  });

  it('should save named ruleset text instead of name', () => {
    const component = new QueryInputComponent();
    (component as any).config = { fields: {} } as any;
    const postSpy = jasmine
      .createSpy('post')
      .and.returnValue({ subscribe: () => {} } as any);
    (component as any).http = { post: postSpy, put: jasmine.createSpy('put') };
    const rs = {
      condition: 'and',
      rules: [{ field: 'a', operator: '=', value: 1 }],
      name: 'TEST',
    };
    component.saveNamedRuleset(rs);
    const payload = postSpy.calls.mostRecent().args[1];
    expect(payload.text).toBe('a=1');
  });
});

describe('QueryInputComponent - Autocomplete Integration Tests', () => {
  let component: QueryInputComponent;
  let fixture: ComponentFixture<QueryInputComponent>;
  let httpMock: HttpTestingController;
  let autocompleteService: BqlAutocompleteService;
  let namedQueryService: jasmine.SpyObj<NamedQueryService>;
  let accountService: jasmine.SpyObj<AccountService>;

  const mockConfig: QueryBuilderConfig = {
    fields: {
      name: {
        name: 'Name',
        type: 'string',
        operators: ['=', '!=', 'contains', '!contains', 'like', '!like'],
        defaultOperator: 'contains'
      },
      age: {
        name: 'Age',
        type: 'number',
        operators: ['=', '!=', '>', '>=', '<', '<='],
        defaultOperator: '='
      },
      active: {
        name: 'Active',
        type: 'boolean',
        operators: ['=', '!='],
        defaultOperator: '='
      },
      status: {
        name: 'Status',
        type: 'category',
        operators: ['=', '!=', 'in', '!in'],
        options: [
          { name: 'Active', value: 'active' },
          { name: 'Inactive', value: 'inactive' },
          { name: 'Pending', value: 'pending' }
        ],
        defaultOperator: '='
      }
    }
  };

  beforeEach(async () => {
    namedQueryService = jasmine.createSpyObj('NamedQueryService', ['query']);
    accountService = jasmine.createSpyObj('AccountService', ['identity']);
    namedQueryService.query.and.returnValue(of(new HttpResponse({ body: [] })));
    accountService.identity.and.returnValue(of(null));

    await TestBed.configureTestingModule({
      imports: [
        QueryInputComponent,
        HttpClientTestingModule,
        MatDialogModule,
        BrowserAnimationsModule,
        FormsModule
      ],
      providers: [
        BqlAutocompleteService,
        { provide: NamedQueryService, useValue: namedQueryService },
        { provide: AccountService, useValue: accountService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(QueryInputComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    autocompleteService = TestBed.inject(BqlAutocompleteService);
    
    component.config = mockConfig;
    component.historyEntity = null;
    component.namedQueryEntity = null;
    
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    fixture.destroy();
  });

  describe('Autocomplete Dropdown Appearance', () => {
    it('should show autocomplete dropdown when typing in edit mode', fakeAsync(() => {
      component.startEdit();
      tick();
      fixture.detectChanges();

      component.query = 'na';
      component.onInputChange({});
      tick(200);
      fixture.detectChanges();

      expect(component.autocompleteSuggestions.length).toBeGreaterThan(0);
      expect(component.showAutocomplete).toBe(true);
      
      const nameSuggestion = component.autocompleteSuggestions.find(s => s.value === 'name');
      expect(nameSuggestion).toBeDefined();
      expect(nameSuggestion?.type).toBe('field');
    }));

    it('should not show autocomplete when not in edit mode', fakeAsync(() => {
      component.query = 'na';
      component.onInputChange({});
      tick(200);
      fixture.detectChanges();

      expect(component.showAutocomplete).toBe(false);
    }));

    it('should show operator suggestions after typing a field name', fakeAsync(() => {
      component.startEdit();
      tick();
      fixture.detectChanges();

      component.query = 'name ';
      component.onInputChange({});
      tick(200);
      fixture.detectChanges();

      expect(component.autocompleteSuggestions.length).toBeGreaterThan(0);
      const operatorSuggestion = component.autocompleteSuggestions.find(s => s.type === 'operator');
      expect(operatorSuggestion).toBeDefined();
    }));

    it('should show value suggestions after typing field and operator', fakeAsync(() => {
      component.startEdit();
      tick();
      fixture.detectChanges();

      component.query = 'status = ';
      component.onInputChange({});
      tick(200);
      fixture.detectChanges();

      expect(component.autocompleteSuggestions.length).toBeGreaterThan(0);
      const valueSuggestion = component.autocompleteSuggestions.find(s => s.type === 'value');
      expect(valueSuggestion).toBeDefined();
      
      const activeSuggestion = component.autocompleteSuggestions.find(s => s.value === 'active');
      expect(activeSuggestion).toBeDefined();
    }));

    it('should hide autocomplete when no suggestions match', fakeAsync(() => {
      component.startEdit();
      tick();
      fixture.detectChanges();

      component.query = 'xyz';
      component.onInputChange({});
      tick(200);
      fixture.detectChanges();

      expect(component.showAutocomplete).toBe(false);
    }));
  });

  describe('Keyboard Navigation', () => {
    beforeEach(fakeAsync(() => {
      component.startEdit();
      tick();
      fixture.detectChanges();

      component.query = 'na';
      component.onInputChange({});
      tick(200);
      fixture.detectChanges();
    }));

    it('should navigate suggestions with arrow down key', () => {
      const initialIndex = component.selectedSuggestionIndex;
      
      const event = new KeyboardEvent('keydown', { key: 'ArrowDown' });
      component.onAutocompleteKeydown(event);

      expect(component.selectedSuggestionIndex).toBe(initialIndex + 1);
    });

    it('should navigate suggestions with arrow up key', () => {
      component.selectedSuggestionIndex = 1;
      
      const event = new KeyboardEvent('keydown', { key: 'ArrowUp' });
      component.onAutocompleteKeydown(event);

      expect(component.selectedSuggestionIndex).toBe(0);
    });

    it('should not go below 0 when pressing arrow up', () => {
      component.selectedSuggestionIndex = 0;
      
      const event = new KeyboardEvent('keydown', { key: 'ArrowUp' });
      component.onAutocompleteKeydown(event);

      expect(component.selectedSuggestionIndex).toBe(0);
    });

    it('should not exceed suggestions length when pressing arrow down', () => {
      const maxIndex = component.autocompleteSuggestions.length - 1;
      component.selectedSuggestionIndex = maxIndex;
      
      const event = new KeyboardEvent('keydown', { key: 'ArrowDown' });
      component.onAutocompleteKeydown(event);

      expect(component.selectedSuggestionIndex).toBe(maxIndex);
    });

    it('should select highlighted suggestion on Enter key', () => {
      component.selectedSuggestionIndex = 0;
      const suggestion = component.autocompleteSuggestions[0];
      
      spyOn(component, 'selectSuggestion');
      
      const event = new KeyboardEvent('keydown', { key: 'Enter' });
      component.onAutocompleteKeydown(event);

      expect(component.selectSuggestion).toHaveBeenCalledWith(suggestion);
    });

    it('should select first suggestion on Enter if none highlighted', () => {
      component.selectedSuggestionIndex = -1;
      const firstSuggestion = component.autocompleteSuggestions[0];
      
      spyOn(component, 'selectSuggestion');
      
      const event = new KeyboardEvent('keydown', { key: 'Enter' });
      component.onAutocompleteKeydown(event);

      expect(component.selectSuggestion).toHaveBeenCalledWith(firstSuggestion);
    });

    it('should close autocomplete on Escape key', () => {
      expect(component.showAutocomplete).toBe(true);
      
      const event = new KeyboardEvent('keydown', { key: 'Escape' });
      component.onAutocompleteKeydown(event);

      expect(component.showAutocomplete).toBe(false);
    });

    it('should select suggestion on Tab key', () => {
      component.selectedSuggestionIndex = 0;
      const suggestion = component.autocompleteSuggestions[0];
      
      spyOn(component, 'selectSuggestion');
      
      const event = new KeyboardEvent('keydown', { key: 'Tab' });
      component.onAutocompleteKeydown(event);

      expect(component.selectSuggestion).toHaveBeenCalledWith(suggestion);
    });
  });

  describe('Suggestion Selection and Insertion', () => {
    beforeEach(fakeAsync(() => {
      component.startEdit();
      tick();
      fixture.detectChanges();
    }));

    it('should insert field suggestion at cursor position', fakeAsync(() => {
      component.query = '';
      const suggestion = {
        value: 'name',
        display: 'Name',
        type: 'field' as const
      };

      component.selectSuggestion(suggestion);
      tick();

      expect(component.query).toContain('name');
    }));

    it('should insert operator suggestion with proper spacing', fakeAsync(() => {
      component.query = 'name';
      const suggestion = {
        value: 'CONTAINS',
        display: 'CONTAINS (contains)',
        type: 'operator' as const
      };

      const inputEl = (component as any).editBox?.inputEL?.nativeElement;
      if (inputEl) {
        Object.defineProperty(inputEl, 'selectionStart', {
          get: () => 4,
          configurable: true
        });
      }

      component.selectSuggestion(suggestion);
      tick();

      expect(component.query).toContain('CONTAINS ');
    }));

    it('should insert value suggestion and close autocomplete', fakeAsync(() => {
      component.query = 'status = ';
      const suggestion = {
        value: 'active',
        display: 'Active',
        type: 'value' as const
      };

      component.selectSuggestion(suggestion);
      tick();

      expect(component.query).toContain('active');
      expect(component.showAutocomplete).toBe(false);
    }));

    it('should quote value suggestions with spaces', fakeAsync(() => {
      component.query = 'name = ';
      const suggestion = {
        value: 'John Doe',
        display: 'John Doe',
        type: 'value' as const
      };

      component.selectSuggestion(suggestion);
      tick();

      expect(component.query).toContain('"John Doe"');
    }));

    it('should validate query after suggestion insertion', fakeAsync(() => {
      spyOn(component, 'onQueryChange');
      
      component.query = '';
      const suggestion = {
        value: 'name',
        display: 'Name',
        type: 'field' as const
      };

      component.selectSuggestion(suggestion);
      tick();

      expect(component.onQueryChange).toHaveBeenCalled();
    }));
  });

  describe('Cursor Position After Insertion', () => {
    beforeEach(fakeAsync(() => {
      component.startEdit();
      tick();
      fixture.detectChanges();
    }));

    it('should position cursor after inserted field name', fakeAsync(() => {
      component.query = '';
      const suggestion = {
        value: 'name',
        display: 'Name',
        type: 'field' as const
      };

      component.selectSuggestion(suggestion);
      tick(100);

      const expectedPosition = 'name '.length;
      expect(component.query.length).toBeGreaterThanOrEqual(expectedPosition);
    }));

    it('should position cursor after inserted operator', fakeAsync(() => {
      component.query = 'name';
      const suggestion = {
        value: 'CONTAINS',
        display: 'CONTAINS (contains)',
        type: 'operator' as const
      };

      component.selectSuggestion(suggestion);
      tick(100);

      expect(component.query).toMatch(/CONTAINS\s/);
    }));
  });

  describe('Autocomplete Close Behavior', () => {
    beforeEach(fakeAsync(() => {
      component.startEdit();
      tick();
      fixture.detectChanges();

      component.query = 'na';
      component.onInputChange({});
      tick(200);
      fixture.detectChanges();
    }));

    it('should close autocomplete on blur', fakeAsync(() => {
      expect(component.showAutocomplete).toBe(true);

      component.onInputBlur();
      tick(250);

      expect(component.showAutocomplete).toBe(false);
    }));

    it('should close autocomplete when cancelEdit is called', () => {
      expect(component.showAutocomplete).toBe(true);

      component.cancelEdit();

      expect(component.showAutocomplete).toBe(false);
    });

    it('should close autocomplete when acceptEdit is called', fakeAsync(() => {
      component.query = 'name CONTAINS "test"';
      component.validQuery = true;
      expect(component.showAutocomplete).toBe(true);

      component.acceptEdit();
      tick();

      expect(component.showAutocomplete).toBe(false);
    }));

    it('should close autocomplete after suggestion selection', fakeAsync(() => {
      const suggestion = component.autocompleteSuggestions[0];
      
      component.selectSuggestion(suggestion);
      tick();

      expect(component.showAutocomplete).toBe(false);
    }));
  });

  describe('Integration with Existing Validation', () => {
    beforeEach(fakeAsync(() => {
      component.startEdit();
      tick();
      fixture.detectChanges();
    }));

    it('should maintain validation state while autocomplete is active', fakeAsync(() => {
      component.query = 'invalid query syntax &&&';
      component.onQueryChange();
      
      expect(component.validQuery).toBe(false);

      component.onInputChange({});
      tick(200);

      expect(component.validQuery).toBe(false);
    }));

    it('should update validation after selecting suggestion', fakeAsync(() => {
      component.query = '';
      const suggestion = {
        value: 'name',
        display: 'Name',
        type: 'field' as const
      };

      component.selectSuggestion(suggestion);
      tick();

      expect(component.validQuery).toBeDefined();
    }));

    it('should not accept invalid query even with autocomplete', fakeAsync(() => {
      component.query = 'invalid &&&';
      component.validQuery = false;

      component.onEnter({ preventDefault: () => {} });
      tick();

      expect(component.editing).toBe(true);
    }));
  });

  describe('Integration with History Navigation', () => {
    beforeEach(fakeAsync(() => {
      component.startEdit();
      tick();
      fixture.detectChanges();
    }));

    it('should use arrow keys for history when autocomplete is closed', () => {
      component.showAutocomplete = false;
      component['history'] = ['previous query 1', 'previous query 2'];
      
      spyOn(component, 'showPrevHistory');
      
      const event = new KeyboardEvent('keydown', { key: 'ArrowUp' });
      component.onAutocompleteKeydown(event);

      expect(component.showPrevHistory).toHaveBeenCalled();
    });

    it('should not trigger history navigation when autocomplete is open', fakeAsync(() => {
      component.query = 'na';
      component.onInputChange({});
      tick(200);
      
      component['history'] = ['previous query'];
      spyOn(component, 'showPrevHistory');

      const event = new KeyboardEvent('keydown', { key: 'ArrowUp' });
      component.onAutocompleteKeydown(event);

      expect(component.showPrevHistory).not.toHaveBeenCalled();
    }));

    it('should use arrow down for history when autocomplete is closed', () => {
      component.showAutocomplete = false;
      component['history'] = ['previous query'];
      component['historyIndex'] = 0;
      
      spyOn(component, 'showNextHistory');
      
      const event = new KeyboardEvent('keydown', { key: 'ArrowDown' });
      component.onAutocompleteKeydown(event);

      expect(component.showNextHistory).toHaveBeenCalled();
    });
  });

  describe('Different QueryLanguageSpec Configurations', () => {
    it('should adapt suggestions to different field types', fakeAsync(() => {
      const customConfig: QueryBuilderConfig = {
        fields: {
          email: {
            name: 'Email',
            type: 'string',
            operators: ['=', '!=', 'contains'],
            defaultOperator: 'contains'
          },
          count: {
            name: 'Count',
            type: 'number',
            operators: ['=', '>', '<'],
            defaultOperator: '='
          }
        }
      };

      component.config = customConfig;
      component.startEdit();
      tick();
      fixture.detectChanges();

      component.query = 'email ';
      component.onInputChange({});
      tick(200);
      fixture.detectChanges();

      const containsOp = component.autocompleteSuggestions.find(s => s.value === 'CONTAINS');
      expect(containsOp).toBeDefined();

      component.query = 'count ';
      component.onInputChange({});
      tick(200);
      fixture.detectChanges();

      const greaterThanOp = component.autocompleteSuggestions.find(s => s.value === '>');
      expect(greaterThanOp).toBeDefined();
    }));

    it('should handle boolean field value suggestions', fakeAsync(() => {
      component.startEdit();
      tick();
      fixture.detectChanges();

      component.query = 'active = ';
      component.onInputChange({});
      tick(200);
      fixture.detectChanges();

      const trueSuggestion = component.autocompleteSuggestions.find(s => s.value === 'true');
      const falseSuggestion = component.autocompleteSuggestions.find(s => s.value === 'false');
      
      expect(trueSuggestion).toBeDefined();
      expect(falseSuggestion).toBeDefined();
    }));

    it('should handle fields with predefined options', fakeAsync(() => {
      component.startEdit();
      tick();
      fixture.detectChanges();

      component.query = 'status = ';
      component.onInputChange({});
      tick(200);
      fixture.detectChanges();

      const activeSuggestion = component.autocompleteSuggestions.find(s => s.value === 'active');
      const inactiveSuggestion = component.autocompleteSuggestions.find(s => s.value === 'inactive');
      const pendingSuggestion = component.autocompleteSuggestions.find(s => s.value === 'pending');
      
      expect(activeSuggestion).toBeDefined();
      expect(inactiveSuggestion).toBeDefined();
      expect(pendingSuggestion).toBeDefined();
    }));

    it('should handle config changes and clear cache', fakeAsync(() => {
      spyOn(autocompleteService, 'clearCache');

      const newConfig: QueryBuilderConfig = {
        fields: {
          newField: {
            name: 'New Field',
            type: 'string',
            operators: ['='],
            defaultOperator: '='
          }
        }
      };

      component.config = newConfig;
      component.ngOnChanges({
        config: {
          currentValue: newConfig,
          previousValue: mockConfig,
          firstChange: false,
          isFirstChange: () => false
        }
      });

      expect(autocompleteService.clearCache).toHaveBeenCalled();
    }));

    it('should handle missing config gracefully', fakeAsync(() => {
      component.config = undefined;
      component.startEdit();
      tick();
      fixture.detectChanges();

      component.query = 'test';
      component.onInputChange({});
      tick(200);
      fixture.detectChanges();

      expect(component.autocompleteSuggestions).toBeDefined();
    }));
  });

  describe('Complex Query Scenarios', () => {
    beforeEach(fakeAsync(() => {
      component.startEdit();
      tick();
      fixture.detectChanges();
    }));

    it('should provide field suggestions after logical AND operator', fakeAsync(() => {
      component.query = 'name CONTAINS "test" & ';
      component.onInputChange({});
      tick(200);
      fixture.detectChanges();

      const fieldSuggestion = component.autocompleteSuggestions.find(s => s.type === 'field');
      expect(fieldSuggestion).toBeDefined();
    }));

    it('should provide field suggestions after logical OR operator', fakeAsync(() => {
      component.query = 'name CONTAINS "test" | ';
      component.onInputChange({});
      tick(200);
      fixture.detectChanges();

      const fieldSuggestion = component.autocompleteSuggestions.find(s => s.type === 'field');
      expect(fieldSuggestion).toBeDefined();
    }));

    it('should provide field suggestions inside parentheses', fakeAsync(() => {
      component.query = '(';
      component.onInputChange({});
      tick(200);
      fixture.detectChanges();

      const fieldSuggestion = component.autocompleteSuggestions.find(s => s.type === 'field');
      expect(fieldSuggestion).toBeDefined();
    }));

    it('should handle nested parentheses context', fakeAsync(() => {
      component.query = '(name CONTAINS "test" & (';
      component.onInputChange({});
      tick(200);
      fixture.detectChanges();

      const fieldSuggestion = component.autocompleteSuggestions.find(s => s.type === 'field');
      expect(fieldSuggestion).toBeDefined();
    }));
  });

  describe('Performance and Debouncing', () => {
    beforeEach(fakeAsync(() => {
      component.startEdit();
      tick();
      fixture.detectChanges();
    }));

    it('should debounce input changes', fakeAsync(() => {
      spyOn(autocompleteService, 'getSuggestions').and.returnValue([]);

      component.query = 'n';
      component.onInputChange({});
      
      component.query = 'na';
      component.onInputChange({});
      
      component.query = 'nam';
      component.onInputChange({});

      expect(autocompleteService.getSuggestions).not.toHaveBeenCalled();

      tick(200);

      expect(autocompleteService.getSuggestions).toHaveBeenCalledTimes(1);
    }));

    it('should limit number of displayed suggestions', fakeAsync(() => {
      const manyFieldsConfig: QueryBuilderConfig = {
        fields: {}
      };

      for (let i = 0; i < 50; i++) {
        manyFieldsConfig.fields[`field${i}`] = {
          name: `Field ${i}`,
          type: 'string',
          operators: ['='],
          defaultOperator: '='
        };
      }

      component.config = manyFieldsConfig;
      component.query = '';
      component.onInputChange({});
      tick(200);
      fixture.detectChanges();

      expect(component.autocompleteSuggestions.length).toBeLessThanOrEqual(15);
    }));
  });

  describe('Cleanup and Memory Management', () => {
    it('should unsubscribe on destroy', () => {
      const destroySpy = spyOn(component['destroy$'], 'next');
      const completeSpy = spyOn(component['destroy$'], 'complete');

      component.ngOnDestroy();

      expect(destroySpy).toHaveBeenCalled();
      expect(completeSpy).toHaveBeenCalled();
    });

    it('should clear autocomplete state on destroy', () => {
      component.showAutocomplete = true;
      component.autocompleteSuggestions = [
        { value: 'test', display: 'test', type: 'field' }
      ];

      component.ngOnDestroy();

      expect(component['destroy$'].closed).toBe(true);
    });
  });
});
