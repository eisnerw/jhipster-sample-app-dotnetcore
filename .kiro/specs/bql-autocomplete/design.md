# Design Document

## Overview

This design implements an intelligent autocomplete system for the BQL query input component. The autocomplete will parse the current query text, determine the context (field name, operator, or value), and provide relevant suggestions from the QueryLanguageSpec. The implementation will use Angular's reactive patterns and PrimeNG's AutoComplete component for the UI, integrated seamlessly into the existing query-input component.

## Architecture

### Component Structure

```
query-input.component.ts (existing - enhanced)
├── BqlAutocompleteService (new service)
│   ├── Context Analysis
│   ├── Suggestion Generation
│   └── Token Parsing
└── Autocomplete UI (PrimeNG AutoComplete)
    ├── Dropdown Rendering
    ├── Keyboard Navigation
    └── Selection Handling
```

### Key Design Decisions

1. **Service-Based Architecture**: Create a dedicated `BqlAutocompleteService` to handle context analysis and suggestion generation, keeping the component focused on UI concerns.

2. **Token-Based Parsing**: Leverage the existing `tokenize` function from `bql.ts` to understand query structure and determine cursor context.

3. **PrimeNG AutoComplete**: Use PrimeNG's AutoComplete component for consistent UI/UX with the existing application, rather than building a custom dropdown.

4. **Reactive Suggestions**: Use RxJS observables to debounce input changes and generate suggestions asynchronously.

5. **Context-Aware Logic**: Implement a state machine to determine whether the user is typing a field name, operator, or value based on cursor position and surrounding tokens.

## Components and Interfaces

### BqlAutocompleteService

**Location**: `src/JhipsterSampleApplication/ClientApp/projects/popup-ngx-query-builder/src/lib/services/bql-autocomplete.service.ts`

**Responsibilities**:
- Analyze query text and cursor position to determine context
- Generate appropriate suggestions based on context and QueryLanguageSpec
- Format suggestions with metadata (display text, value, type)

**Interface**:

```typescript
export interface AutocompleteSuggestion {
  value: string;           // The text to insert
  display: string;         // The text to show in dropdown
  type: 'field' | 'operator' | 'value' | 'keyword';
  metadata?: {
    fieldType?: string;    // For fields: string, number, date, etc.
    description?: string;  // Additional info to display
  };
}

export interface AutocompleteContext {
  type: 'field' | 'operator' | 'value' | 'unknown';
  currentField?: string;   // If in operator/value context
  currentOperator?: string; // If in value context
  prefix: string;          // Text typed so far in current token
  cursorPosition: number;
}

@Injectable()
export class BqlAutocompleteService {
  constructor() {}

  /**
   * Analyzes the query and cursor position to determine what suggestions to show
   */
  getSuggestions(
    query: string,
    cursorPosition: number,
    config: QueryBuilderConfig
  ): AutocompleteSuggestion[];

  /**
   * Determines the current context (field, operator, or value)
   */
  private analyzeContext(
    query: string,
    cursorPosition: number,
    config: QueryBuilderConfig
  ): AutocompleteContext;

  /**
   * Generates field name suggestions
   */
  private getFieldSuggestions(
    prefix: string,
    config: QueryBuilderConfig
  ): AutocompleteSuggestion[];

  /**
   * Generates operator suggestions for a specific field
   */
  private getOperatorSuggestions(
    fieldName: string,
    prefix: string,
    config: QueryBuilderConfig
  ): AutocompleteSuggestion[];

  /**
   * Generates value suggestions for a field/operator combination
   */
  private getValueSuggestions(
    fieldName: string,
    operator: string,
    prefix: string,
    config: QueryBuilderConfig
  ): AutocompleteSuggestion[];

  /**
   * Filters suggestions based on prefix match
   */
  private filterSuggestions(
    suggestions: AutocompleteSuggestion[],
    prefix: string
  ): AutocompleteSuggestion[];
}
```

### Enhanced QueryInputComponent

**Modifications to**: `src/JhipsterSampleApplication/ClientApp/projects/popup-ngx-query-builder/src/lib/query-input/query-input.component.ts`

**New Properties**:
```typescript
// Autocomplete state
autocompleteSuggestions: AutocompleteSuggestion[] = [];
showAutocomplete = false;
selectedSuggestionIndex = -1;
private autocompleteService: BqlAutocompleteService;

// Debounce input changes
private inputSubject = new Subject<string>();
private destroy$ = new Subject<void>();
```

**New Methods**:
```typescript
/**
 * Called on input change to trigger autocomplete
 */
onInputChange(event: Event): void;

/**
 * Handles keyboard navigation in autocomplete dropdown
 */
onAutocompleteKeydown(event: KeyboardEvent): void;

/**
 * Inserts selected suggestion into query
 */
selectSuggestion(suggestion: AutocompleteSuggestion): void;

/**
 * Closes autocomplete dropdown
 */
closeAutocomplete(): void;

/**
 * Updates autocomplete suggestions based on current input
 */
private updateAutocompleteSuggestions(): void;
```

### Template Changes

**Modifications to**: `src/JhipsterSampleApplication/ClientApp/projects/popup-ngx-query-builder/src/lib/query-input/query-input.component.html`

Replace the simple input with PrimeNG AutoComplete:

```html
@if (editing) {
  <p-autoComplete
    #editBox
    [(ngModel)]="query"
    [suggestions]="autocompleteSuggestions"
    (completeMethod)="updateAutocompleteSuggestions()"
    (onSelect)="selectSuggestion($event)"
    (ngModelChange)="onQueryChange()"
    (keydown.enter)="onEnter($event)"
    (keydown.arrowup)="onAutocompleteKeydown($event)"
    (keydown.arrowdown)="onAutocompleteKeydown($event)"
    (keydown.escape)="closeAutocomplete()"
    [dropdown]="false"
    [forceSelection]="false"
    [ngClass]="{ invalid: !validQuery }"
    field="display"
    [inputStyle]="{ flex: 1, border: 'none', outline: 'none', padding: '4px' }"
  >
    <ng-template let-suggestion pTemplate="item">
      <div class="autocomplete-item">
        <span class="suggestion-value">{{ suggestion.display }}</span>
        @if (suggestion.metadata?.fieldType) {
          <span class="suggestion-type">{{ suggestion.metadata.fieldType }}</span>
        }
        @if (suggestion.metadata?.description) {
          <span class="suggestion-desc">{{ suggestion.metadata.description }}</span>
        }
      </div>
    </ng-template>
  </p-autoComplete>
}
```

## Data Models

### Context Analysis Algorithm

The context analysis follows this logic:

1. **Tokenize** the query using the existing `tokenize` function from `bql.ts`
2. **Find cursor token**: Determine which token the cursor is currently in or after
3. **Analyze surrounding tokens**:
   - If cursor is at start or after `&`, `|`, `(`, `!`: suggest **fields**
   - If previous token is a field name: suggest **operators** for that field
   - If previous tokens are field + operator: suggest **values** for that field/operator
   - If inside a value token: continue suggesting values with current prefix

### Suggestion Generation

#### Field Suggestions
- Extract all fields from `config.fields`
- Include field name and type in display
- Filter by prefix match (case-insensitive)
- Sort alphabetically

#### Operator Suggestions
- Get allowed operators from field configuration
- Map operator symbols to readable names:
  - `=` → "equals"
  - `!=` → "not equals"
  - `contains` → "contains"
  - `!contains` → "does not contain"
  - `like` → "matches pattern"
  - `!like` → "does not match pattern"
  - `in` → "in list"
  - `!in` → "not in list"
  - `exists` → "exists"
  - `!exists` → "does not exist"
  - `>`, `>=`, `<`, `<=` → comparison operators
- Filter by prefix match
- Show both symbol and readable name

#### Value Suggestions
- For boolean fields: suggest `true`, `false`
- For fields with options: suggest option values
- For fields with categorySource: call categorySource function
- For date fields: suggest date format hints
- For string fields: no suggestions (free text)
- For number fields: no suggestions (free text)

## Error Handling

### Invalid Query States
- Continue providing suggestions even if `validateBql` returns false
- Use partial parsing to determine context when full parse fails
- Gracefully handle malformed queries by analyzing token-by-token

### Missing Configuration
- If QueryLanguageSpec is not provided, fall back to minimal suggestions
- If field configuration is incomplete, skip that field in suggestions
- Log warnings for configuration issues without breaking functionality

### Edge Cases
- **Cursor in middle of query**: Analyze tokens before cursor only
- **Multiple spaces**: Normalize whitespace when analyzing context
- **Incomplete operators**: Match partial operator text (e.g., "cont" matches "contains")
- **Quoted strings**: Detect when cursor is inside quotes and adjust context
- **Regex literals**: Detect `/pattern/` format and don't suggest inside regex

## Testing Strategy

### Unit Tests

**BqlAutocompleteService Tests** (`bql-autocomplete.service.spec.ts`):

1. **Context Analysis Tests**:
   - Test field context detection at query start
   - Test operator context after field name
   - Test value context after field + operator
   - Test context after logical operators (`&`, `|`)
   - Test context inside parentheses
   - Test context with negation (`!`)

2. **Suggestion Generation Tests**:
   - Test field suggestions with various prefixes
   - Test operator suggestions for different field types
   - Test value suggestions for boolean fields
   - Test value suggestions for fields with options
   - Test filtering by prefix (case-insensitive)
   - Test empty suggestions when no matches

3. **Edge Case Tests**:
   - Test with empty query
   - Test with cursor at various positions
   - Test with malformed queries
   - Test with special characters
   - Test with quoted strings
   - Test with regex patterns

### Integration Tests

**QueryInputComponent Tests** (`query-input.component.spec.ts`):

1. **Autocomplete UI Tests**:
   - Test autocomplete dropdown appears on input
   - Test keyboard navigation (arrow keys)
   - Test selection with Enter key
   - Test closing with Escape key
   - Test clicking outside closes dropdown

2. **Suggestion Insertion Tests**:
   - Test field name insertion
   - Test operator insertion with spacing
   - Test value insertion with quoting
   - Test cursor position after insertion

3. **Integration with Existing Features**:
   - Test history navigation still works when autocomplete is closed
   - Test validation still works with autocomplete
   - Test query builder dialog still works

### Manual Testing Scenarios

1. **Basic Autocomplete Flow**:
   - Type partial field name → see field suggestions
   - Select field → see operator suggestions
   - Select operator → see value suggestions (if applicable)
   - Complete query and verify it's valid

2. **Complex Query Building**:
   - Build query with multiple conditions using `&` and `|`
   - Use parentheses for grouping
   - Use negation with `!`
   - Verify autocomplete works at each step

3. **Different Entity Types**:
   - Test with supreme.json entity
   - Test with other entity JSON files
   - Verify suggestions adapt to different field configurations

4. **Keyboard-Only Workflow**:
   - Build entire query using only keyboard
   - Navigate suggestions with arrow keys
   - Select with Enter, cancel with Escape
   - Verify efficient workflow

## Performance Considerations

### Debouncing
- Debounce input changes by 150ms to avoid excessive suggestion generation
- Cancel pending suggestion requests when new input arrives

### Suggestion Caching
- Cache field and operator suggestions per QueryLanguageSpec
- Only regenerate when spec changes
- Value suggestions generated on-demand (may require API calls)

### Dropdown Rendering
- Limit displayed suggestions to 10-15 items
- Use virtual scrolling if suggestion list is very large
- Lazy-load value suggestions for fields with many options

### Memory Management
- Unsubscribe from observables in ngOnDestroy
- Clear suggestion cache when component is destroyed
- Avoid memory leaks from event listeners

## Accessibility

### Keyboard Support
- Arrow Up/Down: Navigate suggestions
- Enter: Select highlighted suggestion
- Escape: Close dropdown
- Tab: Select suggestion and move to next field (if applicable)

### Screen Reader Support
- Add ARIA labels to autocomplete dropdown
- Announce number of suggestions available
- Announce selected suggestion
- Provide text alternatives for icons

### Visual Indicators
- Clear visual highlight for selected suggestion
- Distinct styling for different suggestion types
- High contrast mode support
- Focus indicators for keyboard navigation

## Future Enhancements

### Phase 2 Features (Not in Initial Implementation)
1. **Fuzzy Matching**: Match suggestions even with typos
2. **Recent Values**: Show recently used values for fields
3. **Smart Suggestions**: Learn from user's query patterns
4. **Multi-Value Support**: Better UX for `in` operator with multiple values
5. **Inline Documentation**: Show field descriptions in autocomplete
6. **Query Templates**: Suggest common query patterns
7. **Syntax Highlighting**: Highlight different parts of query with colors

### API Integration
- If value suggestions require API calls (e.g., for category fields), implement async loading with loading indicators
- Cache API responses to minimize network requests
- Handle API errors gracefully

## Dependencies

### New Dependencies
- None (using existing PrimeNG and Angular dependencies)

### Existing Dependencies Used
- `@angular/core`: Component framework
- `primeng/autocomplete`: Autocomplete UI component
- `rxjs`: Reactive programming for debouncing
- Existing `bql.ts`: Token parsing and validation

## Migration and Rollout

### Backward Compatibility
- Autocomplete is an enhancement, not a breaking change
- Existing query input functionality remains unchanged
- Users can ignore autocomplete and type queries manually
- No changes to BQL syntax or validation

### Feature Flag (Optional)
- Consider adding an `@Input() enableAutocomplete = true` flag
- Allows disabling autocomplete if issues arise
- Can be controlled per-entity or globally

### Documentation Updates
- Update user documentation with autocomplete usage examples
- Add developer documentation for extending autocomplete
- Include screenshots/GIFs of autocomplete in action
