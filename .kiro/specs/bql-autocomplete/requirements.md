# Requirements Document

## Introduction

This feature adds intelligent autocomplete functionality to the BQL (Boolean Query Language) query input component. The autocomplete will provide context-aware suggestions based on the QueryLanguageSpec fields defined in JSON entity specifications (like supreme.json). Users will be able to see available field names, operators, and values while typing queries, improving discoverability and reducing syntax errors.

## Requirements

### Requirement 1

**User Story:** As a user typing a BQL query, I want to see autocomplete suggestions for field names, so that I can quickly discover available fields without memorizing the schema.

#### Acceptance Criteria

1. WHEN the user types in the query input THEN the system SHALL display a dropdown list of matching field names from the QueryLanguageSpec
2. WHEN the user types a partial field name THEN the system SHALL filter suggestions to show only fields that match the typed characters
3. WHEN the user selects a field from the autocomplete list THEN the system SHALL insert the field name at the cursor position
4. WHEN no fields match the typed text THEN the system SHALL hide the autocomplete dropdown
5. IF the QueryLanguageSpec contains field metadata (name, type) THEN the system SHALL display this information in the autocomplete suggestions

### Requirement 2

**User Story:** As a user constructing a BQL query, I want to see valid operators for the current field, so that I can build syntactically correct queries without trial and error.

#### Acceptance Criteria

1. WHEN the user types a field name followed by a space or operator character THEN the system SHALL display available operators for that field
2. WHEN the field configuration specifies allowed operators THEN the system SHALL only show those operators in the autocomplete list
3. WHEN the user selects an operator from the autocomplete list THEN the system SHALL insert the operator with appropriate spacing
4. IF the field type is "string" THEN the system SHALL suggest operators like "=", "!=", "contains", "!contains", "like", "!like", "in", "!in", "exists"
5. IF the field type is "number" THEN the system SHALL suggest operators like "=", "!=", ">", ">=", "<", "<=", "in", "!in", "exists"

### Requirement 3

**User Story:** As a user entering query values, I want autocomplete suggestions for known values, so that I can quickly select from predefined options when available.

#### Acceptance Criteria

1. WHEN the user types after an operator THEN the system SHALL analyze if value suggestions are available for the current field
2. IF the field has predefined options or categories THEN the system SHALL display these as autocomplete suggestions
3. WHEN the user selects a value from the autocomplete list THEN the system SHALL insert the value with proper quoting if needed
4. WHEN the field type is "boolean" THEN the system SHALL suggest "true" and "false"
5. WHEN the operator is "in" or "!in" THEN the system SHALL support multi-value selection with comma-separated suggestions

### Requirement 4

**User Story:** As a user navigating the autocomplete dropdown, I want keyboard controls (arrow keys, Enter, Escape), so that I can efficiently select suggestions without using the mouse.

#### Acceptance Criteria

1. WHEN the autocomplete dropdown is visible THEN the system SHALL allow arrow up/down keys to navigate suggestions
2. WHEN the user presses Enter on a highlighted suggestion THEN the system SHALL insert that suggestion into the query
3. WHEN the user presses Escape THEN the system SHALL close the autocomplete dropdown
4. WHEN the user presses Tab on a highlighted suggestion THEN the system SHALL insert that suggestion and continue editing
5. WHEN the user clicks outside the autocomplete dropdown THEN the system SHALL close the dropdown

### Requirement 5

**User Story:** As a user typing complex queries with parentheses and logical operators, I want context-aware autocomplete, so that suggestions remain relevant throughout the query construction.

#### Acceptance Criteria

1. WHEN the user types after a logical operator ("&", "|") THEN the system SHALL reset context and suggest field names
2. WHEN the user types inside parentheses THEN the system SHALL provide appropriate suggestions based on the current position
3. WHEN the cursor is positioned in the middle of an existing query THEN the system SHALL analyze the surrounding context to provide relevant suggestions
4. WHEN the user types "!" THEN the system SHALL suggest negation-compatible operators and fields
5. IF the validateBql function indicates invalid syntax THEN the system SHALL still attempt to provide helpful suggestions based on partial context

### Requirement 6

**User Story:** As a user with existing queries, I want the autocomplete to integrate seamlessly with the current query input component, so that my workflow is not disrupted.

#### Acceptance Criteria

1. WHEN autocomplete is active THEN the system SHALL not interfere with existing features like query history navigation (arrow up/down when dropdown is closed)
2. WHEN the user is editing a query THEN the system SHALL maintain the existing validation and error highlighting behavior
3. WHEN the autocomplete dropdown is displayed THEN the system SHALL position it appropriately to avoid obscuring the input or other UI elements
4. WHEN the query input loses focus THEN the system SHALL close the autocomplete dropdown
5. IF the user has typed a complete valid query segment THEN the system SHALL automatically close the autocomplete dropdown

### Requirement 7

**User Story:** As a developer maintaining the system, I want the autocomplete implementation to be reusable and configurable, so that it can be easily adapted for different entity types.

#### Acceptance Criteria

1. WHEN the QueryLanguageSpec is provided to the query-input component THEN the system SHALL dynamically generate autocomplete suggestions based on the spec
2. WHEN different entity JSON files have different field configurations THEN the system SHALL adapt autocomplete suggestions accordingly
3. WHEN the spec includes custom operators or field types THEN the system SHALL support these in autocomplete suggestions
4. IF the spec changes at runtime THEN the system SHALL update autocomplete suggestions to reflect the new configuration
5. WHEN implementing autocomplete logic THEN the system SHALL leverage existing BQL parsing utilities (validateBql, bqlToRuleset) where applicable
