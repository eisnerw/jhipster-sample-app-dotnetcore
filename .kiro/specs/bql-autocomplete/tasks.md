# Implementation Plan

- [x] 1. Create BqlAutocompleteService with core context analysis





  - Create the service file with Injectable decorator and basic structure
  - Implement the AutocompleteSuggestion and AutocompleteContext interfaces
  - Implement analyzeContext method to determine if cursor is in field, operator, or value position
  - Use existing tokenize function from bql.ts to parse query structure
  - Handle edge cases: empty query, cursor at start, cursor after logical operators
  - _Requirements: 1.1, 1.2, 5.1, 5.2, 5.3_

- [x] 2. Implement suggestion generation methods in BqlAutocompleteService






- [x] 2.1 Implement getFieldSuggestions method

  - Extract fields from QueryBuilderConfig.fields
  - Create AutocompleteSuggestion objects with field name, display text, and type metadata
  - Implement case-insensitive prefix filtering
  - Sort suggestions alphabetically
  - _Requirements: 1.1, 1.2, 1.5_

- [x] 2.2 Implement getOperatorSuggestions method


  - Get allowed operators from field configuration
  - Map operator symbols to readable display names (e.g., "=" → "equals")
  - Filter by prefix match for both symbol and readable name
  - Handle field type-specific operators (string vs number vs date)
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

- [x] 2.3 Implement getValueSuggestions method

  - Handle boolean fields with true/false suggestions
  - Extract suggestions from field.options if available
  - Call field.categorySource if defined
  - Return empty array for free-text fields (string, number without options)
  - Handle "in" and "!in" operators for multi-value context
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 2.4 Implement main getSuggestions method


  - Call analyzeContext to determine current context
  - Route to appropriate suggestion method (field/operator/value)
  - Apply prefix filtering to results
  - Return formatted AutocompleteSuggestion array
  - _Requirements: 1.1, 2.1, 3.1, 5.3_

- [x] 3. Integrate autocomplete into QueryInputComponent





- [x] 3.1 Add autocomplete dependencies and properties


  - Import BqlAutocompleteService and AutoComplete module
  - Add service to component providers
  - Add properties: autocompleteSuggestions, showAutocomplete, selectedSuggestionIndex
  - Create inputSubject and destroy$ for reactive patterns
  - _Requirements: 6.1, 6.2_

- [x] 3.2 Implement autocomplete trigger logic


  - Create onInputChange method that debounces input by 150ms
  - Subscribe to inputSubject in ngOnInit
  - Call service.getSuggestions with current query and cursor position
  - Update autocompleteSuggestions array
  - Set showAutocomplete based on whether suggestions exist
  - _Requirements: 1.1, 1.2, 1.4, 6.3_

- [x] 3.3 Implement suggestion selection logic


  - Create selectSuggestion method to insert selected value at cursor position
  - Handle proper spacing for operators (add spaces before/after)
  - Handle quoting for string values when needed
  - Update cursor position after insertion
  - Close autocomplete dropdown after selection
  - _Requirements: 1.3, 2.3, 3.3_

- [x] 3.4 Implement keyboard navigation


  - Modify onAutocompleteKeydown to handle arrow up/down when dropdown is open
  - Update selectedSuggestionIndex on arrow key press
  - Prevent default arrow key behavior when autocomplete is open (don't trigger history)
  - Handle Enter key to select highlighted suggestion
  - Handle Escape key to close dropdown
  - Handle Tab key to select and continue
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 6.1_

- [x] 3.5 Implement cleanup and edge case handling


  - Add ngOnDestroy to unsubscribe from observables
  - Close autocomplete when input loses focus
  - Close autocomplete when clicking outside
  - Ensure autocomplete doesn't interfere with existing validation
  - _Requirements: 4.5, 6.2, 6.4_

- [x] 4. Update QueryInputComponent template with AutoComplete





  - Replace simple input with p-autoComplete component
  - Bind [(ngModel)] to query property
  - Bind [suggestions] to autocompleteSuggestions
  - Add (completeMethod) handler for updateAutocompleteSuggestions
  - Add (onSelect) handler for selectSuggestion
  - Preserve existing keydown handlers (enter, arrow keys for history when closed)
  - Add custom template for suggestion items showing display text and metadata
  - Apply existing CSS classes for validation styling
  - _Requirements: 1.1, 1.3, 1.5, 6.3_

- [x] 5. Add autocomplete styling






  - Create CSS classes for autocomplete dropdown positioning
  - Style suggestion items with distinct visual appearance
  - Add styling for selected/highlighted suggestion
  - Style metadata badges (field type, description)
  - Ensure dropdown doesn't obscure input or other UI elements
  - Add responsive styles for mobile devices
  - Ensure high contrast mode support
  - _Requirements: 6.3, 6.5_

- [x] 6. Handle special BQL syntax cases




- [x] 6.1 Handle parentheses and grouping


  - Detect when cursor is inside parentheses
  - Reset context to field suggestions after opening parenthesis
  - Maintain proper context through nested parentheses
  - _Requirements: 5.2_

- [x] 6.2 Handle negation operator


  - Detect "!" prefix before fields or operators
  - Provide appropriate suggestions after negation
  - Handle "!contains", "!like", "!in", "!exists" operators
  - _Requirements: 5.4_

- [x] 6.3 Handle quoted strings and regex patterns



  - Detect when cursor is inside quoted strings
  - Disable autocomplete inside quotes (free text)
  - Detect regex pattern format /pattern/flags
  - Disable autocomplete inside regex patterns
  - _Requirements: 5.5_

- [x] 7. Integrate with existing QueryBuilderConfig






  - Ensure autocomplete respects field.operators configuration
  - Use field.options for value suggestions when available
  - Call field.categorySource for dynamic value suggestions
  - Handle field.nullable for "is null" / "is not null" operators
  - Respect field.validator constraints in suggestions
  - Support computed fields appropriately
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

- [x] 8. Add error handling and fallbacks





  - Handle missing or incomplete QueryLanguageSpec gracefully
  - Provide minimal suggestions when config is not available
  - Log warnings for configuration issues without breaking functionality
  - Handle tokenization errors in malformed queries
  - Continue providing suggestions even when validateBql returns false
  - _Requirements: 5.5, 7.1_

- [x] 9. Optimize performance





  - Implement debouncing for input changes (150ms)
  - Cache field and operator suggestions per QueryLanguageSpec
  - Limit displayed suggestions to 10-15 items
  - Cancel pending suggestion requests when new input arrives
  - Implement proper cleanup in ngOnDestroy to prevent memory leaks
  - _Requirements: 6.1, 6.2_

- [x] 10. Add unit tests for BqlAutocompleteService






  - Test analyzeContext with various cursor positions and query states
  - Test getFieldSuggestions with different prefixes and configs
  - Test getOperatorSuggestions for different field types
  - Test getValueSuggestions for boolean, option-based, and free-text fields
  - Test prefix filtering (case-insensitive)
  - Test edge cases: empty query, malformed query, cursor at boundaries
  - Test with quoted strings and regex patterns
  - _Requirements: 1.1, 1.2, 2.1, 3.1, 5.3, 5.5_

- [x] 11. Add integration tests for QueryInputComponent






  - Test autocomplete dropdown appears on input
  - Test keyboard navigation (arrow keys, Enter, Escape)
  - Test suggestion selection and insertion
  - Test cursor position after insertion
  - Test autocomplete closes on blur and outside click
  - Test integration with existing validation
  - Test integration with history navigation
  - Test with different QueryLanguageSpec configurations
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 6.1, 6.2, 6.4_
