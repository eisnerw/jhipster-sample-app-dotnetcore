import { BqlAutocompleteService } from './bql-autocomplete.service';
import { QueryBuilderConfig } from 'ngx-query-builder';

describe('BqlAutocompleteService', () => {
  let service: BqlAutocompleteService;
  let config: QueryBuilderConfig;

  beforeEach(() => {
    service = new BqlAutocompleteService();
    
    // Setup a basic config for testing
    config = {
      fields: {
        name: {
          name: 'Name',
          type: 'string',
          operators: ['=', '!=', 'contains', '!contains', 'like', '!like']
        },
        age: {
          name: 'Age',
          type: 'number',
          operators: ['=', '!=', '>', '>=', '<', '<=']
        },
        active: {
          name: 'Active',
          type: 'boolean',
          operators: ['=', '!=']
        },
        status: {
          name: 'Status',
          type: 'category',
          operators: ['=', '!=', 'in', '!in'],
          options: [
            { name: 'Active', value: 'active' },
            { name: 'Inactive', value: 'inactive' },
            { name: 'Pending', value: 'pending' }
          ]
        }
      }
    };
  });

  describe('Context Analysis', () => {
    it('should suggest fields for empty query', () => {
      const suggestions = service.getSuggestions('', 0, config);
      expect(suggestions.length).toBeGreaterThan(0);
      expect(suggestions.every(s => s.type === 'field')).toBe(true);
    });

    it('should suggest fields at query start', () => {
      const suggestions = service.getSuggestions('n', 1, config);
      expect(suggestions.length).toBeGreaterThan(0);
      expect(suggestions.some(s => s.value === 'name')).toBe(true);
    });

    it('should suggest operators after field name', () => {
      const suggestions = service.getSuggestions('name ', 5, config);
      expect(suggestions.length).toBeGreaterThan(0);
      expect(suggestions.every(s => s.type === 'operator')).toBe(true);
    });

    it('should suggest values after field and operator', () => {
      const suggestions = service.getSuggestions('active = ', 9, config);
      expect(suggestions.length).toBe(2);
      expect(suggestions.some(s => s.value === 'true')).toBe(true);
      expect(suggestions.some(s => s.value === 'false')).toBe(true);
    });

    it('should suggest fields after logical operator', () => {
      const suggestions = service.getSuggestions('name = "test" & ', 16, config);
      expect(suggestions.length).toBeGreaterThan(0);
      expect(suggestions.every(s => s.type === 'field')).toBe(true);
    });

    it('should suggest fields after opening parenthesis', () => {
      const suggestions = service.getSuggestions('(', 1, config);
      expect(suggestions.length).toBeGreaterThan(0);
      expect(suggestions.every(s => s.type === 'field')).toBe(true);
    });

    it('should suggest fields after OR operator', () => {
      const suggestions = service.getSuggestions('name = "test" | ', 16, config);
      expect(suggestions.length).toBeGreaterThan(0);
      expect(suggestions.every(s => s.type === 'field')).toBe(true);
    });

    it('should suggest fields after negation operator', () => {
      const suggestions = service.getSuggestions('!', 1, config);
      expect(suggestions.length).toBeGreaterThan(0);
      expect(suggestions.every(s => s.type === 'field')).toBe(true);
    });

    it('should handle cursor in middle of query', () => {
      const suggestions = service.getSuggestions('name = "test" & age > 25', 16, config);
      expect(suggestions.length).toBeGreaterThan(0);
      expect(suggestions.every(s => s.type === 'field')).toBe(true);
    });

    it('should handle cursor at end of complete query', () => {
      const suggestions = service.getSuggestions('name = "test"', 13, config);
      expect(suggestions).toBeDefined();
      expect(Array.isArray(suggestions)).toBe(true);
    });

    it('should handle cursor after whitespace', () => {
      const suggestions = service.getSuggestions('name = "test"   ', 16, config);
      expect(suggestions).toBeDefined();
      expect(Array.isArray(suggestions)).toBe(true);
    });

    it('should suggest operators after partial field name with space', () => {
      const suggestions = service.getSuggestions('age ', 4, config);
      expect(suggestions.length).toBeGreaterThan(0);
      expect(suggestions.every(s => s.type === 'operator')).toBe(true);
    });

    it('should suggest values after operator with space', () => {
      const suggestions = service.getSuggestions('status = ', 9, config);
      expect(suggestions.length).toBe(3);
      expect(suggestions.every(s => s.type === 'value')).toBe(true);
    });

    describe('Parentheses Context', () => {
      it('should suggest fields inside parentheses', () => {
        const suggestions = service.getSuggestions('(n', 2, config);
        expect(suggestions.length).toBeGreaterThan(0);
        expect(suggestions.some(s => s.value === 'name')).toBe(true);
      });

      it('should suggest operators inside parentheses after field', () => {
        const suggestions = service.getSuggestions('(name ', 6, config);
        expect(suggestions.length).toBeGreaterThan(0);
        expect(suggestions.every(s => s.type === 'operator')).toBe(true);
      });

      it('should suggest values inside parentheses after field and operator', () => {
        const suggestions = service.getSuggestions('(active = ', 10, config);
        expect(suggestions.length).toBe(2);
        expect(suggestions.every(s => s.type === 'value')).toBe(true);
      });

      it('should handle nested parentheses', () => {
        const suggestions = service.getSuggestions('((name = "test" & ', 18, config);
        expect(suggestions.length).toBeGreaterThan(0);
        expect(suggestions.every(s => s.type === 'field')).toBe(true);
      });

      it('should handle multiple levels of nested parentheses', () => {
        const suggestions = service.getSuggestions('(((', 3, config);
        expect(suggestions.length).toBeGreaterThan(0);
        expect(suggestions.every(s => s.type === 'field')).toBe(true);
      });

      it('should handle cursor after closing parenthesis', () => {
        const suggestions = service.getSuggestions('(name = "test") ', 16, config);
        expect(suggestions).toBeDefined();
        expect(Array.isArray(suggestions)).toBe(true);
      });

      it('should suggest fields after logical operator inside parentheses', () => {
        const suggestions = service.getSuggestions('(name = "test" & ', 17, config);
        expect(suggestions.length).toBeGreaterThan(0);
        expect(suggestions.every(s => s.type === 'field')).toBe(true);
      });

      it('should handle complex nested query with parentheses', () => {
        const suggestions = service.getSuggestions('(name = "test" & (age > 25 | ', 29, config);
        expect(suggestions.length).toBeGreaterThan(0);
        expect(suggestions.every(s => s.type === 'field')).toBe(true);
      });
    });

    describe('Negation Context', () => {
      it('should suggest fields after negation at start', () => {
        const suggestions = service.getSuggestions('!', 1, config);
        expect(suggestions.length).toBeGreaterThan(0);
        expect(suggestions.every(s => s.type === 'field')).toBe(true);
      });

      it('should suggest fields after negation with partial field name', () => {
        const suggestions = service.getSuggestions('!na', 3, config);
        expect(suggestions.length).toBeGreaterThan(0);
        expect(suggestions.some(s => s.value === 'name')).toBe(true);
      });

      it('should handle negated operators (!contains)', () => {
        const configWithNegatedOps: QueryBuilderConfig = {
          fields: {
            name: {
              name: 'Name',
              type: 'string',
              operators: ['=', '!=', 'contains', '!contains', 'like', '!like']
            }
          }
        };
        
        const suggestions = service.getSuggestions('name !contains ', 15, configWithNegatedOps);
        expect(suggestions).toBeDefined();
        expect(Array.isArray(suggestions)).toBe(true);
      });

      it('should handle negated in operator (!in)', () => {
        const suggestions = service.getSuggestions('status !in ', 11, config);
        expect(suggestions.length).toBe(3);
        expect(suggestions.every(s => s.type === 'value')).toBe(true);
      });

      it('should suggest fields after EXISTS operator', () => {
        const configWithExists: QueryBuilderConfig = {
          fields: {
            name: {
              name: 'Name',
              type: 'string',
              operators: ['=', '!=', 'exists']
            }
          }
        };
        
        const suggestions = service.getSuggestions('name EXISTS ', 12, configWithExists);
        expect(suggestions).toBeDefined();
        expect(Array.isArray(suggestions)).toBe(true);
      });

      it('should suggest fields after !EXISTS operator', () => {
        const configWithExists: QueryBuilderConfig = {
          fields: {
            name: {
              name: 'Name',
              type: 'string',
              operators: ['=', '!=', '!exists']
            }
          }
        };
        
        const suggestions = service.getSuggestions('name !EXISTS ', 13, configWithExists);
        expect(suggestions).toBeDefined();
        expect(Array.isArray(suggestions)).toBe(true);
      });
    });

    describe('Complex Query States', () => {
      it('should handle query with multiple conditions', () => {
        const suggestions = service.getSuggestions('name = "test" & age > 25 & ', 27, config);
        expect(suggestions.length).toBeGreaterThan(0);
        expect(suggestions.every(s => s.type === 'field')).toBe(true);
      });

      it('should handle query with OR and AND operators', () => {
        const suggestions = service.getSuggestions('name = "test" | age > 25 & ', 27, config);
        expect(suggestions.length).toBeGreaterThan(0);
        expect(suggestions.every(s => s.type === 'field')).toBe(true);
      });

      it('should handle query with parentheses and logical operators', () => {
        const suggestions = service.getSuggestions('(name = "test" | age > 25) & ', 29, config);
        expect(suggestions.length).toBeGreaterThan(0);
        expect(suggestions.every(s => s.type === 'field')).toBe(true);
      });

      it('should handle cursor at various positions in complex query', () => {
        const query = 'name = "test" & (age > 25 | status = "active")';
        
        // At start
        const suggestions1 = service.getSuggestions(query, 0, config);
        expect(suggestions1.every(s => s.type === 'field')).toBe(true);
        
        // After first condition
        const suggestions2 = service.getSuggestions(query, 13, config);
        expect(suggestions2).toBeDefined();
        
        // Inside parentheses
        const suggestions3 = service.getSuggestions(query, 27, config);
        expect(suggestions3).toBeDefined();
      });

      it('should handle incomplete query with trailing operator', () => {
        const suggestions = service.getSuggestions('name = "test" & age >', 20, config);
        expect(suggestions).toBeDefined();
        expect(Array.isArray(suggestions)).toBe(true);
      });

      it('should handle query with IN operator and multiple values', () => {
        const suggestions = service.getSuggestions('status IN ', 10, config);
        expect(suggestions.length).toBe(3);
        expect(suggestions.every(s => s.type === 'value')).toBe(true);
      });

      it('should handle whitespace-only query', () => {
        const suggestions = service.getSuggestions('   ', 3, config);
        expect(suggestions.length).toBeGreaterThan(0);
        expect(suggestions.every(s => s.type === 'field')).toBe(true);
      });

      it('should handle query with multiple spaces between tokens', () => {
        const suggestions = service.getSuggestions('name   =   "test"   &   ', 24, config);
        expect(suggestions.length).toBeGreaterThan(0);
        expect(suggestions.every(s => s.type === 'field')).toBe(true);
      });
    });

    describe('Cursor Position Edge Cases', () => {
      it('should handle cursor at position 0', () => {
        const suggestions = service.getSuggestions('name = "test"', 0, config);
        expect(suggestions.length).toBeGreaterThan(0);
        expect(suggestions.every(s => s.type === 'field')).toBe(true);
      });

      it('should handle cursor beyond query length', () => {
        const query = 'name = "test"';
        const suggestions = service.getSuggestions(query, query.length + 10, config);
        expect(suggestions).toBeDefined();
        expect(Array.isArray(suggestions)).toBe(true);
      });

      it('should handle cursor at exact end of query', () => {
        const query = 'name = "test"';
        const suggestions = service.getSuggestions(query, query.length, config);
        expect(suggestions).toBeDefined();
        expect(Array.isArray(suggestions)).toBe(true);
      });

      it('should handle cursor in middle of field name', () => {
        const suggestions = service.getSuggestions('name', 2, config);
        expect(suggestions.length).toBeGreaterThan(0);
        expect(suggestions.some(s => s.value === 'name')).toBe(true);
      });

      it('should handle cursor in middle of operator', () => {
        const suggestions = service.getSuggestions('name cont', 9, config);
        expect(suggestions).toBeDefined();
        expect(Array.isArray(suggestions)).toBe(true);
      });

      it('should handle cursor right before logical operator', () => {
        const suggestions = service.getSuggestions('name = "test" &', 15, config);
        expect(suggestions).toBeDefined();
        expect(Array.isArray(suggestions)).toBe(true);
      });

      it('should handle cursor right after logical operator', () => {
        const suggestions = service.getSuggestions('name = "test" &', 15, config);
        expect(suggestions).toBeDefined();
        expect(Array.isArray(suggestions)).toBe(true);
      });
    });
  });

  describe('Field Suggestions', () => {
    it('should return all fields when no prefix', () => {
      const suggestions = service.getSuggestions('', 0, config);
      expect(suggestions.length).toBe(4);
      expect(suggestions.map(s => s.value).sort()).toEqual(['active', 'age', 'name', 'status']);
    });

    it('should filter fields by prefix (case-insensitive)', () => {
      const suggestions = service.getSuggestions('na', 2, config);
      expect(suggestions.length).toBe(1);
      expect(suggestions[0].value).toBe('name');
    });

    it('should include field metadata', () => {
      const suggestions = service.getSuggestions('', 0, config);
      const nameSuggestion = suggestions.find(s => s.value === 'name');
      expect(nameSuggestion?.metadata?.fieldType).toBe('string');
    });
  });

  describe('Operator Suggestions', () => {
    it('should return operators for string field', () => {
      const suggestions = service.getSuggestions('name ', 5, config);
      expect(suggestions.length).toBeGreaterThan(0);
      expect(suggestions.some(s => s.value.toLowerCase() === 'contains')).toBe(true);
      expect(suggestions.some(s => s.value.toLowerCase() === 'like')).toBe(true);
    });

    it('should return operators for number field', () => {
      const suggestions = service.getSuggestions('age ', 4, config);
      expect(suggestions.length).toBeGreaterThan(0);
      expect(suggestions.some(s => s.value === '>')).toBe(true);
      expect(suggestions.some(s => s.value === '<=')).toBe(true);
    });

    it('should return operators for boolean field', () => {
      const suggestions = service.getSuggestions('active ', 7, config);
      expect(suggestions.length).toBeGreaterThan(0);
      expect(suggestions.some(s => s.value === '=')).toBe(true);
      expect(suggestions.some(s => s.value === '!=')).toBe(true);
    });

    it('should return operators for category field', () => {
      const suggestions = service.getSuggestions('status ', 7, config);
      expect(suggestions.length).toBeGreaterThan(0);
      expect(suggestions.some(s => s.value === '=')).toBe(true);
      expect(suggestions.some(s => s.value.toLowerCase() === 'in')).toBe(true);
    });

    it('should return default operators for date field', () => {
      const configWithDate: QueryBuilderConfig = {
        fields: {
          createdDate: {
            name: 'Created Date',
            type: 'date'
          }
        }
      };
      
      const suggestions = service.getSuggestions('createdDate ', 12, configWithDate);
      expect(suggestions.length).toBeGreaterThan(0);
      expect(suggestions.some(s => s.value === '=')).toBe(true);
      expect(suggestions.some(s => s.value === '>')).toBe(true);
      expect(suggestions.some(s => s.value === '<')).toBe(true);
      expect(suggestions.some(s => s.value.toLowerCase() === 'exists')).toBe(true);
    });

    it('should include readable names for operators', () => {
      const suggestions = service.getSuggestions('name ', 5, config);
      const equalsSuggestion = suggestions.find(s => s.value === '=');
      
      expect(equalsSuggestion).toBeDefined();
      expect(equalsSuggestion?.display).toContain('equals');
    });

    it('should convert word operators to uppercase', () => {
      const configWithWordOps: QueryBuilderConfig = {
        fields: {
          name: {
            name: 'Name',
            type: 'string',
            operators: ['contains', 'like', 'in', 'exists']
          }
        }
      };
      
      const suggestions = service.getSuggestions('name ', 5, configWithWordOps);
      expect(suggestions.some(s => s.value === 'CONTAINS')).toBe(true);
      expect(suggestions.some(s => s.value === 'LIKE')).toBe(true);
      expect(suggestions.some(s => s.value === 'IN')).toBe(true);
      expect(suggestions.some(s => s.value === 'EXISTS')).toBe(true);
    });

    it('should keep symbol operators as-is', () => {
      const suggestions = service.getSuggestions('age ', 4, config);
      expect(suggestions.some(s => s.value === '=')).toBe(true);
      expect(suggestions.some(s => s.value === '!=')).toBe(true);
      expect(suggestions.some(s => s.value === '>')).toBe(true);
      expect(suggestions.some(s => s.value === '>=')).toBe(true);
      expect(suggestions.some(s => s.value === '<')).toBe(true);
      expect(suggestions.some(s => s.value === '<=')).toBe(true);
    });

    it('should filter operators by prefix', () => {
      const configWithManyOps: QueryBuilderConfig = {
        fields: {
          name: {
            name: 'Name',
            type: 'string',
            operators: ['=', '!=', 'contains', '!contains', 'like', '!like', 'in', '!in', 'exists']
          }
        }
      };
      
      const suggestions = service.getSuggestions('name cont', 9, configWithManyOps);
      expect(suggestions.length).toBeGreaterThan(0);
      expect(suggestions.every(s => 
        s.value.toLowerCase().includes('cont') || 
        s.display.toLowerCase().includes('cont')
      )).toBe(true);
    });

    it('should handle negated operators', () => {
      const configWithNegated: QueryBuilderConfig = {
        fields: {
          name: {
            name: 'Name',
            type: 'string',
            operators: ['!contains', '!like', '!in', '!exists']
          }
        }
      };
      
      const suggestions = service.getSuggestions('name ', 5, configWithNegated);
      expect(suggestions.some(s => s.value === '!CONTAINS')).toBe(true);
      expect(suggestions.some(s => s.value === '!LIKE')).toBe(true);
      expect(suggestions.some(s => s.value === '!IN')).toBe(true);
      expect(suggestions.some(s => s.value === '!EXISTS')).toBe(true);
    });
  });

  describe('Value Suggestions', () => {
    it('should suggest boolean values for boolean field', () => {
      const suggestions = service.getSuggestions('active = ', 9, config);
      expect(suggestions.length).toBe(2);
      expect(suggestions.map(s => s.value).sort()).toEqual(['false', 'true']);
    });

    it('should suggest options for category field', () => {
      const suggestions = service.getSuggestions('status = ', 9, config);
      expect(suggestions.length).toBe(3);
      expect(suggestions.some(s => s.value === 'active')).toBe(true);
      expect(suggestions.some(s => s.value === 'inactive')).toBe(true);
      expect(suggestions.some(s => s.value === 'pending')).toBe(true);
    });

    it('should return empty for free-text fields', () => {
      const suggestions = service.getSuggestions('name = ', 7, config);
      expect(suggestions.length).toBe(0);
    });

    it('should return empty for number fields without options', () => {
      const suggestions = service.getSuggestions('age = ', 6, config);
      expect(suggestions.length).toBe(0);
    });

    it('should filter value suggestions by prefix', () => {
      const suggestions = service.getSuggestions('status = act', 12, config);
      expect(suggestions.length).toBe(1);
      expect(suggestions[0].value).toBe('active');
    });

    it('should filter value suggestions case-insensitively', () => {
      const suggestions = service.getSuggestions('status = ACT', 12, config);
      expect(suggestions.length).toBe(1);
      expect(suggestions[0].value).toBe('active');
    });

    it('should handle IN operator with multi-value context', () => {
      const suggestions = service.getSuggestions('status IN ', 10, config);
      expect(suggestions.length).toBe(3);
      expect(suggestions.every(s => s.type === 'value')).toBe(true);
    });

    it('should handle !IN operator with multi-value context', () => {
      const suggestions = service.getSuggestions('status !IN ', 11, config);
      expect(suggestions.length).toBe(3);
      expect(suggestions.every(s => s.type === 'value')).toBe(true);
    });

    it('should handle comma-separated values in IN operator', () => {
      const suggestions = service.getSuggestions('status IN active,', 17, config);
      expect(suggestions.length).toBe(3);
      expect(suggestions.every(s => s.type === 'value')).toBe(true);
    });

    it('should filter after comma in IN operator', () => {
      const suggestions = service.getSuggestions('status IN active, pend', 22, config);
      expect(suggestions.length).toBe(1);
      expect(suggestions[0].value).toBe('pending');
    });

    it('should handle field with categorySource', () => {
      const categorySourceMock = jasmine.createSpy('categorySource').and.returnValue(['cat1', 'cat2', 'cat3']);
      
      const configWithCategorySource: QueryBuilderConfig = {
        fields: {
          category: {
            name: 'Category',
            type: 'category',
            operators: ['=', '!='],
            categorySource: categorySourceMock
          }
        }
      };
      
      const suggestions = service.getSuggestions('category = ', 11, configWithCategorySource);
      
      expect(categorySourceMock).toHaveBeenCalled();
      expect(suggestions.length).toBe(3);
      expect(suggestions.map(s => s.value)).toEqual(['cat1', 'cat2', 'cat3']);
    });

    it('should handle categorySource returning null values', () => {
      const categorySourceMock = jasmine.createSpy('categorySource').and.returnValue([null, 'valid', undefined, 'another']);
      
      const configWithCategorySource: QueryBuilderConfig = {
        fields: {
          category: {
            name: 'Category',
            type: 'category',
            operators: ['=', '!='],
            categorySource: categorySourceMock
          }
        }
      };
      
      const suggestions = service.getSuggestions('category = ', 11, configWithCategorySource);
      
      expect(suggestions.length).toBe(2);
      expect(suggestions.map(s => s.value)).toEqual(['valid', 'another']);
    });

    it('should handle categorySource returning non-array', () => {
      const categorySourceMock = jasmine.createSpy('categorySource').and.returnValue(null);
      
      const configWithCategorySource: QueryBuilderConfig = {
        fields: {
          category: {
            name: 'Category',
            type: 'category',
            operators: ['=', '!='],
            categorySource: categorySourceMock
          }
        }
      };
      
      const suggestions = service.getSuggestions('category = ', 11, configWithCategorySource);
      
      expect(suggestions.length).toBe(0);
    });

    it('should include option display names', () => {
      const suggestions = service.getSuggestions('status = ', 9, config);
      const activeSuggestion = suggestions.find(s => s.value === 'active');
      
      expect(activeSuggestion?.display).toBe('Active');
    });

    it('should handle options with same name and value', () => {
      const configWithSameNameValue: QueryBuilderConfig = {
        fields: {
          type: {
            name: 'Type',
            type: 'category',
            operators: ['=', '!='],
            options: [
              { name: 'TypeA', value: 'TypeA' },
              { name: 'TypeB', value: 'TypeB' }
            ]
          }
        }
      };
      
      const suggestions = service.getSuggestions('type = ', 7, configWithSameNameValue);
      
      expect(suggestions.length).toBe(2);
      expect(suggestions[0].display).toBe('TypeA');
      expect(suggestions[0].metadata?.description).toBeUndefined();
    });

    it('should handle numeric option values', () => {
      const configWithNumericOptions: QueryBuilderConfig = {
        fields: {
          priority: {
            name: 'Priority',
            type: 'number',
            operators: ['=', '!=', 'in'],
            options: [
              { name: 'Low', value: 1 },
              { name: 'Medium', value: 2 },
              { name: 'High', value: 3 }
            ]
          }
        }
      };
      
      const suggestions = service.getSuggestions('priority = ', 11, configWithNumericOptions);
      
      expect(suggestions.length).toBe(3);
      expect(suggestions.map(s => s.value)).toEqual(['1', '2', '3']);
      expect(suggestions.map(s => s.display)).toEqual(['Low', 'Medium', 'High']);
    });

    it('should handle date fields without options', () => {
      const configWithDate: QueryBuilderConfig = {
        fields: {
          createdDate: {
            name: 'Created Date',
            type: 'date',
            operators: ['=', '>', '<']
          }
        }
      };
      
      const suggestions = service.getSuggestions('createdDate = ', 14, configWithDate);
      expect(suggestions.length).toBe(0);
    });
  });

  describe('Edge Cases', () => {
    it('should handle cursor at start', () => {
      const suggestions = service.getSuggestions('name = "test"', 0, config);
      expect(suggestions.length).toBeGreaterThan(0);
      expect(suggestions.every(s => s.type === 'field')).toBe(true);
    });

    it('should handle malformed query gracefully', () => {
      const suggestions = service.getSuggestions('name = = =', 10, config);
      expect(suggestions).toBeDefined();
      expect(Array.isArray(suggestions)).toBe(true);
    });

    it('should handle query with negation', () => {
      const suggestions = service.getSuggestions('!', 1, config);
      expect(suggestions.length).toBeGreaterThan(0);
      expect(suggestions.every(s => s.type === 'field')).toBe(true);
    });
  });

  describe('Error Handling and Fallbacks', () => {
    it('should handle missing QueryBuilderConfig gracefully', () => {
      const suggestions = service.getSuggestions('test', 4, null as any);
      expect(suggestions).toBeDefined();
      expect(Array.isArray(suggestions)).toBe(true);
      expect(suggestions.length).toBeGreaterThan(0);
      // Should return minimal suggestions
      expect(suggestions.some(s => s.value === 'EXISTS')).toBe(true);
    });

    it('should handle empty fields configuration', () => {
      const emptyConfig: QueryBuilderConfig = {
        fields: {}
      };
      const suggestions = service.getSuggestions('test', 4, emptyConfig);
      expect(suggestions).toBeDefined();
      expect(Array.isArray(suggestions)).toBe(true);
      // Should return minimal suggestions
      expect(suggestions.some(s => s.value === 'EXISTS')).toBe(true);
    });

    it('should handle missing fields property', () => {
      const configWithoutFields: QueryBuilderConfig = {} as any;
      const suggestions = service.getSuggestions('test', 4, configWithoutFields);
      expect(suggestions).toBeDefined();
      expect(Array.isArray(suggestions)).toBe(true);
      // Should return minimal suggestions
      expect(suggestions.some(s => s.value === 'EXISTS')).toBe(true);
    });

    it('should handle incomplete field configuration', () => {
      const incompleteConfig: QueryBuilderConfig = {
        fields: {
          name: null as any,
          age: {
            name: 'Age',
            type: 'number'
          }
        }
      };
      const suggestions = service.getSuggestions('', 0, incompleteConfig);
      expect(suggestions).toBeDefined();
      expect(Array.isArray(suggestions)).toBe(true);
      // Should skip null field but include valid field
      expect(suggestions.some(s => s.value === 'age')).toBe(true);
      expect(suggestions.some(s => s.value === 'name')).toBe(false);
    });

    it('should handle tokenization errors in malformed queries', () => {
      // Unterminated string
      const suggestions1 = service.getSuggestions('name = "unterminated', 19, config);
      expect(suggestions1).toBeDefined();
      expect(Array.isArray(suggestions1)).toBe(true);

      // Invalid syntax
      const suggestions2 = service.getSuggestions('name & & |', 10, config);
      expect(suggestions2).toBeDefined();
      expect(Array.isArray(suggestions2)).toBe(true);
    });

    it('should continue providing suggestions even when validateBql would return false', () => {
      // Incomplete query that would fail validation
      const suggestions = service.getSuggestions('name = ', 7, config);
      expect(suggestions).toBeDefined();
      expect(Array.isArray(suggestions)).toBe(true);
      // Should still provide suggestions (empty for free-text field)
    });

    it('should handle field with missing type', () => {
      const configWithMissingType: QueryBuilderConfig = {
        fields: {
          noType: {
            name: 'No Type'
          } as any
        }
      };
      const suggestions = service.getSuggestions('noType ', 7, configWithMissingType);
      expect(suggestions).toBeDefined();
      expect(Array.isArray(suggestions)).toBe(true);
      // Should fall back to default operators
      expect(suggestions.length).toBeGreaterThan(0);
    });

    it('should handle malformed options array', () => {
      const configWithBadOptions: QueryBuilderConfig = {
        fields: {
          status: {
            name: 'Status',
            type: 'category',
            operators: ['='],
            options: [
              null as any,
              { name: 'Valid', value: 'valid' },
              { value: undefined } as any
            ]
          }
        }
      };
      const suggestions = service.getSuggestions('status = ', 9, configWithBadOptions);
      expect(suggestions).toBeDefined();
      expect(Array.isArray(suggestions)).toBe(true);
      // Should only include valid option
      expect(suggestions.length).toBe(1);
      expect(suggestions[0].value).toBe('valid');
    });

    it('should handle errors in getSuggestions without crashing', () => {
      // Pass invalid cursor position
      const suggestions = service.getSuggestions('test', -1, config);
      expect(suggestions).toBeDefined();
      expect(Array.isArray(suggestions)).toBe(true);
    });

    it('should handle null or undefined query', () => {
      const suggestions1 = service.getSuggestions(null as any, 0, config);
      expect(suggestions1).toBeDefined();
      expect(Array.isArray(suggestions1)).toBe(true);

      const suggestions2 = service.getSuggestions(undefined as any, 0, config);
      expect(suggestions2).toBeDefined();
      expect(Array.isArray(suggestions2)).toBe(true);
    });
  });

  describe('Prefix Filtering', () => {
    it('should filter suggestions case-insensitively', () => {
      const suggestions = service.getSuggestions('NA', 2, config);
      expect(suggestions.length).toBe(1);
      expect(suggestions[0].value).toBe('name');
    });

    it('should match partial strings', () => {
      const suggestions = service.getSuggestions('act', 3, config);
      expect(suggestions.length).toBe(1);
      expect(suggestions[0].value).toBe('active');
    });
  });

  describe('Quoted Strings and Regex Patterns', () => {
    describe('Quoted Strings', () => {
      it('should not provide suggestions inside quoted strings', () => {
        const suggestions = service.getSuggestions('name = "test', 12, config);
        expect(suggestions.length).toBe(0);
      });

      it('should not provide suggestions in middle of quoted string', () => {
        const suggestions = service.getSuggestions('name = "some text here', 18, config);
        expect(suggestions.length).toBe(0);
      });

      it('should provide suggestions after closing quote', () => {
        const suggestions = service.getSuggestions('name = "test" ', 14, config);
        expect(suggestions.length).toBeGreaterThan(0);
        // Should suggest logical operators or fields for next condition
      });

      it('should handle escaped quotes inside strings', () => {
        const suggestions = service.getSuggestions('name = "test\\"quote', 19, config);
        expect(suggestions.length).toBe(0);
      });

      it('should provide suggestions after properly closed string with escaped quote', () => {
        const suggestions = service.getSuggestions('name = "test\\"quote" ', 21, config);
        expect(suggestions.length).toBeGreaterThan(0);
      });

      it('should handle multiple quoted strings in query', () => {
        const suggestions = service.getSuggestions('name = "first" & status = "sec', 30, config);
        expect(suggestions.length).toBe(0);
      });

      it('should provide suggestions between quoted strings', () => {
        const suggestions = service.getSuggestions('name = "first" & ', 17, config);
        expect(suggestions.length).toBeGreaterThan(0);
        expect(suggestions.every(s => s.type === 'field')).toBe(true);
      });

      it('should handle cursor at opening quote', () => {
        const suggestions = service.getSuggestions('name = "', 8, config);
        expect(suggestions.length).toBe(0);
      });

      it('should handle empty quoted string', () => {
        const suggestions = service.getSuggestions('name = ""', 9, config);
        expect(suggestions.length).toBeGreaterThan(0);
      });

      it('should handle cursor right after opening quote', () => {
        const suggestions = service.getSuggestions('name = "t', 9, config);
        expect(suggestions.length).toBe(0);
      });
    });

    describe('Regex Patterns', () => {
      it('should not provide suggestions inside regex pattern', () => {
        const suggestions = service.getSuggestions('name LIKE /test', 15, config);
        expect(suggestions.length).toBe(0);
      });

      it('should not provide suggestions in middle of regex pattern', () => {
        const suggestions = service.getSuggestions('name LIKE /some.*pattern', 24, config);
        expect(suggestions.length).toBe(0);
      });

      it('should not provide suggestions in regex flags', () => {
        const suggestions = service.getSuggestions('name LIKE /test/i', 17, config);
        expect(suggestions.length).toBe(0);
      });

      it('should not provide suggestions in regex with multiple flags', () => {
        const suggestions = service.getSuggestions('name LIKE /test/gi', 18, config);
        expect(suggestions.length).toBe(0);
      });

      it('should provide suggestions after closing regex with flags', () => {
        const suggestions = service.getSuggestions('name LIKE /test/gi ', 19, config);
        expect(suggestions.length).toBeGreaterThan(0);
      });

      it('should provide suggestions after closing regex without flags', () => {
        const suggestions = service.getSuggestions('name LIKE /test/ ', 17, config);
        expect(suggestions.length).toBeGreaterThan(0);
      });

      it('should handle escaped forward slash in regex', () => {
        const suggestions = service.getSuggestions('name LIKE /test\\/path', 21, config);
        expect(suggestions.length).toBe(0);
      });

      it('should provide suggestions after properly closed regex with escaped slash', () => {
        const suggestions = service.getSuggestions('name LIKE /test\\/path/ ', 23, config);
        expect(suggestions.length).toBeGreaterThan(0);
      });

      it('should handle regex at start of query', () => {
        const suggestions = service.getSuggestions('name LIKE /^test', 16, config);
        expect(suggestions.length).toBe(0);
      });

      it('should handle regex with special characters', () => {
        const suggestions = service.getSuggestions('name LIKE /[a-z]+\\d{2,4}/', 25, config);
        expect(suggestions.length).toBeGreaterThan(0);
      });

      it('should handle cursor at opening slash', () => {
        const suggestions = service.getSuggestions('name LIKE /', 11, config);
        expect(suggestions.length).toBe(0);
      });

      it('should handle multiple regex patterns in query', () => {
        const suggestions = service.getSuggestions('name LIKE /test/ & status LIKE /pat', 35, config);
        expect(suggestions.length).toBe(0);
      });

      it('should provide suggestions between regex patterns', () => {
        const suggestions = service.getSuggestions('name LIKE /test/ & ', 19, config);
        expect(suggestions.length).toBeGreaterThan(0);
        expect(suggestions.every(s => s.type === 'field')).toBe(true);
      });
    });

    describe('Mixed Quoted Strings and Regex', () => {
      it('should handle query with both quoted strings and regex', () => {
        const suggestions1 = service.getSuggestions('name = "test" & status LIKE /pat', 32, config);
        expect(suggestions1.length).toBe(0);

        const suggestions2 = service.getSuggestions('name = "test" & status LIKE /pattern/ & ', 41, config);
        expect(suggestions2.length).toBeGreaterThan(0);
      });

      it('should not confuse quotes with regex slashes', () => {
        const suggestions = service.getSuggestions('name = "test/path"', 18, config);
        expect(suggestions.length).toBeGreaterThan(0);
      });

      it('should not confuse regex slashes with quotes', () => {
        const suggestions = service.getSuggestions('name LIKE /test"quote/', 22, config);
        expect(suggestions.length).toBeGreaterThan(0);
      });
    });
  });

  describe('QueryBuilderConfig Integration', () => {
    describe('field.operators configuration', () => {
      it('should respect field.operators when specified', () => {
        const suggestions = service.getSuggestions('name ', 5, config);
        const operatorValues = suggestions.map(s => s.value.toLowerCase());
        
        // Should include operators from config
        expect(operatorValues).toContain('=');
        expect(operatorValues).toContain('!=');
        expect(operatorValues).toContain('contains');
        
        // Should not include operators not in config
        expect(operatorValues).not.toContain('exists');
      });

      it('should fall back to default operators when field.operators not specified', () => {
        const configWithoutOps: QueryBuilderConfig = {
          fields: {
            email: {
              name: 'Email',
              type: 'string'
            }
          }
        };
        
        const suggestions = service.getSuggestions('email ', 6, configWithoutOps);
        const operatorValues = suggestions.map(s => s.value.toLowerCase());
        
        // Should include default string operators
        expect(operatorValues).toContain('=');
        expect(operatorValues).toContain('contains');
        expect(operatorValues).toContain('exists');
      });
    });

    describe('field.options configuration', () => {
      it('should use field.options for value suggestions', () => {
        const suggestions = service.getSuggestions('status = ', 9, config);
        
        expect(suggestions.length).toBe(3);
        expect(suggestions.some(s => s.value === 'active')).toBe(true);
        expect(suggestions.some(s => s.value === 'inactive')).toBe(true);
        expect(suggestions.some(s => s.value === 'pending')).toBe(true);
      });

      it('should include option names in display', () => {
        const suggestions = service.getSuggestions('status = ', 9, config);
        const activeSuggestion = suggestions.find(s => s.value === 'active');
        
        expect(activeSuggestion?.display).toBe('Active');
      });
    });

    describe('field.categorySource configuration', () => {
      it('should call field.categorySource for dynamic value suggestions', () => {
        const categorySourceMock = jasmine.createSpy('categorySource').and.returnValue(['cat1', 'cat2', 'cat3']);
        
        const configWithCategorySource: QueryBuilderConfig = {
          fields: {
            category: {
              name: 'Category',
              type: 'category',
              operators: ['=', '!='],
              categorySource: categorySourceMock
            }
          }
        };
        
        const suggestions = service.getSuggestions('category = ', 11, configWithCategorySource);
        
        expect(categorySourceMock).toHaveBeenCalled();
        expect(suggestions.length).toBe(3);
        expect(suggestions.map(s => s.value)).toEqual(['cat1', 'cat2', 'cat3']);
      });

      it('should handle categorySource errors gracefully', () => {
        const categorySourceMock = jasmine.createSpy('categorySource').and.throwError('API Error');
        
        const configWithCategorySource: QueryBuilderConfig = {
          fields: {
            category: {
              name: 'Category',
              type: 'category',
              operators: ['=', '!='],
              categorySource: categorySourceMock
            }
          }
        };
        
        const suggestions = service.getSuggestions('category = ', 11, configWithCategorySource);
        
        expect(suggestions.length).toBe(0);
      });
    });

    describe('field.nullable configuration', () => {
      it('should add "is null" and "is not null" operators for nullable fields', () => {
        const configWithNullable: QueryBuilderConfig = {
          fields: {
            description: {
              name: 'Description',
              type: 'string',
              nullable: true,
              operators: ['=', '!=', 'contains']
            }
          }
        };
        
        const suggestions = service.getSuggestions('description ', 12, configWithNullable);
        const operatorValues = suggestions.map(s => s.value.toLowerCase());
        
        expect(operatorValues).toContain('is null');
        expect(operatorValues).toContain('is not null');
      });

      it('should not add null operators for non-nullable fields', () => {
        const suggestions = service.getSuggestions('name ', 5, config);
        const operatorValues = suggestions.map(s => s.value.toLowerCase());
        
        expect(operatorValues).not.toContain('is null');
        expect(operatorValues).not.toContain('is not null');
      });
    });

    describe('computed fields', () => {
      it('should exclude computed fields from field suggestions', () => {
        const configWithComputed: QueryBuilderConfig = {
          fields: {
            name: {
              name: 'Name',
              type: 'string',
              operators: ['=', '!=']
            },
            fullName: {
              name: 'Full Name',
              type: 'computed',
              operators: []
            }
          }
        };
        
        const suggestions = service.getSuggestions('', 0, configWithComputed);
        
        expect(suggestions.length).toBe(1);
        expect(suggestions[0].value).toBe('name');
        expect(suggestions.some(s => s.value === 'fullName')).toBe(false);
      });
    });

    describe('config.getOperators', () => {
      it('should use config.getOperators when available', () => {
        const getOperatorsMock = jasmine.createSpy('getOperators').and.returnValue(['=', '!=', 'custom']);
        
        const configWithGetOperators: QueryBuilderConfig = {
          fields: {
            name: {
              name: 'Name',
              type: 'string',
              operators: ['=', '!=', 'contains']
            }
          },
          getOperators: getOperatorsMock
        };
        
        const suggestions = service.getSuggestions('name ', 5, configWithGetOperators);
        
        expect(getOperatorsMock).toHaveBeenCalledWith('name', jasmine.any(Object));
        const operatorValues = suggestions.map(s => s.value.toLowerCase());
        expect(operatorValues).toContain('custom');
      });

      it('should handle config.getOperators errors gracefully', () => {
        const getOperatorsMock = jasmine.createSpy('getOperators').and.throwError('Error');
        
        const configWithGetOperators: QueryBuilderConfig = {
          fields: {
            name: {
              name: 'Name',
              type: 'string',
              operators: ['=', '!=']
            }
          },
          getOperators: getOperatorsMock
        };
        
        const suggestions = service.getSuggestions('name ', 5, configWithGetOperators);
        
        // Should fall back to field.operators
        expect(suggestions.length).toBeGreaterThan(0);
      });
    });

    describe('config.getOptions', () => {
      it('should use config.getOptions when available', () => {
        const getOptionsMock = jasmine.createSpy('getOptions').and.returnValue([
          { name: 'Option 1', value: 'opt1' },
          { name: 'Option 2', value: 'opt2' }
        ]);
        
        const configWithGetOptions: QueryBuilderConfig = {
          fields: {
            status: {
              name: 'Status',
              type: 'category',
              operators: ['=', '!=']
            }
          },
          getOptions: getOptionsMock
        };
        
        const suggestions = service.getSuggestions('status = ', 9, configWithGetOptions);
        
        expect(getOptionsMock).toHaveBeenCalledWith('status');
        expect(suggestions.length).toBe(2);
        expect(suggestions.map(s => s.value)).toEqual(['opt1', 'opt2']);
      });

      it('should fall back to field.options if config.getOptions returns empty', () => {
        const getOptionsMock = jasmine.createSpy('getOptions').and.returnValue([]);
        
        const configWithGetOptions: QueryBuilderConfig = {
          fields: {
            status: {
              name: 'Status',
              type: 'category',
              operators: ['=', '!='],
              options: [
                { name: 'Active', value: 'active' }
              ]
            }
          },
          getOptions: getOptionsMock
        };
        
        const suggestions = service.getSuggestions('status = ', 9, configWithGetOptions);
        
        expect(suggestions.length).toBe(1);
        expect(suggestions[0].value).toBe('active');
      });

      it('should handle config.getOptions errors gracefully', () => {
        const getOptionsMock = jasmine.createSpy('getOptions').and.throwError('Error');
        
        const configWithGetOptions: QueryBuilderConfig = {
          fields: {
            status: {
              name: 'Status',
              type: 'category',
              operators: ['=', '!='],
              options: [
                { name: 'Active', value: 'active' }
              ]
            }
          },
          getOptions: getOptionsMock
        };
        
        const suggestions = service.getSuggestions('status = ', 9, configWithGetOptions);
        
        // Should fall back to field.options
        expect(suggestions.length).toBe(1);
        expect(suggestions[0].value).toBe('active');
      });
    });
  });
});
